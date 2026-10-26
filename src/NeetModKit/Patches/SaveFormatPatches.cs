using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using NeetModKit.Saves;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NeetModKit.Patches;

/// <summary>Swaps single calls in the game's save code, leaving everything else as the game wrote it.</summary>
internal static class SaveCalls
{
    public static readonly MethodInfo WriteAllText = AccessTools.Method(typeof(File), nameof(File.WriteAllText), new[] { typeof(string), typeof(string) });
    public static readonly MethodInfo ReadAllText = AccessTools.Method(typeof(File), nameof(File.ReadAllText), new[] { typeof(string) });
    public static readonly MethodInfo TokenToString = AccessTools.Method(typeof(JToken), nameof(JToken.ToString), new[] { typeof(Formatting), typeof(JsonConverter[]) });

    /// <summary>Points calls to <paramref name="original"/> at a static SaveStorage method with the same stack shape.</summary>
    public static IEnumerable<CodeInstruction> Replace(IEnumerable<CodeInstruction> code, MethodInfo original, string replacement)
    {
        var target = AccessTools.Method(typeof(SaveStorage), replacement);
        foreach (var instruction in code)
        {
            if (instruction.operand is MethodInfo m && m == original)
                yield return new CodeInstruction(OpCodes.Call, target).MoveLabelsFrom(instruction).MoveBlocksFrom(instruction);
            else
                yield return instruction;
        }
    }
}

/// <summary>Always on: LoadAll can read .nsav saves even when writing them is switched off.</summary>
[HarmonyPatch(typeof(SaveLoadHandler), nameof(SaveLoadHandler.LoadAll))]
internal static class SaveReadPatch
{
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code) =>
        SaveCalls.Replace(code, SaveCalls.ReadAllText, nameof(SaveStorage.ReadGameSave));
}

/// <summary>Part of the Compressed saves feature: SaveAll writes minified JSON into a .nsav file.</summary>
[HarmonyPatch(typeof(SaveLoadHandler), nameof(SaveLoadHandler.SaveAll))]
[HarmonyPatchCategory(Features.Builtin.CompressedSavesFeature.FeatureId)]
internal static class SaveWritePatch
{
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code) =>
        SaveCalls.Replace(SaveCalls.Replace(code, SaveCalls.WriteAllText, nameof(SaveStorage.WriteGameSave)),
            SaveCalls.TokenToString, nameof(SaveStorage.Serialize));
}

/// <summary>Part of the Save cleanup feature.</summary>
[HarmonyPatch(typeof(SaveLoadHandler), nameof(SaveLoadHandler.SaveAll))]
[HarmonyPatchCategory(Features.Builtin.SaveCleanupFeature.FeatureId)]
internal static class SaveCleanupPatch
{
    private static void Postfix() => SaveStorage.Prune(SaveStorage.Folder);
}
