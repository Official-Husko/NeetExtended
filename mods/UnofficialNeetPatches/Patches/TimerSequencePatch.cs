using System;
using HarmonyLib;
using UnofficialNeetPatches.Fixes;

namespace UnofficialNeetPatches.Patches;

/// <summary>
/// TimerSequence.Update runs the sequence when its timer is up and only then clears "active". If
/// RunSequence throws, "active" stays set and the sequence runs again next frame. The finalizer
/// clears it and puts the error in the error report with where it happened, instead of letting it
/// through (which would log it a second time).
/// </summary>
[HarmonyPatch(typeof(TimerSequence), "Update")]
[HarmonyPatchCategory(StuckSequencesFix.FeatureId)]
internal static class TimerSequencePatch
{
    private static Exception Finalizer(TimerSequence __instance, Exception __exception)
    {
        if (__exception == null || !__instance.active || __instance.timer > 0f)
            return __exception;

        __instance.active = false;
        UnofficialNeetPatchesMod.Instance?.Log.Warn($"The story event {__instance.sequenceName} failed and won't be retried (details in errors.log).");
        PatchReports.StoryEventFailed(__instance.sequenceName, __exception);
        return null;
    }
}
