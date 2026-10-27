using System.Collections.Generic;
using HarmonyLib;
using UnofficialNeetPatches.Fixes;

namespace UnofficialNeetPatches.Patches;

/// <summary>
/// SaveLoadHandler.Update autosaves when the timer is up, the player is in the bedroom and
/// timeToLastLoad &lt;= 0 (SaveLoadHandler.cs:80). LoadAll sets timeToLastLoad to Time.time and
/// nothing ever clears it (:151), so after a load the check is false for the rest of the session.
/// The check is meant to hold autosave back right after a load: the field it reads now
/// reads 0 once the load is over.
/// </summary>
[HarmonyPatch(typeof(SaveLoadHandler), "Update")]
[HarmonyPatchCategory(AutosaveAfterLoadFix.FeatureId)]
internal static class AutosaveAfterLoadPatch
{
    private static readonly System.Reflection.FieldInfo LastLoad =
        AccessTools.Field(typeof(SaveLoadHandler), nameof(SaveLoadHandler.timeToLastLoad));

    private static readonly System.Reflection.MethodInfo Recent =
        AccessTools.Method(typeof(LoadClock), nameof(LoadClock.RecentLoadTime));

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code)
    {
        foreach (var instruction in code)
        {
            if (instruction.LoadsField(LastLoad))
                yield return new CodeInstruction(System.Reflection.Emit.OpCodes.Call, Recent).MoveLabelsFrom(instruction);
            else
                yield return instruction;
        }
    }
}
