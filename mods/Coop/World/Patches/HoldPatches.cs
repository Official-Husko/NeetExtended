using Coop.World.Ownership;
using HarmonyLib;
using UnityEngine;

namespace Coop.World.Patches;

/// <summary>
/// Picking up and letting go. A successful pickup always ends in Usable.SetPlayerRef; drops and
/// throws go through ResetPlayerRef. The pickup guard keeps anyone here from grabbing what another
/// player holds (replicas never point at a remote holder: the game would read its scripts).
/// </summary>
[HarmonyPatchCategory(WorldPatches.World)]
[HarmonyPatch]
internal static class HoldPatches
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Usable), nameof(Usable.SetPlayerRef))]
    private static void Grabbed(Usable __instance, GameObject thisPlayer) => HoldEvents.Grabbed(__instance, thisPlayer);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Usable), nameof(Usable.ResetPlayerRef))]
    private static void BeforeRelease(Usable __instance, out GameObject __state) => __state = __instance.player;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Usable), nameof(Usable.ResetPlayerRef))]
    private static void Released(Usable __instance, bool delay, GameObject __state) => HoldEvents.Released(__instance, __state, delay);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Player_Abilities), nameof(Player_Abilities.Pickup), typeof(Hands), typeof(GameObject))]
    private static bool Pickup(Player_Abilities __instance, ref GameObject newPickup) => HoldEvents.MayPickUp(__instance, ref newPickup);
}
