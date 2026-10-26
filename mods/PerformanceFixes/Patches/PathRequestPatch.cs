using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using NeetModKit.Patching;
using PerformanceFixes.Features;
using PerformanceFixes.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace PerformanceFixes.Patches;

/// <summary>Every place the game sets a character's destination, routed through <see cref="PathRequests"/>.</summary>
[HarmonyPatch]
[HarmonyPatchCategory(PathRequestsFeature.FeatureId)]
internal static class PathRequestPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(BrainAgentCop), "MoveTo");
        yield return AccessTools.Method(typeof(BrainAgentCop), "StopMoving");
        yield return AccessTools.Method(typeof(BrainCat), "MoveTo");
        yield return AccessTools.Method(typeof(BrainCat), "StopMoving");
        yield return AccessTools.Method(typeof(BrainTrickOrTreater), "MoveTo");
        yield return AccessTools.Method(typeof(BrainRat), "MoveTo");
        yield return AccessTools.Method(typeof(BrainWithSegments), "MoveTo");
        yield return AccessTools.Method(typeof(DeliveryGuy), "MoveTo");
        yield return AccessTools.Method(typeof(NPCNavigationTest), "Update");
        yield return AccessTools.Method(typeof(SkeletonParasite), "MoveTo");
    }

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code) =>
        CallSwap.Replace(code,
            AccessTools.Method(typeof(NavMeshAgent), nameof(NavMeshAgent.SetDestination), new[] { typeof(Vector3) }),
            AccessTools.Method(typeof(PathRequests), nameof(PathRequests.SetDestination)));
}
