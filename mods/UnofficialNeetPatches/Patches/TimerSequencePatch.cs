using System;
using HarmonyLib;
using UnofficialNeetPatches.Fixes;

namespace UnofficialNeetPatches.Patches;

/// <summary>
/// TimerSequence.Update runs the sequence when its timer is up and only then clears "active". If
/// RunSequence throws, "active" stays set and the sequence runs again next frame. The finalizer
/// clears it anyway and lets the error through, so it's logged once.
/// </summary>
[HarmonyPatch(typeof(TimerSequence), "Update")]
[HarmonyPatchCategory(StuckSequencesFix.FeatureId)]
internal static class TimerSequencePatch
{
    private static Exception Finalizer(TimerSequence __instance, Exception __exception)
    {
        if (__exception != null && __instance.active && __instance.timer <= 0f)
        {
            __instance.active = false;
            UnofficialNeetPatchesMod.Instance?.Log.Warn($"The story event {__instance.sequenceName} failed and won't be retried.");
        }
        return __exception;
    }
}
