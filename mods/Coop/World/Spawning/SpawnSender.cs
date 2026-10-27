using System.Collections.Generic;
using Coop.Net;
using Coop.World.Npcs;
using Coop.World.Objects;
using Coop.World.Ownership;
using Coop.World.State;
using Coop.World.Streaming;
using UnityEngine;

namespace Coop.World.Spawning;

/// <summary>
/// Notices things appearing and going away in this game and tells the others, batched once per frame
/// (late, so whatever made the thing has finished setting it up). Whoever spawns an item simulates it
/// at first; NPCs always run on the host, so a client hands its new ones over at once.
/// </summary>
internal static class SpawnSender
{
    private static readonly List<NetObject> Spawned = new List<NetObject>();
    private static readonly List<NetRef> Destroyed = new List<NetRef>();
    private static readonly List<Brain> StartedBrains = new List<Brain>();
    private static readonly HashSet<string> Unshareable = new HashSet<string>();

    public static void Clear()
    {
        Spawned.Clear();
        Destroyed.Clear();
        StartedBrains.Clear();
    }

    /// <summary>From Usable.Start.</summary>
    public static void OnUsableStarted(Usable usable)
    {
        if (!WorldSide.Running || SpawnContext.Silent || NetObjects.Find(usable.gameObject) != null)
            return;
        var o = Classifier.ForUsable(usable, scanning: false);
        if (o == null)
        {
            NoteUnshareable(usable);
            return;
        }
        o.Owner = WorldSide.LocalSlot;
        if (!Register(o))
            return;
        NetObjects.MarkMoving(o, Time.time);
        Spawned.Add(o);
    }

    /// <summary>From Brain.Start, which runs inside the subclass's Start: handled next frame, once it's done.</summary>
    public static void OnBrainStarted(Brain brain)
    {
        if (brain is BrainPlayer)
            return;
        if (WorldSide.IsClient)
            brain.enabled = false; // NPCs only think on the host.
        if (WorldSide.Running && !SpawnContext.Silent)
            StartedBrains.Add(brain);
    }

    public static void ProcessStartedBrains()
    {
        for (int i = 0; i < StartedBrains.Count; i++)
        {
            var brain = StartedBrains[i];
            if (brain == null || NetObjects.Find(brain.gameObject) != null)
                continue;
            var o = Classifier.ForBrain(brain);
            if (o == null)
                continue;
            o.Owner = WorldSide.HostSlot;
            if (!Register(o))
                continue;
            if (WorldSide.IsClient)
                NpcPuppets.Puppetize(o);
            Spawned.Add(o);
        }
        StartedBrains.Clear();
    }

    /// <summary>Announces something that exists here but not at the host (made here while loading or reloading).</summary>
    public static void Announce(NetObject o)
    {
        if (!Spawned.Contains(o))
            Spawned.Add(o);
    }

    /// <summary>From the destroy hooks.</summary>
    public static void OnDestroyed(GameObject go)
    {
        var o = NetObjects.Find(go);
        if (o == null)
            return;
        NetObjects.Remove(o);
        LoadedIds.Remove(o);
        StateTrackers.RemoveFor(o);
        ReplicaDriver.Stop(o);
        if (!WorldSide.Running || SpawnContext.Silent)
            return;
        if (Spawned.Remove(o))
            return; // Came and went within one frame: nobody needs to hear of it.
        Destroyed.Add(o.Ref);
    }

    public static void FlushSpawns()
    {
        if (Spawned.Count == 0)
            return;
        int count = 0;
        foreach (var o in Spawned)
        {
            if (o.Alive && o.Index >= 0)
                count++;
        }
        if (count > 0)
        {
            var w = Outbox.Begin(MessageIds.WorldSpawn);
            w.Put((ushort)count);
            foreach (var o in Spawned)
            {
                if (o.Alive && o.Index >= 0)
                    SpawnRecord.Capture(o).Write(w);
            }
            Outbox.ToPlayers(w, Channel.Events);
        }
        Spawned.Clear();
    }

    public static void FlushDestroys()
    {
        if (Destroyed.Count == 0)
            return;
        var w = Outbox.Begin(MessageIds.WorldDestroy);
        w.Put((ushort)Destroyed.Count);
        foreach (var r in Destroyed)
            r.Write(w);
        Outbox.ToPlayers(w, Channel.Events);
        Destroyed.Clear();
    }

    private static bool Register(NetObject o)
    {
        if (!NetObjects.Add(o))
        {
            CoopMod.Log.Debug($"World: {o.Go.name} turned up with an id that's taken ({o.Ref}); it stays local.");
            return false;
        }
        LoadedIds.Add(o);
        StateTrackers.AddFor(o, fresh: true);
        return true;
    }

    // Things with a body but no name to spawn them by can't be made elsewhere. Counted once per kind.
    private static void NoteUnshareable(Usable usable)
    {
        if (usable.lever || usable.button || usable.GetComponent<Rigidbody>() == null || !string.IsNullOrEmpty(usable.prefabName))
            return;
        // Pee, puke and blood are made on every machine on their own.
        if (usable.GetComponentInParent<Brain>(true) != null || usable.GetComponent<SpraySphere>() != null)
            return;
        string name = usable.name.Replace("(Clone)", "").Trim();
        if (Unshareable.Add(name))
            CoopMod.Log.Info($"World: {name} appeared here, but it has no item name, so the others won't see it.");
    }
}
