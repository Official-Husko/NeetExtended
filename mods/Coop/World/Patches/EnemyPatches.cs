using Coop.World.Hostiles;
using HarmonyLib;
using UnityEngine;

namespace Coop.World.Patches;

/// <summary>
/// Enemies go after the nearest player who's up, host or remote (see <see cref="CopTargeting"/>,
/// <see cref="NpcTargeting"/>), and the hitman's van (<see cref="Hitman"/>). Their brains only run on
/// the host, so every hook here is the host's.
/// </summary>
[HarmonyPatchCategory(WorldPatches.World)]
[HarmonyPatch]
internal static class EnemyPatches
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(BrainAgentCop), "CheckForNeet")]
    private static bool CopSearch(BrainAgentCop __instance, ref NodeState __result) => CopTargeting.BeforeScan(__instance, ref __result);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(BrainAgentCop), "CheckForNeet")]
    private static void CopSpotted(BrainAgentCop __instance, ref NodeState __result, bool __runOriginal)
    {
        if (__runOriginal)
            CopTargeting.AfterScan(__instance, ref __result);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(BrainAgentCop), "HasNEETLineOfSight")]
    private static bool CopSight(BrainAgentCop __instance, ref NodeState __result) => CopTargeting.BeforeSight(__instance, ref __result);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(BrainAgentCop), "HasNEETLineOfSight")]
    private static void CopBlocked(BrainAgentCop __instance, NodeState __result, bool __runOriginal)
    {
        if (__runOriginal)
            CopTargeting.AfterSight(__instance, __result);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(BrainAgentCop), "CheckTargetDead")]
    private static bool CopKillCheck(BrainAgentCop __instance, ref NodeState __result) => CopTargeting.BeforeDeadCheck(__instance, ref __result);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CopVan), "Start")]
    private static void VanParked(CopVan __instance) => Hitman.VanStarted(__instance);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(BrainNeetAI), "CheckForNeet")]
    private static void NpcSpotted(BrainNeetAI __instance, ref NodeState __result) => NpcTargeting.AfterScan(__instance, ref __result);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(BrainNeetAI), "HasNeetTarget")]
    private static void NpcTarget(BrainNeetAI __instance) => NpcTargeting.BeforeHasTarget(__instance);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(BrainNeetAI), "SetDestinationReachedRot")]
    private static bool NpcFacing(BrainNeetAI __instance, ref NodeState __result) => NpcTargeting.BeforeFacing(__instance, ref __result);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Spider_House), "ScanForTargets")]
    private static void SpiderScan(Spider_House __instance, ref NodeState __result) => NpcTargeting.AfterSpiderScan(__instance, ref __result);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Spider_House), "OnCollisionEnter")]
    private static void SpiderBite(Spider_House __instance, Collision other, bool __runOriginal)
    {
        if (__runOriginal)
            NpcTargeting.AfterSpiderBite(__instance, other);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(SkeletonParasite), "Update")]
    private static void ParasiteLooks(SkeletonParasite __instance) => NpcTargeting.AfterParasiteUpdate(__instance);
}
