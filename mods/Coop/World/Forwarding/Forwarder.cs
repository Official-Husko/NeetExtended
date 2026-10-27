using Coop.Net;
using Coop.Players;
using Coop.Session;
using Coop.World.Objects;
using LiteNetLib.Utils;
using UnityEngine;

namespace Coop.World.Forwarding;

/// <summary>
/// Hits that land where the hurt one isn't simulated go to the game that simulates them:
/// - a remote player's body here: to that player's game, which applies it to its own player through
///   the game's stats (HUD, knockout and death as in single player);
/// - an NPC's copy on a client: to the host, which applies it to the real NPC.
///
/// Burns aren't passed on: every game has the fire's flames and burns its own player from them.
/// Things that hit by touch (knives, thrown things, traps) only act where they're simulated, so
/// nothing is hurt twice.
///
/// WorldForward: target slot, kind, then
/// - Damage: position, amount, damage kind, force (optional);
/// - Blast: centre, radius (an explosion's push);
/// - NpcDamage (to the host): NPC ref, position, amount, damage kind, force (optional);
/// - Possess: the skeleton parasite that got the player (its mask goes on their face).
/// </summary>
internal static class Forwarder
{
    private const byte Damage = 0;
    private const byte Blast = 1;
    private const byte NpcDamage = 2;
    private const byte Possess = 3;

    // What the game's explosions push bodies with (Commons.ExplosionDamage).
    private const float BlastForce = 100f;
    private const float BlastLift = 0.3f;

    public static void Init() => Messages.On(MessageIds.WorldForward, OnForward);

    public static void HitPlayer(byte slot, Vector3 position, float amount, EffectType kind, Vector3? force)
    {
        if (!WorldSide.Running || kind == EffectType.Burn || amount == 0f)
            return;
        var w = Begin(slot, Damage);
        PutHit(w, position, amount, kind, force);
        Send(slot, w);
    }

    public static void HitNpc(NetRef npc, Vector3 position, float amount, EffectType kind, Vector3? force)
    {
        if (!WorldSide.Running || !WorldSide.IsClient || kind == EffectType.Burn || amount == 0f)
            return;
        var w = Begin(WorldSide.HostSlot, NpcDamage);
        npc.Write(w);
        PutHit(w, position, amount, kind, force);
        Send(WorldSide.HostSlot, w);
    }

    /// <summary>Host: a skeleton parasite reached another player; they're knocked out and get its mask.</summary>
    public static void Possessed(byte slot, NetRef parasite)
    {
        if (!WorldSide.Running || !WorldSide.IsHost)
            return;
        var w = Begin(slot, Possess);
        parasite.Write(w);
        Send(slot, w);
    }

    /// <summary>After an explosion here: other players' bodies in reach get its push at home.</summary>
    public static void Exploded(Vector3 centre, float radius)
    {
        if (!WorldSide.Running)
            return;
        foreach (var player in CoopSession.Players)
        {
            if (player.IsLocal || !player.InWorld)
                continue;
            var body = RemotePlayers.Find(player.Slot);
            if (body == null || (body.transform.position - centre).sqrMagnitude > radius * radius)
                continue;
            var w = Begin(player.Slot, Blast);
            Put(w, centre);
            w.Put(radius);
            Send(player.Slot, w);
        }
    }

    private static NetDataWriter Begin(byte target, byte kind)
    {
        var w = Outbox.Begin(MessageIds.WorldForward);
        w.Put(target);
        w.Put(kind);
        return w;
    }

    private static void Send(byte target, NetDataWriter w)
    {
        if (WorldSide.IsHost)
            Outbox.ToSlot(target, w, Channel.Events);
        else
            Outbox.ToHost(w, Channel.Events);
    }

    private static void OnForward(in Sender from, NetDataReader reader)
    {
        int start = reader.Position;
        int length = reader.AvailableBytes;
        byte target = reader.GetByte();
        if (WorldSide.IsHost && target != WorldSide.HostSlot)
        {
            // Client to client goes through the host.
            var relay = Outbox.Begin(MessageIds.WorldForward);
            relay.Put(reader.RawData, start, length);
            Outbox.ToSlot(target, relay, Channel.Events);
            return;
        }
        if (!WorldSide.Running || target != WorldSide.LocalSlot)
            return;
        switch (reader.GetByte())
        {
            case Damage:
                TakeHit(reader);
                break;
            case Blast:
                TakeBlast(Get(reader), reader.GetFloat());
                break;
            case NpcDamage:
                HurtNpc(NetObjects.Find(NetRef.Read(reader)), reader);
                break;
            case Possess:
                BePossessed(NetObjects.Find(NetRef.Read(reader)));
                break;
        }
    }

    private static void TakeHit(NetDataReader reader)
    {
        ReadHit(reader, out var position, out float amount, out var kind, out var force);
        var player = WorldSide.LocalPlayer;
        if (player != null && player.TryGetComponent(out PlayerStats stats) && stats.health != null)
            stats.health.Affect(position, amount, kind, force);
    }

    private static void TakeBlast(Vector3 centre, float radius)
    {
        var player = WorldSide.LocalPlayer;
        if (player != null && player.TryGetComponent(out Rigidbody body))
            body.AddExplosionForce(BlastForce, centre, radius, BlastLift, ForceMode.Impulse);
    }

    // What the parasite's own possession does to the host's player: knocked out, a worn mask off,
    // its skull mask on (the mask is a new item made here, so it's this player's from the start).
    private static void BePossessed(NetObject parasite)
    {
        var player = WorldSide.LocalPlayer;
        var mask = parasite != null && parasite.Alive && parasite.Go.TryGetComponent(out SkeletonParasite skull) ? skull.mask : null;
        if (player == null || !player.TryGetComponent(out PlayerStats stats) || !player.TryGetComponent(out Player_Abilities hands))
            return;
        stats.knockout.SetVal(10f);
        foreach (var worn in hands.masks)
        {
            if (worn != null && worn.TryGetComponent(out MaskFace face) && !face.stackable)
            {
                face.RemoveMask(player);
                break;
            }
        }
        if (mask != null)
            Object.Instantiate(mask, Vector3.up * 100f, Quaternion.identity).GetComponent<MaskFace>()?.PutOnMask(player, notHeld: true);
    }

    private static void HurtNpc(NetObject npc, NetDataReader reader)
    {
        ReadHit(reader, out var position, out float amount, out var kind, out var force);
        if (npc == null || !npc.Alive || !npc.Go.TryGetComponent(out ActorStats actor) || !ActorStats.canDamageOther(actor))
            return;
        actor.stats[typeof(Health)].Affect(position, amount, kind, force);
    }

    private static void PutHit(NetDataWriter w, Vector3 position, float amount, EffectType kind, Vector3? force)
    {
        Put(w, position);
        w.Put(amount);
        w.Put((byte)kind);
        w.Put(force.HasValue);
        if (force.HasValue)
            Put(w, force.Value);
    }

    private static void ReadHit(NetDataReader r, out Vector3 position, out float amount, out EffectType kind, out Vector3? force)
    {
        position = Get(r);
        amount = r.GetFloat();
        kind = (EffectType)r.GetByte();
        force = r.GetBool() ? Get(r) : (Vector3?)null;
    }

    private static void Put(NetDataWriter w, Vector3 v)
    {
        w.Put(v.x);
        w.Put(v.y);
        w.Put(v.z);
    }

    private static Vector3 Get(NetDataReader r) => new Vector3(r.GetFloat(), r.GetFloat(), r.GetFloat());
}
