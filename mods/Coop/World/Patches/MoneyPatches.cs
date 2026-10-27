using Coop.World.Economy;
using HarmonyLib;

namespace Coop.World.Patches;

/// <summary>Every money change in the game goes through this setter; see <see cref="MoneySync"/>.</summary>
[HarmonyPatchCategory(WorldPatches.World)]
[HarmonyPatch(typeof(Computer), nameof(Computer.Money), MethodType.Setter)]
internal static class MoneyPatches
{
    private static bool Prefix(Computer __instance, float value) => MoneySync.BeforeSet(__instance, value);

    private static void Postfix(Computer __instance, bool __runOriginal)
    {
        if (__runOriginal)
            MoneySync.AfterSet(__instance);
    }
}
