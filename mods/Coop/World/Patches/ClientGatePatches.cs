using System.Collections;
using HarmonyLib;
using NeetModKit.Game;
using UnityEngine;

namespace Coop.World.Patches;

/// <summary>
/// Client: the parts of the house that act on their own and can't simply be switched off, because
/// they restart themselves (coroutines from Start and Load) or exist many times over. Each is skipped
/// while this game shows the host's house; the host's results arrive as spawns and state.
/// </summary>
[HarmonyPatchCategory(WorldPatches.Client)]
[HarmonyPatch]
internal static class ClientGatePatches
{
    private static bool RunsWorld => WorldRole.RunsWorld;

    // Ordering black-market goods, box values and overdue taxes roll for the cop van.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(HouseManager), nameof(HouseManager.SpawnCopVan))]
    private static bool CopVan(ref GameObject __result)
    {
        if (RunsWorld)
            return true;
        __result = null;
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(HouseManager), nameof(HouseManager.QueueCorpseReaping))]
    private static bool Reaper() => RunsWorld;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Phone_Dispatch), nameof(Phone_Dispatch.SchedulePrankCall))]
    private static bool PrankCalls(ref IEnumerator __result) => Gate(ref __result);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(StockMarket), "UpdatePrices")]
    private static bool StockPrices(ref IEnumerator __result) => Gate(ref __result);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(EmailHandler), "SendSpam")]
    private static bool Spam(ref IEnumerator __result) => Gate(ref __result);

    // Each unlocked card earns experience and crystals while the computer is on.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(AnimeCard), "Update")]
    private static bool CardTicks() => RunsWorld;

    // The delivery guy's puppet walking in would sell boxes (and pay out) here too.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(PickupPackageArea), "OnTriggerEnter")]
    private static bool BoxSales() => RunsWorld;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(FireSpreader), "FixedUpdate")]
    private static bool FireSpread() => RunsWorld;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(FireSpreaderSphere), "Update")]
    private static bool FireSpheres() => RunsWorld;

    private static bool Gate(ref IEnumerator __result)
    {
        if (RunsWorld)
            return true;
        __result = Nothing();
        return false;
    }

    private static IEnumerator Nothing()
    {
        yield break;
    }
}

/// <summary>
/// Client: physics callbacks still reach switched-off components, and these NPCs act in them
/// (a rat skinned by a knife, a spider biting, the mushroom boy rolling over). Only on the host.
/// </summary>
[HarmonyPatchCategory(WorldPatches.Client)]
[HarmonyPatch]
internal static class NpcCallbackPatches
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(BrainRat), "OnCollisionEnter")]
    private static bool RatHit() => WorldRole.RunsWorld;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(BrainRat), "OnTriggerEnter")]
    private static bool RatSees() => WorldRole.RunsWorld;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Spider_House), "OnCollisionEnter")]
    private static bool SpiderBite() => WorldRole.RunsWorld;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(MushroomBoy), "OnCollisionEnter")]
    private static bool MushroomRoll() => WorldRole.RunsWorld;
}
