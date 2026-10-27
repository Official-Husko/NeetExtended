using Coop.World.Breakables;
using Coop.World.Objects;
using HarmonyLib;
using UnityEngine;

namespace Coop.World.Patches;

/// <summary>
/// BreakOnImpact, where it breaks. The house's own breakables (windows, glass, walls) are decided by
/// the host: on a client a hit becomes a request and nothing breaks until the host's answer comes
/// back. On the host, and for an item broken where it's simulated, the result is announced so the
/// others hear it and see its particles.
/// </summary>
[HarmonyPatchCategory(WorldPatches.World)]
[HarmonyPatch]
internal static class BreakPatches
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(BreakOnImpact), nameof(BreakOnImpact.Impact), typeof(Vector3), typeof(Collision), typeof(float))]
    private static bool Hit(BreakOnImpact __instance, Vector3 dirForceOverride, Collision col, float additionalBreakForce, out bool __state)
    {
        __state = __instance.broken;
        if (!WorldSide.Running || !WorldSide.IsClient)
            return true;
        int id = SceneBreakers.IdOf(__instance);
        if (id == 0)
            return true;
        if (dirForceOverride == Vector3.zero && col != null && __instance.breakObjExternalForce)
            dirForceOverride = col.impulse.normalized;
        BreakNet.Request(id, false, dirForceOverride, additionalBreakForce);
        return false;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(BreakOnImpact), nameof(BreakOnImpact.Impact), typeof(Vector3), typeof(Collision), typeof(float))]
    private static void Hit(BreakOnImpact __instance, bool __state)
    {
        if (!WorldSide.Running)
            return;
        int id = SceneBreakers.IdOf(__instance);
        if (id != 0)
        {
            if (WorldSide.IsHost)
                BreakNet.AnnounceScene(id, __instance);
            return;
        }
        // An item broke where it's simulated: the others get the sound and the particles.
        if (!__state && __instance.broken)
        {
            var o = NetObjects.Find(__instance.gameObject);
            if (o != null)
                BreakNet.AnnounceItem(o);
        }
    }

    // A forced break (explosions, the thief, a cop kicking in) first sets the count the host would have to reach.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(BreakOnImpact), nameof(BreakOnImpact.ForceBreak))]
    private static bool Force(BreakOnImpact __instance)
    {
        if (!WorldSide.Running || !WorldSide.IsClient)
            return true;
        int id = SceneBreakers.IdOf(__instance);
        if (id == 0)
            return true;
        BreakNet.Request(id, true, Vector3.zero, 0f);
        return false;
    }
}
