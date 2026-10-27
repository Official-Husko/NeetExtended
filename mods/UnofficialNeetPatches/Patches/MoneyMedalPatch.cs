using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using UnofficialNeetPatches.Fixes;

namespace UnofficialNeetPatches.Patches;

/// <summary>
/// AchievementManager.Awake builds the "totalMoneyPlatinum" goal with
/// TryGetPlayerPrefBool("biggestMoneyGainPlatinum") (AchievementManager.cs:254), the other
/// achievement's key. The first "biggestMoneyGainPlatinum" after "totalMoneyPlatinum" is replaced.
/// </summary>
[HarmonyPatch(typeof(AchievementManager), "Awake")]
[HarmonyPatchCategory(MoneyMedalFix.FeatureId)]
internal static class MoneyMedalPatch
{
    private const string Goal = "totalMoneyPlatinum";
    private const string WrongKey = "biggestMoneyGainPlatinum";

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code)
    {
        bool afterGoal = false;
        foreach (var instruction in code)
        {
            if (instruction.opcode == OpCodes.Ldstr && instruction.operand as string == Goal)
                afterGoal = true;
            else if (afterGoal && instruction.opcode == OpCodes.Ldstr && instruction.operand as string == WrongKey)
            {
                instruction.operand = Goal;
                afterGoal = false;
            }
            yield return instruction;
        }
    }
}
