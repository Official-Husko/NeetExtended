using Coop.World.Economy;
using HarmonyLib;

namespace Coop.World.Patches;

/// <summary>The stock market's price ticks (host) and trades (orders on a client); see <see cref="StockSync"/>.</summary>
[HarmonyPatchCategory(WorldPatches.World)]
[HarmonyPatch]
internal static class StockPatches
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Stock), nameof(Stock.ShiftPriceForward))]
    private static void Ticked(Stock __instance) => StockSync.OnTicked(__instance);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(StockMarket), nameof(StockMarket.BuyStock))]
    private static bool Buy(string ticker, int qty, bool free, ref float __result) =>
        free || StockSync.Order(buy: true, ticker, qty, out __result);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(StockMarket), nameof(StockMarket.SellStock))]
    private static bool Sell(string ticker, int qty, bool free, ref float __result) =>
        free || StockSync.Order(buy: false, ticker, qty, out __result);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(StockMarket), nameof(StockMarket.BuyStock))]
    private static void Bought(string ticker, bool free, bool __runOriginal)
    {
        if (__runOriginal && !free)
            StockSync.Traded(ticker);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(StockMarket), nameof(StockMarket.SellStock))]
    private static void Sold(string ticker, bool free, bool __runOriginal)
    {
        if (__runOriginal && !free)
            StockSync.Traded(ticker);
    }
}
