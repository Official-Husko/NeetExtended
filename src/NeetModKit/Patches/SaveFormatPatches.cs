using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using NeetModKit.Saves;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NeetModKit.Patches;

/// <summary>
/// Swaps the three IO calls in the game's save code for SaveStorage versions. Transpilers keep
/// the rest of SaveAll/LoadAll exactly as the game wrote them.
/// </summary>
[HarmonyPatch(typeof(SaveLoadHandler))]
internal static class SaveFormatPatches
{
    private static readonly MethodInfo WriteAllText = AccessTools.Method(typeof(File), nameof(File.WriteAllText), new[] { typeof(string), typeof(string) });
    private static readonly MethodInfo ReadAllText = AccessTools.Method(typeof(File), nameof(File.ReadAllText), new[] { typeof(string) });
    private static readonly MethodInfo TokenToString = AccessTools.Method(typeof(JToken), nameof(JToken.ToString), new[] { typeof(Formatting), typeof(JsonConverter[]) });

    [HarmonyTranspiler]
    [HarmonyPatch(nameof(SaveLoadHandler.SaveAll))]
    private static IEnumerable<CodeInstruction> RedirectWrite(IEnumerable<CodeInstruction> code) =>
        Replace(Replace(code, WriteAllText, nameof(SaveStorage.WriteGameSave)), TokenToString, nameof(SaveStorage.Serialize));

    [HarmonyTranspiler]
    [HarmonyPatch(nameof(SaveLoadHandler.LoadAll))]
    private static IEnumerable<CodeInstruction> RedirectRead(IEnumerable<CodeInstruction> code) =>
        Replace(code, ReadAllText, nameof(SaveStorage.ReadGameSave));

    /// <summary>Points calls to <paramref name="original"/> at a static SaveStorage method with the same stack shape.</summary>
    private static IEnumerable<CodeInstruction> Replace(IEnumerable<CodeInstruction> code, MethodInfo original, string replacement)
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
