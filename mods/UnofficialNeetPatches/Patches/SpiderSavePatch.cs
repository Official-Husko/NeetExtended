using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnofficialNeetPatches.Fixes;

namespace UnofficialNeetPatches.Patches;

/// <summary>
/// Brain.SaveBasicBrain saves a creature's other saveable components with the filter
/// <c>x =&gt; x != (ISaveable)this &amp;&amp; ...</c>. The cast throws for brains that aren't
/// ISaveable themselves, and Spider_House is the only one. The filter only runs when the spider has
/// a saveable component, which Spider_House.Die adds (BreakOnImpact), so every save with a dead
/// spider around fails. Turning the cast into an "as" keeps the filter's meaning (a brain that
/// isn't saveable can't be one of the components) without throwing.
/// </summary>
[HarmonyPatch]
[HarmonyPatchCategory(SpiderSavesFix.FeatureId)]
internal static class SpiderSavePatch
{
    private static MethodBase TargetMethod() =>
        AccessTools.GetDeclaredMethods(typeof(Brain))
            .FirstOrDefault(m => m.Name.StartsWith("<SaveBasicBrain>", StringComparison.Ordinal) && m.ReturnType == typeof(bool))
        ?? throw new MissingMethodException("Brain's save filter wasn't found; the game has changed.");

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code)
    {
        foreach (var instruction in code)
        {
            if (instruction.opcode == OpCodes.Castclass && instruction.operand as Type == typeof(ISaveable))
                instruction.opcode = OpCodes.Isinst;
            yield return instruction;
        }
    }
}
