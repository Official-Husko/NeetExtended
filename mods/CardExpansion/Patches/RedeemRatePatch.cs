using HarmonyLib;
using NeetModKit.Game;

namespace CardExpansion.Patches;

/// <summary>The Convert button pays the configured amount per 10K crystals instead of a flat $3.</summary>
[HarmonyPatch(typeof(AnimeCardGenerator), nameof(AnimeCardGenerator.RedeemCrystals))]
[HarmonyPatchCategory(Features.BetterExchangeFeature.FeatureId)]
internal static class RedeemRatePatch
{
    private const float CrystalsPerConversion = 10000f;

    private static bool Prefix(AnimeCardGenerator __instance)
    {
        var mod = CardExpansionMod.Instance;
        if (mod == null)
            return true; // Mod unloading: let the game handle it.

        if (__instance.Crystals >= CrystalsPerConversion)
        {
            __instance.Crystals -= CrystalsPerConversion;
            Money.Add(mod.Config.Value.DollarsPer10K); // Taxed, like the game's own payout.
        }
        return false;
    }
}
