using System.Collections.Generic;
using Coop.World.Simulation;
using HarmonyLib;
using UnityEngine;

namespace Coop.World.Patches;

/// <summary>
/// Host: the house's own events treat every player, not just the host's, the way the game treats its
/// player. Visitors that shouldn't appear in front of you don't appear in front of anyone, and the
/// thief breaks in where nobody is.
/// </summary>
[HarmonyPatchCategory(WorldPatches.World)]
[HarmonyPatch]
internal static class HostSimPatches
{
    private const int Tries = 6;

    private static readonly Location[] ThiefRooms =
    {
        Location.Diningroom, Location.Livingroom, Location.BathroomFloor, Location.Bedroom_Neet, Location.Lounge, Location.Backyard,
    };

    private static readonly List<Location> Free = new List<Location>(8);
    private static bool _rolling;

    private static bool Hosting => WorldSide.Running && WorldSide.IsHost && PlayerRooms.AnyRemote();

    // Skeletons, spiders and other need visitors avoid "the player's room"; re-roll if another player is there.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(HouseManager), nameof(HouseManager.GetValidSpawnPointForBrain))]
    private static void AwayFromPlayers(HouseManager __instance, bool excludeRoomPLayerIsIn, ref Vector3 __result)
    {
        if (!excludeRoomPLayerIsIn || _rolling || !Hosting)
            return;
        _rolling = true;
        try
        {
            for (int i = 0; i < Tries && PlayerRooms.RemoteIn(PlayerRooms.Of(__result)); i++)
                __result = __instance.GetValidSpawnPointForBrain(excludeRoomPLayerIsIn: true);
        }
        finally
        {
            _rolling = false;
        }
    }

    // The thief comes in through a room nobody is in (the game only avoids its own player's room).
    [HarmonyPrefix]
    [HarmonyPatch(typeof(ThiefHandler), nameof(ThiefHandler.SpawnThief))]
    private static bool ThiefWhereNobodyIs()
    {
        if (!Hosting)
            return true;
        Free.Clear();
        foreach (var room in ThiefRooms)
        {
            if (!PlayerRooms.AnyoneIn(room))
                Free.Add(room);
        }
        if (Free.Count == 0)
            return true;
        var at = Free[Random.Range(0, Free.Count)];
        Object.Instantiate(BrainBank.brainsDict["Thief"].obj, Locator.GetRandomPositionInRoom(at) + Vector3.up * 1.9f, Quaternion.identity);
        ThiefHandler.BreakWindowInRoom(at);
        return false;
    }
}
