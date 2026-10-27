using Coop.World.Objects;
using HarmonyLib;
using NeetModKit.Game;
using UnityEngine;

namespace Coop.World.Patches;

/// <summary>
/// Item logic that swaps, breaks or destroys the item runs only on the game that simulates it (its
/// owner; the host for scene objects). Running it on two machines would leave two cooked steaks or
/// two sets of shards. The result reaches the others as a destroy and spawns.
/// </summary>
[HarmonyPatchCategory(WorldPatches.World)]
[HarmonyPatch]
internal static class OwnerGatePatches
{
    // Spoiling and cooking replace the food with another object.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Food), "Update")]
    private static bool FoodTimers(Food __instance) => Simulates(__instance.gameObject);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(BreakOnImpact), "OnCollisionEnter")]
    private static bool Breaking(BreakOnImpact __instance) => Simulates(__instance.gameObject);

    // Water reaching food turns it into its soaked version.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Food), "OnTriggerEnter")]
    private static bool FoodSoaks(Food __instance) => Simulates(__instance.gameObject);

    // Melting ends in a water splash and the cube going away.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(IceCube), "Update")]
    private static bool IceMelts(IceCube __instance) => Simulates(__instance.gameObject);

    // Tipping a full glass empties it (and splashes).
    [HarmonyPrefix]
    [HarmonyPatch(typeof(LiquidContainer), "Update")]
    private static bool GlassTips(LiquidContainer __instance) => Simulates(__instance.gameObject);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(LiquidContainer), "OnTriggerEnter")]
    private static bool GlassFills(LiquidContainer __instance) => Simulates(__instance.gameObject);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Fuse), nameof(Fuse.Explode))]
    private static bool FuseBlows(Fuse __instance) => Simulates(__instance.gameObject);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Lighter_BBQ), nameof(Lighter_BBQ.Explode))]
    private static bool LighterBlows(Lighter_BBQ __instance) => Simulates(__instance.gameObject);

    // A fuse drifting past a slot snaps in; only where the fuse is simulated (the slot's state follows).
    [HarmonyPrefix]
    [HarmonyPatch(typeof(FuseBox_Slot), "OnTriggerEnter")]
    private static bool FuseSnaps(Collider other) => SimulatesBodyOf(other);

    // A gib dropped in the cat's bowl becomes food; a coin dropped in the piggy bank becomes savings.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(CatFoodBowl), "OnTriggerEnter")]
    private static bool BowlEats(Collider other) => SimulatesBodyOf(other);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Piggybank), "OnTriggerEnter")]
    private static bool PiggySaves(Collider other) => SimulatesBodyOf(other);

    // A bath or sink soaks what falls in and fills empty glasses.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(WaterVolume), "OnTriggerEnter")]
    private static bool WaterSoaks(Collider other) => SimulatesBodyOf(other);

    private static bool SimulatesBodyOf(Collider other) =>
        Simulates(other.attachedRigidbody != null ? other.attachedRigidbody.gameObject : other.gameObject);

    public static bool Simulates(GameObject go)
    {
        if (!WorldSide.Running)
            return WorldRole.RunsWorld;
        var o = NetObjects.Find(go);
        return o != null ? !o.IsReplica : WorldRole.RunsWorld;
    }
}
