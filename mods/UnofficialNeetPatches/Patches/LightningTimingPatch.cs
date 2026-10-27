using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using UnofficialNeetPatches.Fixes;

namespace UnofficialNeetPatches.Patches;

/// <summary>
/// The lightning flash eases the Moon's light with <c>Mathf.Lerp(.., 0.6f)</c> and <c>Lerp(.., 0.1f)</c>
/// once per frame (HouseManager.cs:500-514), and each lamp's dip does the same with 0.22
/// (LightSwitch.FlickerCoroutine, :155-175). A fixed share per frame means the effect lasts half as long
/// at 120 fps as at 60. Each of those factors is turned into the share that gives the same result
/// over the same real time as at 60 fps: <c>1 - (1 - k)^(dt * 60)</c>.
/// </summary>
[HarmonyPatch]
[HarmonyPatchCategory(LightningTimingFix.FeatureId)]
internal static class LightningTimingPatch
{
    private static readonly MethodInfo Lerp = AccessTools.Method(typeof(Mathf), nameof(Mathf.Lerp), new[] { typeof(float), typeof(float), typeof(float) });
    private static readonly MethodInfo Share = AccessTools.Method(typeof(LightningTimingPatch), nameof(At60Fps));

    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return ThunderCode.ThunderMoveNext();
        yield return AccessTools.EnumeratorMoveNext(AccessTools.Method(typeof(LightSwitch), "FlickerCoroutine"));
    }

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code, MethodBase original)
    {
        var untouched = new List<CodeInstruction>(code);
        var list = new List<CodeInstruction>(untouched);
        int found = 0;
        for (int i = 0; i + 1 < list.Count; i++)
        {
            // "...Lerp(a, b, <constant>)": the constant is the last argument.
            if (list[i].opcode == OpCodes.Ldc_R4 && list[i + 1].Calls(Lerp))
            {
                list.Insert(i + 1, new CodeInstruction(OpCodes.Call, Share));
                found++;
                i++;
            }
        }
        // The flash has two (rise and fade) and each lamp's flicker two (dip and recover).
        if (found != 2)
        {
            ThunderCode.NotFound("Lightning looks the same at any frame rate (" + original.DeclaringType?.DeclaringType?.Name + ")");
            return untouched;
        }
        return list;
    }

    // Longest frame counted, so a hitch doesn't skip the effect and a paused game (dt 0) doesn't freeze it.
    private const float LongestFrame = 0.25f;
    private const float ReferenceFps = 60f;

    private static float At60Fps(float perFrame)
    {
        float frames = Mathf.Clamp(Time.unscaledDeltaTime, 0.0001f, LongestFrame) * ReferenceFps;
        return 1f - Mathf.Pow(1f - perFrame, frames);
    }
}
