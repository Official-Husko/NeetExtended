using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using NeetModKit.Game;
using UnityEngine;

namespace NeetModKit.Patches;

/// <summary>
/// What <see cref="Sky"/> needs from the game: the sky is found as the house starts, and the storm loop's
/// multiplier is applied right after the game sets its volume for a room.
/// </summary>
[HarmonyPatch]
internal static class SkyPatches
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(HouseManager), "Start")]
    private static void AfterHouseStart() => SkyScene.Ensure();

    // Brain.OnTriggerEnter raises OnLocationChange, where BrainPlayer sets the storm volume.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Brain), "OnTriggerEnter")]
    private static void AfterRoomChange(Brain __instance)
    {
        if (__instance is BrainPlayer && Sky.StormVolume.Applied)
            SkyScene.ApplyStormVolume();
    }
}

/// <summary>
/// The thunder loop's wait (<c>new WaitForSeconds(Random.Range(30f, 180f))</c>) goes through
/// <see cref="ThunderTiming"/>. If the loop's code isn't what this expects, it's left alone.
/// </summary>
[HarmonyPatch]
internal static class ThunderWaitPatch
{
    private static readonly MethodInfo Range = AccessTools.Method(typeof(Random), nameof(Random.Range), new[] { typeof(float), typeof(float) });
    private static readonly ConstructorInfo NewWait = AccessTools.Constructor(typeof(WaitForSeconds), new[] { typeof(float) });
    private static readonly MethodInfo Delay = AccessTools.Method(typeof(ThunderTiming), nameof(ThunderTiming.Delay));
    private static readonly MethodInfo Wait = AccessTools.Method(typeof(ThunderTiming), nameof(ThunderTiming.Wait));

    private static MethodBase TargetMethod() =>
        AccessTools.EnumeratorMoveNext(AccessTools.Method(typeof(HouseManager), nameof(HouseManager.Thunder)));

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code)
    {
        var list = new List<CodeInstruction>(code);
        for (int i = 0; i + 3 < list.Count; i++)
        {
            if (IsFloat(list[i], 30f) && IsFloat(list[i + 1], 180f) && list[i + 2].Calls(Range) &&
                list[i + 3].opcode == OpCodes.Newobj && (ConstructorInfo)list[i + 3].operand == NewWait)
            {
                list[i + 2] = new CodeInstruction(OpCodes.Call, Delay).MoveLabelsFrom(list[i + 2]);
                list[i + 3] = new CodeInstruction(OpCodes.Call, Wait).MoveLabelsFrom(list[i + 3]);
                return list;
            }
        }
        KitMod.Instance?.Log.Warn("The thunder loop looks different from what was expected, so Sky can't time or look at it (NextStrikeIn, SetThunderDelay).");
        return list;
    }

    private static bool IsFloat(CodeInstruction instruction, float value) =>
        instruction.opcode == OpCodes.Ldc_R4 && (float)instruction.operand == value;
}
