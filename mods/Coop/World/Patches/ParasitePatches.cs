using Coop.World.Forwarding;
using Coop.World.Objects;
using Coop.World.Simulation;
using HarmonyLib;
using UnityEngine;
using UnityEngine.AI;

namespace Coop.World.Patches;

/// <summary>
/// Host: the skeleton parasite goes after the nearest player, not only the host's. Reaching a remote
/// player, it possesses them in their own game (see <see cref="Forwarder.Possessed"/>) and leaves the
/// way its own possession does.
/// </summary>
[HarmonyPatchCategory(WorldPatches.World)]
[HarmonyPatch]
internal static class ParasitePatches
{
    private const float Close = 2.3f;
    private const float Touch = 1.1f;

    private static readonly AccessTools.FieldRef<SkeletonParasite, NavMeshAgent> Nav =
        AccessTools.FieldRefAccess<SkeletonParasite, NavMeshAgent>("nav");

    private static bool Hosting => WorldSide.Running && WorldSide.IsHost && PlayerRooms.AnyRemote();

    [HarmonyPrefix]
    [HarmonyPatch(typeof(SkeletonParasite), "GetPlayerNavPos")]
    private static bool ChaseNearest(SkeletonParasite __instance, ref AITarget __result)
    {
        if (!Hosting)
            return true;
        var target = PlayerRooms.Nearest(__instance.transform.position, out _);
        if (target == null)
            return true;
        __result = new AITarget(Brain.GetValidNavmeshDestination(target.transform.position));
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(SkeletonParasite), "CheckCanPossess")]
    private static bool PossessNearest(SkeletonParasite __instance)
    {
        if (!Hosting)
            return true;
        var target = PlayerRooms.Nearest(__instance.transform.position, out byte slot);
        if (target == null || slot == WorldSide.LocalSlot)
            return true; // The host's own player: the game's own possession.
        var nav = Nav(__instance);
        float distance = Vector3.Distance(__instance.transform.position, target.transform.position);
        if (distance >= Close)
        {
            if (nav != null)
                nav.speed = 0.9f;
            return false;
        }
        if (nav != null)
            nav.speed = 2f;
        if (distance >= Touch)
            return false;

        // As its own possession, with the player's part done in their game.
        if (nav != null)
            nav.enabled = false;
        var audio = __instance.GetComponents<AudioSource>();
        if (audio.Length > 1)
        {
            audio[0].Stop();
            audio[1].Play();
        }
        var o = NetObjects.Find(__instance.gameObject);
        if (o != null)
            Forwarder.Possessed(slot, o.Ref);
        var blood = Resources.Load<GameObject>("SpraySphere_Blood_Grav");
        if (blood != null)
            Object.Instantiate(blood, __instance.transform.position + Vector3.up * 0.2f, Quaternion.identity);
        __instance.transform.position = Vector3.up * 500f;
        __instance.possesses = true;
        SaveLoadHandler._.UnsubSave(__instance.Save);
        Object.Destroy(__instance.gameObject, 8f);
        return false;
    }
}
