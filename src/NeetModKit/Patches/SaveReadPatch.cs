using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using NeetModKit.Patching;
using NeetModKit.Saves;

namespace NeetModKit.Patches;

/// <summary>
/// LoadAll reads the save through SaveFormats, so mods can add formats, and lists the save folder
/// through SaveSource, so a snapshot in memory can stand in for the files.
/// </summary>
[HarmonyPatch(typeof(SaveLoadHandler), nameof(SaveLoadHandler.LoadAll))]
internal static class SaveReadPatch
{
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code)
    {
        code = CallSwap.Replace(code,
            AccessTools.Method(typeof(File), nameof(File.ReadAllText), new[] { typeof(string) }),
            AccessTools.Method(typeof(SaveFormats), nameof(SaveFormats.ReadText)));
        return CallSwap.Replace(code,
            AccessTools.Method(typeof(DirectoryInfo), nameof(DirectoryInfo.GetFiles), Type.EmptyTypes),
            AccessTools.Method(typeof(SaveSource), nameof(SaveSource.GetFiles)));
    }
}
