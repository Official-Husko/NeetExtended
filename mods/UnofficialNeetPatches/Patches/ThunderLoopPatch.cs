using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using UnofficialNeetPatches.Fixes;

namespace UnofficialNeetPatches.Patches;

/// <summary>
/// HouseManager starts the random thunder loop and the story's thunder sequence (Ghostboy's first line)
/// stops "thunderCorout" and starts a single strike in its place (HouseManager.cs:345-352). The loop
/// schedules its own next round by storing itself in thunderCorout (:521), so after the first random
/// strike the story's stop kills the loop, and the single strike never schedules another: thunder is over
/// for the session. The loop no longer stores itself there, so the story's stop only ever reaches its
/// own earlier strike and the loop keeps its one place in the world. Anything else that wants to start a
/// strike (<c>Sky.Strike()</c>) starts the same single strike and leaves the loop alone.
/// </summary>
[HarmonyPatch]
[HarmonyPatchCategory(ThunderLoopFix.FeatureId)]
internal static class ThunderLoopPatch
{
    private static readonly FieldInfo Stored = AccessTools.Field(typeof(HouseManager), "thunderCorout");
    private static readonly MethodInfo Skip = AccessTools.Method(typeof(ThunderLoopPatch), nameof(KeepLoopOut));

    private static MethodBase TargetMethod() => ThunderCode.ThunderMoveNext();

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code)
    {
        var untouched = new List<CodeInstruction>(code);
        var list = new List<CodeInstruction>(untouched);
        int found = 0;
        for (int i = 0; i < list.Count; i++)
        {
            // "thunderCorout = StartCoroutine(Thunder())": the store takes the HouseManager and the Coroutine.
            if (list[i].opcode == OpCodes.Stfld && (FieldInfo)list[i].operand == Stored)
            {
                list[i] = new CodeInstruction(OpCodes.Call, Skip).MoveLabelsFrom(list[i]);
                found++;
            }
        }
        if (found != 1)
        {
            ThunderCode.NotFound("Thunder keeps going");
            return untouched;
        }
        return list;
    }

    // Takes the place of the field store; the loop's coroutine is simply started and left running.
    private static void KeepLoopOut(HouseManager house, Coroutine loop) { }
}
