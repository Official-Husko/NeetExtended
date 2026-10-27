using HarmonyLib;
using UnofficialNeetPatches.Fixes;

namespace UnofficialNeetPatches.Patches;

/// <summary>
/// EmailApp rebuilds the inbox from EmailHandler.deliveredEmails in its OnPostLoad handler, which it
/// only subscribes in its own Start. The save is loaded in SaveLoadHandler.Start, which runs first,
/// so the handler isn't there yet when the emails come back: deliveredEmails is full and the inbox
/// has no buttons. After Start, the inbox is built if the load already happened.
/// </summary>
[HarmonyPatch(typeof(EmailApp), "Start")]
[HarmonyPatchCategory(LostInboxFix.FeatureId)]
internal static class LostInboxPatch
{
    private static void Postfix(EmailApp __instance)
    {
        var handler = EmailHandler._;
        if (handler == null || __instance.emailContainer == null)
            return;
        if (handler.deliveredEmails.Count > 0 && __instance.emailContainer.GetComponentInChildren<Email>(true) == null)
            __instance.CreateAllDelivered();
        if (handler.pendingEmails.Count > 0)
            __instance.UpdateInbox();
    }
}
