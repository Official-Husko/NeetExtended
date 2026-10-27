using Coop.World.Economy;
using HarmonyLib;

namespace Coop.World.Patches;

/// <summary>The card game's buttons on a client become requests to the host; see <see cref="CardSync"/>.</summary>
[HarmonyPatchCategory(WorldPatches.World)]
[HarmonyPatch]
internal static class CardPatches
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(AnimeCard), nameof(AnimeCard.ClickedCard))]
    private static bool Clicked(AnimeCard __instance) => CardSync.Request(CardSync.Click, __instance);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(AnimeCard), nameof(AnimeCard.BuyCard))]
    private static bool Bought(AnimeCard __instance) => CardSync.Request(CardSync.Buy, __instance);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(AnimeCard), nameof(AnimeCard.PromoteCard))]
    private static bool Promoted(AnimeCard __instance) => CardSync.Request(CardSync.Promote, __instance);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(AnimeCardGenerator), nameof(AnimeCardGenerator.RedeemCrystals))]
    private static bool Redeemed() => CardSync.Request(CardSync.Redeem, null);
}
