using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnofficialNeetPatches.Fixes;

namespace UnofficialNeetPatches.Patches;

/// <summary>
/// Every death of the player ends in HouseManager.CreateAndSetNewPlayer(prevPlayer), which moves the
/// old body 600 m up and makes a new one with the prefab's default outfit; the old body is destroyed a
/// frame later. The worn clothes are separate items parked at y -500 (Wearable), and nothing frees
/// them, so they stay there for good and are saved forever. The normal death drops the hands and takes
/// the masks off, but skeletonizing moves the body to y 500 first and leaves the masks on, and
/// Obliterate drops nothing. So: remember where each death of the player happened, and when the new
/// body is made, drop whatever the old one still holds or wears there, clothes included.
/// </summary>
[HarmonyPatch]
[HarmonyPatchCategory(DeathDropsFix.FeatureId)]
internal static class DeathDropsPatches
{
    private const float Scatter = 0.35f;
    private static readonly Dictionary<GameObject, Vector3> Spots = new Dictionary<GameObject, Vector3>();

    [HarmonyPrefix]
    [HarmonyPriority(Priority.Last)]
    [HarmonyPatch(typeof(Player_Abilities), nameof(Player_Abilities.DieAccessor), typeof(bool))]
    private static void Dying(Player_Abilities __instance, bool noEffects, bool __runOriginal)
    {
        if (__runOriginal && !noEffects && !__instance.dead)
            Remember(__instance);
    }

    [HarmonyPrefix]
    [HarmonyPriority(Priority.Last)]
    [HarmonyPatch(typeof(Player_Abilities), nameof(Player_Abilities.SkeletonizeAccessor))]
    private static void Skeletonized(Player_Abilities __instance, bool __runOriginal)
    {
        if (__runOriginal)
            Remember(__instance);
    }

    [HarmonyPrefix]
    [HarmonyPriority(Priority.Last)]
    [HarmonyPatch(typeof(Player_Abilities), nameof(Player_Abilities.Obliterate))]
    private static void Obliterated(Player_Abilities __instance, bool __runOriginal)
    {
        if (__runOriginal)
            Remember(__instance);
    }

    // Only this game's player; the first death wins (skeletonizing ends in DieAccessor too).
    private static void Remember(Player_Abilities body)
    {
        var house = HouseManager._;
        if (house == null || body.gameObject != house.player || Spots.ContainsKey(body.gameObject))
            return;
        // Bodies are destroyed after the respawn; forget any that never got there.
        if (Spots.Count > 0)
        {
            var gone = new List<GameObject>();
            foreach (var key in Spots.Keys)
            {
                if (key == null)
                    gone.Add(key);
            }
            foreach (var key in gone)
                Spots.Remove(key);
        }
        Spots[body.gameObject] = body.transform.position;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(HouseManager), nameof(HouseManager.CreateAndSetNewPlayer))]
    private static void Respawning(GameObject prevPlayer)
    {
        if (prevPlayer == null || !prevPlayer.TryGetComponent(out Player_Abilities body))
            return;
        Vector3 spot = Spots.TryGetValue(prevPlayer, out var at) ? at : prevPlayer.transform.position;
        Spots.Remove(prevPlayer);

        for (int i = 0; i < body.pickups.Length && i < 2; i++)
        {
            var held = body.pickups[i];
            if (held == null)
                continue;
            body.Drop((Hands)i);
            Place(held, spot);
        }

        if (body.masks != null)
        {
            foreach (var mask in body.masks.ToArray())
            {
                if (mask != null && mask.TryGetComponent(out MaskFace face))
                {
                    face.RemoveMask(prevPlayer);
                    Place(mask, spot);
                }
            }
        }

        if (body.noClothUVs || body.clothings == null)
            return;
        for (int i = 0; i < body.clothings.Length; i++)
        {
            var piece = body.clothings[i];
            if (piece == null)
                continue;
            body.RemoveClothing(i, noHandInsert: true);
            if (body.clothings[i] == null)
                Place(piece, spot);
        }
    }

    private static void Place(GameObject item, Vector3 spot)
    {
        var offset = Random.insideUnitCircle * Scatter;
        item.transform.position = spot + new Vector3(offset.x, 0.3f, offset.y);
        if (item.TryGetComponent(out Rigidbody rb))
        {
            rb.isKinematic = false;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }
}
