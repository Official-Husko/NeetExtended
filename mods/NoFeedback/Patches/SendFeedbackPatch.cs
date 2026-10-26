using HarmonyLib;

namespace NoFeedback.Patches;

/// <summary>The form posts to the original developer's Google Form; never send from a modded game.</summary>
[HarmonyPatch(typeof(Feedback_Sender), nameof(Feedback_Sender.SendFeedback))]
internal static class SendFeedbackPatch
{
    private static bool Prefix()
    {
        NoFeedbackMod.Instance?.Log.Info("Feedback sending is turned off.");
        return false;
    }
}
