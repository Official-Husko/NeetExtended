using HarmonyLib;

namespace NeetModKit.Patches;

/// <summary>
/// Never send feedback reports from a modded game: the form belongs to the original developer,
/// the game is abandoned, and reports from changed code would only be misleading.
/// </summary>
[HarmonyPatch(typeof(Feedback_Sender), nameof(Feedback_Sender.SendFeedback))]
internal static class FeedbackPatches
{
    private static bool Prefix()
    {
        KitMod.Instance?.Log.Info("Feedback sending is turned off in modded games.");
        return false;
    }
}
