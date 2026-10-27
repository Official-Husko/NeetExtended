using System;
using System.Collections.Generic;
using Coop.Net;
using Coop.World.Npcs;
using Coop.World.Objects;
using Coop.World.Ownership;
using Coop.World.State;
using LiteNetLib.Utils;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Coop.World.Spawning;

/// <summary>
/// Makes what other games spawned and removes what they destroyed. Items are made the way the game
/// loads one from a save (prefab, id before Start, scale, component saves); NPCs from the brain bank,
/// and on a client they're turned into puppets. Everything here is quiet: no echo goes back out.
/// </summary>
internal static class SpawnApplier
{
    private static readonly HashSet<string> Missing = new HashSet<string>();

    public static void Init()
    {
        Messages.On(MessageIds.WorldSpawn, OnSpawn);
        Messages.On(MessageIds.WorldDestroy, OnDestroy);
    }

    private static void OnSpawn(in Sender from, NetDataReader reader)
    {
        int start = reader.Position;
        int length = reader.AvailableBytes;
        if (WorldSide.IsHost)
            Relay.Forward(MessageIds.WorldSpawn, reader.RawData, start, length, Channel.Events, from.Slot);
        if (!WorldSide.Running)
            return;
        int count = reader.GetUShort();
        for (int i = 0; i < count; i++)
        {
            var record = SpawnRecord.Read(reader);
            try
            {
                Apply(record);
            }
            catch (Exception e)
            {
                CoopMod.Log.Error($"World: couldn't make {record.PrefabName} ({record.Ref}): {e}");
            }
        }
    }

    private static void OnDestroy(in Sender from, NetDataReader reader)
    {
        int start = reader.Position;
        int length = reader.AvailableBytes;
        if (WorldSide.IsHost)
            Relay.Forward(MessageIds.WorldDestroy, reader.RawData, start, length, Channel.Events, from.Slot);
        if (!WorldSide.Running)
            return;
        int count = reader.GetUShort();
        for (int i = 0; i < count; i++)
            Remove(NetObjects.Find(NetRef.Read(reader)));
    }

    /// <summary>Removes something here because it's gone elsewhere (or never existed there).</summary>
    public static void Remove(NetObject o)
    {
        if (o == null)
            return;
        NetObjects.Remove(o);
        LoadedIds.Remove(o);
        StateTrackers.RemoveFor(o);
        if (o.Alive)
            Object.Destroy(o.Go);
    }

    public static NetObject Apply(in SpawnRecord record)
    {
        var existing = NetObjects.Find(record.Ref);
        if (existing != null)
            return existing;
        var o = record.Ref.Kind switch
        {
            NetKind.Item => MakeItem(record),
            NetKind.Npc => MakeNpc(record),
            _ => null,
        };
        if (o == null)
            return null;

        o.Owner = record.Owner;
        if (!NetObjects.Add(o))
        {
            Object.Destroy(o.Go);
            return null;
        }
        LoadedIds.Add(o);
        StateTrackers.AddFor(o);
        if (o.Brain != null && WorldSide.IsClient)
        {
            NpcPuppets.Puppetize(o);
        }
        else if (o.Owner != WorldSide.LocalSlot)
        {
            ReplicaBody.MakeReplica(o);
            o.GameKinematic = (record.Flags & BodyFlags.Kinematic) != 0;
        }
        return o;
    }

    private static NetObject MakeItem(in SpawnRecord record)
    {
        if (!PickupBank.pickupsDict.TryGetValue(record.PrefabName, out var spawnable) || spawnable.obj == null)
        {
            NoteMissing("item", record.PrefabName);
            return null;
        }
        using (SpawnContext.Applying())
        {
            var go = Object.Instantiate(spawnable.obj, record.Position, record.Rotation);
            var usable = go.GetComponentInChildren<Usable>(true);
            if (usable == null)
            {
                Object.Destroy(go);
                return null;
            }
            usable.saveId = record.Ref.Id;
            usable.Start();
            go.transform.localScale = record.Scale;
            LoadSubSaves(go, record.SubSaves);
            go.SetActive((record.Flags & BodyFlags.Active) != 0);
            return Classifier.ForUsable(usable, scanning: false);
        }
    }

    private static NetObject MakeNpc(in SpawnRecord record)
    {
        if (!BrainBank.brainsDict.TryGetValue(record.PrefabName, out var spawnable) || spawnable.obj == null)
        {
            NoteMissing("NPC", record.PrefabName);
            return null;
        }
        using (SpawnContext.Applying())
        {
            var go = Object.Instantiate(spawnable.obj, record.Position, record.Rotation);
            var brain = go.GetComponent<Brain>();
            if (brain == null)
            {
                Object.Destroy(go);
                return null;
            }
            brain.saveId = record.Ref.Id;
            go.transform.localScale = record.Scale;
            if (WorldSide.IsClient)
                NpcPuppets.BeforeStart(go);
            brain.Start();
            if (WorldSide.IsClient)
                brain.enabled = false;
            go.SetActive((record.Flags & BodyFlags.Active) != 0);
            return Classifier.ForBrain(brain);
        }
    }

    // The same matching the game does when it loads an item: each saved part goes to the component of that type.
    private static void LoadSubSaves(GameObject go, string json)
    {
        if (string.IsNullOrEmpty(json))
            return;
        var parts = go.GetComponentsInChildren<ISaveableObj>(true);
        foreach (var token in JArray.Parse(json))
        {
            if (!(token is JObject part))
                continue;
            string type = part.Value<string>("type");
            foreach (var component in parts)
            {
                if (component.GetType().ToString() != type)
                    continue;
                try
                {
                    component.Load(part);
                }
                catch (Exception e)
                {
                    CoopMod.Log.Debug($"World: loading {type} on {go.name} failed: {e.Message}");
                }
            }
        }
    }

    private static void NoteMissing(string kind, string name)
    {
        if (Missing.Add(name))
            CoopMod.Log.Warn($"World: the other game spawned the {kind} \"{name}\", which doesn't exist here.");
    }
}
