using HarmonyLib;
using UnofficialNeetPatches.Fixes;

namespace UnofficialNeetPatches.Patches;

/// <summary>
/// TV.Load switches the TV on and shows the saved channel (TV.cs:81-98), but TV.Start runs after the
/// save has loaded and turns the screen light and every channel off again (:119-124). The TV stays
/// "on", playing the channel's sound over a dark screen, until the remote is used. The saved channel
/// is shown again once Start is done. A TV that's off isn't touched.
/// </summary>
[HarmonyPatch(typeof(TV), "Start")]
[HarmonyPatchCategory(TvAfterLoadFix.FeatureId)]
internal static class TvAfterLoadPatch
{
    private static void Postfix(TV __instance)
    {
        if (__instance.IsOn && __instance.displayedScreenModule >= 0)
            __instance.Control(__instance.displayedScreenModule, noAudio: true);
    }
}
