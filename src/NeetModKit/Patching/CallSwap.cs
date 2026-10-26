using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace NeetModKit.Patching;

/// <summary>Transpiler helper: swap every call to one method for a call to another.</summary>
public static class CallSwap
{
    /// <summary>
    /// Replaces calls to <paramref name="original"/> with calls to the static <paramref name="replacement"/>.
    /// The replacement must take the same stack values (for an instance method: the instance first).
    /// </summary>
    public static IEnumerable<CodeInstruction> Replace(IEnumerable<CodeInstruction> code, MethodInfo original, MethodInfo replacement)
    {
        foreach (var instruction in code)
        {
            if (instruction.operand is MethodInfo m && m == original)
                yield return new CodeInstruction(OpCodes.Call, replacement).MoveLabelsFrom(instruction).MoveBlocksFrom(instruction);
            else
                yield return instruction;
        }
    }
}
