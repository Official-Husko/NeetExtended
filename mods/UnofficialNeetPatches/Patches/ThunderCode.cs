using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace UnofficialNeetPatches.Patches;

/// <summary>What the thunder patches share: where the loop's code is, and reading its constants.</summary>
internal static class ThunderCode
{
    /// <summary>The body of <c>HouseManager.Thunder</c>, a coroutine, which the compiler turned into a state machine's MoveNext.</summary>
    public static MethodBase ThunderMoveNext() =>
        AccessTools.EnumeratorMoveNext(AccessTools.Method(typeof(HouseManager), nameof(HouseManager.Thunder)));

    public static bool IsFloat(CodeInstruction instruction, float value) =>
        instruction.opcode == OpCodes.Ldc_R4 && (float)instruction.operand == value;

    /// <summary>The patches leave the game's code alone when it isn't what they expect; they say so once.</summary>
    public static void NotFound(string what) =>
        UnofficialNeetPatchesMod.Instance?.Log.Warn($"{what}: the game's code isn't what this expects, so the fix was left off.");
}
