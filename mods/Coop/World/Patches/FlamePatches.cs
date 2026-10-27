using Coop.World.Hazards;
using Coop.World.Spawning;
using Coop.World.State;
using HarmonyLib;
using UnityEngine;

namespace Coop.World.Patches;

/// <summary>
/// Where flames light and go out. A flame lights only in the game that simulates what it sits on,
/// and what changed is sent (FlameSync). A client's copy of a room fire never lights or burns
/// anything that is only a copy here, and the stove's gas is decided by the host (GasSync).
/// </summary>
[HarmonyPatchCategory(WorldPatches.World)]
[HarmonyPatch]
internal static class FlamePatches
{
    private static bool Quiet => !WorldSide.Running || Flames.FlameSync.Applying || Flames.GasSync.Replaying || StateSync.Applying || SpawnContext.Silent;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Flame), nameof(Flame.Ignite))]
    private static bool Lighting(Flame __instance)
    {
        if (Quiet)
            return true;
        Flames.FlameBook.Info(__instance);
        if (Flames.FlameRules.Simulates(__instance))
            return true;
        Flames.GasSync.AskToLight(__instance);
        return false;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Flame), nameof(Flame.Ignite))]
    private static void Lit(Flame __instance)
    {
        if (Quiet)
            return;
        Flames.FlameSync.Changed(__instance);
        Flames.FireballSync.Lit(__instance);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Flame), nameof(Flame.Extinguish))]
    private static void PuttingOut(Flame __instance)
    {
        if (!Quiet)
            Flames.FlameBook.Info(__instance);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Flame), nameof(Flame.Extinguish))]
    private static void PutOut(Flame __instance)
    {
        if (!Quiet)
            Flames.FlameSync.Changed(__instance);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Flame), nameof(Flame.HeatLevel), MethodType.Setter)]
    private static void HeatChanging(Flame __instance)
    {
        if (!Quiet)
            Flames.FlameBook.Info(__instance);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Flame), nameof(Flame.HeatLevel), MethodType.Setter)]
    private static void HeatChanged(Flame __instance)
    {
        if (!Quiet)
            Flames.FlameSync.Changed(__instance);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Flame), nameof(Flame.AddBurnCollider))]
    private static void Caught(Flame __result)
    {
        if (!Quiet)
            Flames.FlameSync.Caught(__result);
    }

    // A thing catches fire where it is simulated, and a fire spreads from a flame that is simulated
    // here. A client's copy of a room fire starts nothing on furniture: the host's fire does.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Flame), nameof(Flame.MakeFlame), typeof(GameObject), typeof(Vector3?))]
    private static bool Spreading(Flame __instance, GameObject g)
    {
        if (Quiet || !__instance.lit || g == null || g.GetComponentInChildren<Flame>() != null)
            return true;
        // A copy of another game's fireball lights what is simulated here and nothing else: the
        // furniture, and anything that isn't here, catches in the game that made the ball.
        if (Flames.FireballSync.IsCopy(__instance))
            return g.GetComponent<Usable>() != null && Flames.FlameRules.Simulates(g);
        bool roomFire = Flames.FlameRules.IsRoomFire(__instance);
        if (!roomFire && !Flames.FlameRules.Simulates(__instance))
            return false;
        if (g.GetComponent<Usable>() != null)
            return Flames.FlameRules.Simulates(g);
        return !WorldSide.IsClient || !roomFire && Request(__instance, g);
    }

    // A burning item of ours touching furniture on a client: the host starts the fire.
    private static bool Request(Flame source, GameObject g)
    {
        if (g.layer == LayerMask.NameToLayer("Furniture") || g.layer == LayerMask.NameToLayer("Construction"))
        {
            var at = g.transform.position;
            FireStarts.Request(Locator.LocatePoint(at), at, true);
        }
        return false;
    }

    // A client's flames burn its own player and nobody else: the NPCs there are puppets of the host's
    // (their burns come from the host's fire), and the other players burn in their own games.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Flame), nameof(Flame.canDamageOther), typeof(ActorStats))]
    private static bool Burns(ActorStats a, ref bool __result)
    {
        if (!WorldSide.Running || !WorldSide.IsClient || a == null || a.gameObject == WorldSide.LocalPlayer)
            return true;
        __result = false;
        return false;
    }

    // A fuse or lighter that goes off (only where it is simulated) makes its fireball in every game.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Fuse), nameof(Fuse.Explode))]
    private static void FuseWentOff(Fuse __instance, bool __runOriginal)
    {
        if (__runOriginal)
            Flames.BlastSync.Exploded(__instance.GetComponent<Usable>(), __instance.transform.position);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Lighter_BBQ), nameof(Lighter_BBQ.Explode))]
    private static void LighterWentOff(Lighter_BBQ __instance, bool __runOriginal)
    {
        if (__runOriginal)
            Flames.BlastSync.Exploded(__instance.GetComponent<Usable>(), __instance.transform.position);
    }

    // An overdue tax bill burns up in a puff of fire, where the host's tax handler runs.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(TaxBill), nameof(TaxBill.Timer), MethodType.Setter)]
    private static void BillBurning(TaxBill __instance, float value)
    {
        if (value <= 0f && __instance.stage + 1 >= 3 && __instance.bill != null)
            Flames.PuffSync.Burnt(__instance.bill.transform.position);
    }
}
