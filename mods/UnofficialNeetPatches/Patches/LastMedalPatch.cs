using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using UnofficialNeetPatches.Fixes;

namespace UnofficialNeetPatches.Patches;

/// <summary>
/// AchievementViewer.Start lays out a wing (and the medal hooks) for each achievement while
/// i * 5 + j &lt; achievements.Count - 1 (AchievementViewer.cs:26), so the last achievement (pissCount)
/// never gets one. The "- 1" becomes "- 0".
/// </summary>
[HarmonyPatch(typeof(AchievementViewer), "Start")]
[HarmonyPatchCategory(LastMedalFix.FeatureId)]
internal static class LastMedalPatch
{
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code)
    {
        var list = new List<CodeInstruction>(code);
        for (int i = 0; i + 2 < list.Count; i++)
        {
            if (list[i].operand is System.Reflection.MethodInfo method && method.Name == "get_Count" &&
                list[i + 1].opcode == OpCodes.Ldc_I4_1 && list[i + 2].opcode == OpCodes.Sub)
            {
                list[i + 1] = new CodeInstruction(OpCodes.Ldc_I4_0).MoveLabelsFrom(list[i + 1]);
                break;
            }
        }
        return list;
    }
}
