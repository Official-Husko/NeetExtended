using System.Collections.Generic;
using Coop.Net;
using Coop.World.Economy;
using Coop.World.Objects;
using Coop.World.Ownership;
using Coop.World.Spawning;
using Coop.World.State;
using LiteNetLib.Utils;
using UnityEngine;

namespace Coop.World.Manifest;

/// <summary>
/// Client: brings this house in line with the host's list.
/// - Missing things are made.
/// - Things the host no longer has go.
/// - Things made here while loading (in this player's id block) are announced to the host.
/// - Everything else takes the host's pose, owner and flags.
/// </summary>
internal static class ManifestReader
{
    private static readonly HashSet<long> Listed = new HashSet<long>();
    private static readonly List<NetObject> Extra = new List<NetObject>();

    public static void Init() => Messages.On(MessageIds.WorldManifest, OnManifest);

    private static void OnManifest(in Sender from, NetDataReader reader)
    {
        if (WorldSide.IsHost || !WorldSide.Running)
            return;
        int nextId = reader.GetInt();
        float money = reader.GetFloat();
        int count = reader.GetInt();
        int made = 0, missing = 0;
        Listed.Clear();
        for (int i = 0; i < count; i++)
        {
            var record = new SpawnRecord
            {
                Ref = NetRef.Read(reader),
                PrefabName = reader.GetString(),
                Owner = reader.GetByte(),
            };
            byte holder = reader.GetByte();
            record.Flags = reader.GetByte();
            record.Position = new Vector3(reader.GetFloat(), reader.GetFloat(), reader.GetFloat());
            record.Rotation = new Quaternion(reader.GetFloat(), reader.GetFloat(), reader.GetFloat(), reader.GetFloat());
            record.Scale = new Vector3(reader.GetFloat(), reader.GetFloat(), reader.GetFloat());
            Listed.Add(record.Ref.Key);

            var o = NetObjects.Find(record.Ref);
            if (o == null)
            {
                if (record.PrefabName.Length > 0 && SpawnApplier.Apply(record) != null)
                    made++;
                else
                    missing++;
                continue;
            }
            Align(o, record, holder);
        }
        int removed = RemoveExtras();
        int states = StateSync.ReadAll(reader);
        MoneySync.ApplyTotal(money);
        IdRanges.UseLocalBlock(nextId);
        CoopMod.Log.Info($"World: matched the host's {count} shared things: {made} made, {removed} removed, {missing} missing here; {states} states changed.");
    }

    private static void Align(NetObject o, in SpawnRecord record, byte holder)
    {
        if (o.Brain != null)
        {
            // NPCs: the pose stream places them; only snap the ones that aren't streaming yet.
            if (o.Rig == null || !o.Rig.Driving)
                ReplicaBody.Place(o, record.Position, record.Rotation);
            return;
        }
        if (record.Owner == WorldSide.LocalSlot)
        {
            // Still ours (held through a hot reload, say).
            o.Owner = record.Owner;
            o.Holder = holder;
            ReplicaBody.MakeOwned(o);
            return;
        }
        if (!o.IsReplica)
        {
            Owners.DropIfHeldHere(o);
            ReplicaBody.MakeReplica(o);
        }
        o.Owner = record.Owner;
        o.Holder = holder;
        o.GameKinematic = (record.Flags & BodyFlags.Kinematic) != 0;
        o.Buffer.Clear();
        Streaming.ReplicaDriver.Stop(o);
        bool active = (record.Flags & BodyFlags.Active) != 0;
        if (o.Go.activeSelf != active)
            o.Go.SetActive(active);
        o.Tf.localScale = record.Scale;
        ReplicaBody.Place(o, record.Position, record.Rotation);
    }

    private static int RemoveExtras()
    {
        Extra.Clear();
        foreach (var o in NetObjects.All)
        {
            if (o.Ref.IsDynamic && !Listed.Contains(o.Ref.Key))
                Extra.Add(o);
        }
        int removed = 0;
        foreach (var o in Extra)
        {
            if (IdRanges.IsLocal(o.Ref.Id) && o.IsSpawnable)
            {
                // Made here before the host knew us (the starting clothes of a new body).
                o.Owner = WorldSide.LocalSlot;
                ReplicaBody.MakeOwned(o);
                SpawnSender.Announce(o);
                continue;
            }
            SpawnApplier.Remove(o);
            removed++;
        }
        Extra.Clear();
        return removed;
    }
}
