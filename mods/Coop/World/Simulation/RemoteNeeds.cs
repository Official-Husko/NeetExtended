using System;
using System.Collections.Generic;
using System.Reflection;
using Coop.Players;
using Coop.Session;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Coop.World.Simulation;

/// <summary>
/// Host: starving or sleepless players get visitors (skeletons for hunger, spiders for no sleep). The
/// game checks only its own player every 10 seconds; this does the same check for every other player,
/// with their hunger and energy from the body they last sent (see BodyBook), through the game's own
/// check so the odds, the timing and the house-wide limit of four stay the game's.
/// </summary>
internal static class RemoteNeeds
{
    private const float Interval = 10f;
    private const string Skeleton = "SkeletonNeetNPC";
    private const string Spider = "Spider_House";

    private static readonly MethodInfo StatCheck =
        AccessTools.Method(typeof(HouseManager), "StatCheck")?.MakeGenericMethod(typeof(Stat));

    private static readonly Dictionary<byte, int> FailedHunger = new Dictionary<byte, int>();
    private static readonly Dictionary<byte, int> FailedEnergy = new Dictionary<byte, int>();
    private static readonly object[] Args = new object[5];
    private static float _next;

    public static void Clear()
    {
        FailedHunger.Clear();
        FailedEnergy.Clear();
    }

    public static void HostTick()
    {
        float now = Time.time;
        var house = HouseManager._;
        if (now < _next || house == null || !house.performStatChecks || StatCheck == null)
            return;
        _next = now + Interval;
        foreach (var player in CoopSession.Players)
        {
            if (player.IsLocal || !player.InWorld || !Needs(player.Id, out float hunger, out float energy))
                continue;
            Check(house, hunger, FailedHunger, player.Slot, () => house.spawnedSkeletonCount, i => house.spawnedSkeletonCount += i, Skeleton);
            Check(house, energy, FailedEnergy, player.Slot, () => house.spawnedSpiderCount, i => house.spawnedSpiderCount += i, Spider);
        }
    }

    private static void Check(HouseManager house, float value, Dictionary<byte, int> failed, byte slot, Func<int> count, Action<int> add, string visitor)
    {
        failed.TryGetValue(slot, out int fails);
        Args[0] = new Stat(value);
        Args[1] = fails;
        Args[2] = count;
        Args[3] = add;
        Args[4] = visitor;
        try
        {
            StatCheck.Invoke(house, Args);
            failed[slot] = (int)Args[1];
        }
        catch (Exception e)
        {
            CoopMod.Log.Warn($"World: checking slot {slot}'s needs failed: {e.GetBaseException().Message}");
        }
    }

    // The body record is the game's own brain save; its PlayerStats part lists stat names and [last, now] values.
    private static bool Needs(string playerId, out float hunger, out float energy)
    {
        hunger = energy = 100f;
        string body = BodyBook.Find(playerId);
        if (string.IsNullOrEmpty(body))
            return false;
        try
        {
            var subs = JObject.Parse(body)["subSaveData"] as JArray;
            if (subs == null)
                return false;
            foreach (var sub in subs)
            {
                if (sub.Value<string>("type") != "PlayerStats")
                    continue;
                var names = sub["statTypes"] as JArray;
                var values = sub["statVals"] as JArray;
                if (names == null || values == null)
                    return false;
                bool found = false;
                for (int i = 0; i < names.Count && i < values.Count; i++)
                {
                    string name = names[i].Value<string>();
                    float now = values[i][1].Value<float>();
                    if (name == "Hunger") { hunger = now; found = true; }
                    else if (name == "Energy") { energy = now; found = true; }
                }
                return found;
            }
        }
        catch (Exception e)
        {
            CoopMod.Log.Debug($"World: couldn't read a player's needs: {e.Message}");
        }
        return false;
    }
}
