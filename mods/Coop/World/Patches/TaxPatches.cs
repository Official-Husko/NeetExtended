using Coop.World.Economy;
using HarmonyLib;

namespace Coop.World.Patches;

/// <summary>Paying a tax bill on a client is a request to the host; see <see cref="TaxSync"/>.</summary>
[HarmonyPatchCategory(WorldPatches.World)]
[HarmonyPatch(typeof(TaxPaymentUI), nameof(TaxPaymentUI.PayBill))]
internal static class TaxPatches
{
    private static bool Prefix(int billId) => TaxSync.Pay(billId);
}
