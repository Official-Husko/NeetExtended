using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using NeetModKit.Game;
using UnityEngine;
using UnofficialNeetPatches.Fixes;

namespace UnofficialNeetPatches.Patches;

/// <summary>
/// A lightning strike flashes the Moon (the house's only light that's on, intensity 0.2), fades it
/// and then sets its intensity to 0 (HouseManager.cs:515). Nothing sets it back (the sun and moon
/// rotation scripts that would are off in the house), so after the first strike the night has no moonlight
/// and no moon shadows. The 0 is swapped for the intensity the scene started with, which the framework's
/// Sky remembers for every house load.
/// </summary>
[HarmonyPatch]
[HarmonyPatchCategory(MoonRestoreFix.FeatureId)]
internal static class MoonRestorePatch
{
    private static readonly MethodInfo SetIntensity = AccessTools.PropertySetter(typeof(Light), nameof(Light.intensity));
    private static readonly MethodInfo SceneMoon = AccessTools.PropertyGetter(typeof(Sky), nameof(Sky.MoonBaseIntensity));

    private static MethodBase TargetMethod() => ThunderCode.ThunderMoveNext();

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code)
    {
        var untouched = new List<CodeInstruction>(code);
        var list = new List<CodeInstruction>(untouched);
        int found = 0;
        for (int i = 0; i + 1 < list.Count; i++)
        {
            // "mainDirectionalLight.intensity = 0f"
            if (ThunderCode.IsFloat(list[i], 0f) && list[i + 1].Calls(SetIntensity))
            {
                list[i] = new CodeInstruction(OpCodes.Call, SceneMoon).MoveLabelsFrom(list[i]);
                found++;
            }
        }
        if (found != 1)
        {
            ThunderCode.NotFound("The moon comes back after lightning");
            return untouched;
        }
        return list;
    }
}
