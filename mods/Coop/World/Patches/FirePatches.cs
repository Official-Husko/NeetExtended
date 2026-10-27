using Coop.World.Flames;
using Coop.World.Hazards;
using HarmonyLib;
using NeetModKit.Game;
using UnityEngine;

namespace Coop.World.Patches;

/// <summary>
/// Where the room fire changes. The host sends each flame lit and each flame gone; a flame put out on
/// a client goes out on the host too; a fire a client starts is started by the host; and a client's
/// flames only go when the host says so. See <see cref="FireSync"/>.
/// </summary>
[HarmonyPatchCategory(WorldPatches.World)]
[HarmonyPatch]
internal static class FirePatches
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(FireParticle), nameof(FireParticle.Kill))]
    private static void FlameKilled(FireParticle __instance) => FireSync.FlameOut(__instance);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(FireSpreaderSphere), nameof(FireSpreaderSphere.SpawnFireParticle), typeof(Vector3))]
    private static void FlameLit(FireSpreaderSphere __instance, Vector3 pos) => FireSync.FlameLit(__instance, pos);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(FireSpreaderSphere), nameof(FireSpreaderSphere.DespawnFireParticle))]
    private static void FlameGone(FireSpreaderSphere __instance, GameObject part)
    {
        if (part != null && __instance.fireParts != null && __instance.fireParts.Contains(part))
            FireSync.FlameGone(__instance, part.transform.position);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(FireSpreader), nameof(FireSpreader.Clear))]
    private static void FireOut(FireSpreader __instance) => FireSync.FireOut(__instance);

    // A client's flame of the room fire burns out when the host's does (an explosion's fireball has no fire to follow).
    [HarmonyPrefix]
    [HarmonyPatch(typeof(FireParticle), "OnParticleSystemStopped")]
    private static bool FlameBurntOut(FireParticle __instance) =>
        __instance.fireSphere == null || WorldRole.RunsWorld || FireSync.Matching;

    // A fire started on a client is started by the host, so there is one fire and one place it spreads from.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Locator), nameof(Locator.StartRoomFire))]
    private static bool StartFire(Location loc, Vector3 origin, bool byUsableCol, ref FireSpreader __result)
    {
        if (!WorldSide.Running || !WorldSide.IsClient || FireSync.Matching)
            return true;
        if (!GasSync.Replaying)
            FireStarts.Request(loc, origin, byUsableCol);
        var go = Locator.GetRoom(loc)?.fireSpreader;
        __result = go != null ? go.GetComponent<FireSpreader>() : null;
        return false;
    }
}
