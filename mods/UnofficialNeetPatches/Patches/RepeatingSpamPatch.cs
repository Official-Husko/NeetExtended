using HarmonyLib;
using UnofficialNeetPatches.Fixes;

namespace UnofficialNeetPatches.Patches;

/// <summary>
/// EmailHandler.Save writes randomEmails, the list asset as it ships, instead of
/// randomEmailsInstance, the copy SendSpam marks emails off in. Load then restores the shipped list
/// into the copy, so every spam email can arrive again. The save gets the copy instead.
/// </summary>
[HarmonyPatch(typeof(EmailHandler), nameof(EmailHandler.Save))]
[HarmonyPatchCategory(RepeatingSpamFix.FeatureId)]
internal static class RepeatingSpamPatch
{
    private static void Postfix(EmailHandler __instance, SaveData __result)
    {
        if (__result is EmailHandler.SaveDataEmailHandler data && (object)__instance.randomEmailsInstance != null)
            data.randomEmails = __instance.randomEmailsInstance;
    }
}
