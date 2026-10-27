using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using NeetModKit.Patching;
using NeetModKit.Saves;

namespace NeetModKit.Patches;

/// <summary>LoadPaintTargets reads the paint pictures through SaveSource, so a snapshot can supply them.</summary>
[HarmonyPatch(typeof(SaveLoadHandler), "LoadPaintTargets")]
internal static class PaintLoadPatch
{
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code)
    {
        code = CallSwap.Replace(code,
            AccessTools.Method(typeof(File), nameof(File.Exists), new[] { typeof(string) }),
            AccessTools.Method(typeof(SaveSource), nameof(SaveSource.PaintExists)));
        return CallSwap.Replace(code,
            AccessTools.Method(typeof(File), nameof(File.ReadAllBytes), new[] { typeof(string) }),
            AccessTools.Method(typeof(SaveSource), nameof(SaveSource.PaintBytes)));
    }
}
