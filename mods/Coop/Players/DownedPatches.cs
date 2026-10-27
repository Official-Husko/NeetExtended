using HarmonyLib;

namespace Coop.Players;

/// <summary>
/// The hooks downed players need. Every way the game kills the local player (its death, being
/// skeletonized, being gibbed) becomes going down first, and the death happens later if nobody helps
/// (<see cref="Downed"/>). Food and drinks used while looking at a downed player go to them
/// (<see cref="Reviving"/>).
/// </summary>
[HarmonyPatch]
internal static class DownedPatches
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Player_Abilities), nameof(Player_Abilities.DieAccessor), typeof(bool))]
    private static bool GoDownInsteadOfDying(Player_Abilities __instance, bool noEffects, bool __runOriginal) =>
        // Something else (god mode) already stopped this death.
        __runOriginal && (noEffects || !Downed.TryGoDown(__instance, Ending.Death));

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Player_Abilities), nameof(Player_Abilities.SkeletonizeAccessor), typeof(bool))]
    private static bool GoDownInsteadOfSkeletonizing(Player_Abilities __instance, bool noSkeleton, bool __runOriginal) =>
        __runOriginal && !Downed.TryGoDown(__instance, noSkeleton ? Ending.Hollow : Ending.Skeleton);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Player_Abilities), nameof(Player_Abilities.Obliterate))]
    private static bool GoDownInsteadOfGibbing(Player_Abilities __instance, bool __runOriginal) =>
        __runOriginal && !Downed.TryGoDown(__instance, Ending.Gibbed);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Food), nameof(Food.Use))]
    private static bool FeedDownedPlayer(Food __instance) => !Reviving.TryFeed(__instance);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(LiquidContainer), nameof(LiquidContainer.Use))]
    private static bool GiveDownedPlayerADrink(LiquidContainer __instance) => !Reviving.TryGiveDrink(__instance);
}
