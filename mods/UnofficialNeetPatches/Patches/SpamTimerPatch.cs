using System;
using System.Collections;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnofficialNeetPatches.Fixes;

namespace UnofficialNeetPatches.Patches;

/// <summary>
/// EmailHandler.Save passes the time left until the next spam email to SaveDataEmailHandler, whose
/// constructor never stores it, so the save always says 0. Load then starts SendSpam from scratch,
/// which sends one 15 seconds later. The save gets the time left, and loading waits that long.
/// SendSpam(wait) waits, then 15 more seconds, then sends, so it's started with 15 seconds less.
/// </summary>
[HarmonyPatch]
[HarmonyPatchCategory(SpamTimerFix.FeatureId)]
internal static class SpamTimerPatch
{
    private const float LastWait = 15f;

    private static readonly AccessTools.FieldRef<EmailHandler, float> NextSpam =
        AccessTools.FieldRefAccess<EmailHandler, float>("timeToNextRandom");
    private static readonly AccessTools.FieldRef<EmailHandler, Coroutine> Running =
        AccessTools.FieldRefAccess<EmailHandler, Coroutine>("sendRandomCoroutine");
    private static readonly Func<EmailHandler, float, IEnumerator> SendSpam =
        AccessTools.MethodDelegate<Func<EmailHandler, float, IEnumerator>>(AccessTools.Method(typeof(EmailHandler), "SendSpam"));

    [HarmonyPostfix]
    [HarmonyPatch(typeof(EmailHandler), nameof(EmailHandler.Save))]
    private static void AfterSave(EmailHandler __instance, SaveData __result)
    {
        if (__result is EmailHandler.SaveDataEmailHandler data)
            data.timeToNextRandom = Mathf.Max(0f, NextSpam(__instance) - Time.time);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(EmailHandler), nameof(EmailHandler.Load))]
    private static void AfterLoad(EmailHandler __instance, JToken jt)
    {
        float left = jt?["timeToNextRandom"]?.Value<float>() ?? 0f;
        if (left <= LastWait)
            return; // Due within the game's own 15 seconds anyway.
        if (Running(__instance) != null)
            __instance.StopCoroutine(Running(__instance));
        NextSpam(__instance) = Time.time + left;
        Running(__instance) = __instance.StartCoroutine(SendSpam(__instance, left - LastWait));
    }
}
