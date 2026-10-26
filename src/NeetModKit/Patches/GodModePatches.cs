using HarmonyLib;
using NeetModKit.Game;

namespace NeetModKit.Patches;

/// <summary>Blocks every way the player can die while god mode is on.</summary>
[HarmonyPatch]
internal static class GodModePatches
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Player_Abilities), nameof(Player_Abilities.DieAccessor), typeof(bool))]
    private static bool BlockDie() => !PlayerControl.GodMode;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Player_Abilities), nameof(Player_Abilities.Obliterate))]
    private static bool BlockObliterate() => !PlayerControl.GodMode;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Player_Abilities), nameof(Player_Abilities.SkeletonizeAccessor))]
    private static bool BlockSkeletonize() => !PlayerControl.GodMode;
}
