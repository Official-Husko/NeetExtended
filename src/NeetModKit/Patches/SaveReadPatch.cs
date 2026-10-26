using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using NeetModKit.Patching;
using NeetModKit.Saves;

namespace NeetModKit.Patches;

/// <summary>LoadAll reads the save through SaveFormats, so mods can add formats.</summary>
[HarmonyPatch(typeof(SaveLoadHandler), nameof(SaveLoadHandler.LoadAll))]
internal static class SaveReadPatch
{
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code) =>
        CallSwap.Replace(code,
            AccessTools.Method(typeof(File), nameof(File.ReadAllText), new[] { typeof(string) }),
            AccessTools.Method(typeof(SaveFormats), nameof(SaveFormats.ReadText)));
}
