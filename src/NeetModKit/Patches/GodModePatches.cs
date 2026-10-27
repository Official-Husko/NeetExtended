using HarmonyLib;
using NeetModKit.Game;

namespace NeetModKit.Patches;

/// <summary>Blocks every way the player can die while god mode is on. Everyone else dies as usual.</summary>
[HarmonyPatch]
internal static class GodModePatches
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Player_Abilities), nameof(Player_Abilities.DieAccessor), typeof(bool))]
    private static bool BlockDie(Player_Abilities __instance) => !Shielded(__instance);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Player_Abilities), nameof(Player_Abilities.Obliterate))]
    private static bool BlockObliterate(Player_Abilities __instance) => !Shielded(__instance);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Player_Abilities), nameof(Player_Abilities.SkeletonizeAccessor))]
    private static bool BlockSkeletonize(Player_Abilities __instance) => !Shielded(__instance);

    // NPCs are player rigs too; only this game's own player is protected.
    private static bool Shielded(Player_Abilities rig) =>
        PlayerControl.GodMode && HouseManager._ != null && rig.gameObject == HouseManager._.player;
}
