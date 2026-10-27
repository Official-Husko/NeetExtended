using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using UnofficialNeetPatches.Fixes;

namespace UnofficialNeetPatches.Patches;

/// <summary>
/// Spawner.CanSpawn allows a spawn when timeToLastLoad is 0 (no load) or
/// <c>timeToLastLoad - Time.time &gt; 2</c> (Spawner.cs:274). The load time is in the past, so the
/// difference is never positive and after any load no spawner passes again. The subtraction is
/// meant the other way round: two seconds after the load. Every spawner whose Update runs
/// (nothing in the game sets externalCallsOnly) is held by this: loose change and dust
/// (HouseManager), sink water (SinkFaucet.cs:102), toilet flushes (Toilet_Flush.cs:95), the
/// skeleton parasite (SkeletonParasite.cs:182), held corpses dripping (Player_Abilities.cs:1774)
/// and the paint and blood sprays (Commons.cs:244, Player_Abilities.cs:1894).
/// The subtraction's operands are swapped by a call that returns Time.time minus the load time.
/// </summary>
[HarmonyPatch(typeof(Spawner), nameof(Spawner.CanSpawn))]
[HarmonyPatchCategory(SpawnersAfterLoadFix.FeatureId)]
internal static class SpawnersAfterLoadPatch
{
    private static readonly MethodInfo GetTime = AccessTools.PropertyGetter(typeof(Time), nameof(Time.time));
    private static readonly MethodInfo Since = AccessTools.Method(typeof(LoadClock), nameof(LoadClock.Since));

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code)
    {
        var list = new List<CodeInstruction>(code);
        for (int i = 0; i + 1 < list.Count; i++)
        {
            // "timeToLastLoad - Time.time": the Sub right after reading Time.time.
            if (list[i].Calls(GetTime) && list[i + 1].opcode == OpCodes.Sub)
            {
                list[i + 1] = new CodeInstruction(OpCodes.Call, Since).MoveLabelsFrom(list[i + 1]);
                break;
            }
        }
        return list;
    }
}
