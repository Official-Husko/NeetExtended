using HarmonyLib;
using NeetModKit.Cards;

namespace NeetModKit.Patches;

[HarmonyPatch(typeof(AnimeCardGenerator), "Start")]
internal static class CardPatches
{
    private static readonly AccessTools.FieldRef<AnimeCardGenerator, bool> Initialized =
        AccessTools.FieldRefAccess<AnimeCardGenerator, bool>("noSaveSubscribe");

    // Start collects the card objects only on its first run (guarded by noSaveSubscribe).
    private static void Prefix(AnimeCardGenerator __instance)
    {
        if (!Initialized(__instance))
            CardInjector.Inject(__instance);
    }
}
