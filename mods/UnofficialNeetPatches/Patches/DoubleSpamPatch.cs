using HarmonyLib;
using UnityEngine;
using UnofficialNeetPatches.Fixes;

namespace UnofficialNeetPatches.Patches;

/// <summary>
/// The save loads in SaveLoadHandler.Start, before EmailHandler.Start runs. EmailHandler.Load starts
/// the spam coroutine, then Start starts another one and keeps only that one in
/// sendRandomCoroutine, so two run for the rest of the session. When the load already started one,
/// the one from Start is stopped again.
/// </summary>
[HarmonyPatch(typeof(EmailHandler), "Start")]
[HarmonyPatchCategory(DoubleSpamFix.FeatureId)]
internal static class DoubleSpamPatch
{
    private static readonly AccessTools.FieldRef<EmailHandler, Coroutine> Running =
        AccessTools.FieldRefAccess<EmailHandler, Coroutine>("sendRandomCoroutine");
    private static readonly AccessTools.FieldRef<EmailHandler, float> NextSpam =
        AccessTools.FieldRefAccess<EmailHandler, float>("timeToNextRandom");

    internal struct Loaded
    {
        public Coroutine Spam;
        public float NextSpam;
    }

    private static void Prefix(EmailHandler __instance, out Loaded __state) =>
        __state = new Loaded { Spam = Running(__instance), NextSpam = NextSpam(__instance) };

    // Start's coroutine already set timeToNextRandom before its first wait, so that goes back too.
    private static void Postfix(EmailHandler __instance, Loaded __state)
    {
        if (__state.Spam == null || Running(__instance) == null || Running(__instance) == __state.Spam)
            return;
        __instance.StopCoroutine(Running(__instance));
        Running(__instance) = __state.Spam;
        NextSpam(__instance) = __state.NextSpam;
    }
}
