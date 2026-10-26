using HarmonyLib;
using UnityEngine;
using UnofficialNeetPatches.Fixes;

namespace UnofficialNeetPatches.Patches;

/// <summary>
/// StatusEffect.OnTick scales by Time.deltaTime only for tickers; persistent effects that add to a
/// stat (linearDelta) add their full size every frame. Scaling those by deltaTime times the frame
/// rate the game caps itself at (120, CameraControl.Start) keeps their strength at 120 fps and
/// makes it the same at any other rate. Multipliers and overwriters don't depend on the frame
/// rate and are left alone.
/// </summary>
[HarmonyPatch(typeof(StatusEffect), nameof(StatusEffect.OnTick))]
[HarmonyPatchCategory(EffectStrengthFix.FeatureId)]
internal static class EffectStrengthPatch
{
    private const float ReferenceFrameRate = 120f;

    private static bool Prefix(StatusEffect __instance)
    {
        if (__instance.statusType != StatusEffectType.persistent ||
            __instance.operationType != StatusEffect.OperationType.linearDelta ||
            __instance.noStatEffect || __instance.statRef == null || __instance.statRef.Count == 0 || __instance.oper == null)
            return true;

        float delta = __instance.effectSize * Time.deltaTime * ReferenceFrameRate;
        foreach (var stat in __instance.statRef)
            stat.SetVal(__instance.oper(stat.GetVal(), delta));
        return false;
    }
}
