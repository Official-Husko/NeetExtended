using HarmonyLib;
using TvStations.Tv;

namespace TvStations.Patches;

[HarmonyPatch]
internal static class TvPatches
{
    // Awake collects the channels from the TV's children; stations go in right after, before a
    // save tells the TV which channel to show.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(TV), "Awake")]
    private static void AfterAwake(TV __instance) => TvChannels.OnTvAwake(__instance);

    // Channel numbers (the remote's buttons, hitting it, loading a save) all go through here.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(TV), nameof(TV.Control), typeof(int), typeof(bool))]
    private static bool BeforeControl(TV __instance, int remoteInput) => TvChannels.Control(__instance, remoteInput);
}
