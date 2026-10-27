using Coop.Net;
using Coop.Players;
using Coop.Session;
using Coop.World.Objects;
using LiteNetLib.Utils;
using NeetModKit.Rules;
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
/// - Possess: the skeleton parasite that got the player (its mask goes on their face);
/// - Kill, Skeletonize, Knockout (seconds): the story did it to the player it was talking to;
/// - Stat: name, set (else add), value: a rule changed a stat of the player it was about;
/// - Disarm: hand: a cop knocked what's in it out of the player's hand;
/// - Move, Gib: see <see cref="BodyMoves"/>.
/// </summary>
internal static class Forwarder
{
    private const byte Damage = 0;
    private const byte Blast = 1;
    private const byte NpcDamage = 2;
    private const byte Possess = 3;
    private const byte Kill = 4;
    private const byte Skeletonize = 5;
    private const byte Knockout = 6;
    private const byte Stat = 7;
    private const byte Disarm = 8;
    internal const byte Move = 9;
    internal const byte Gib = 10;

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

    /// <summary>Host: a conversation's KillPlayer, SkeletonizePlayer or KnockoutPlayer, for the player it was with.</summary>
    public static void StoryKilled(byte slot) => SendStory(slot, Kill, 0f);

    public static void StorySkeletonized(byte slot) => SendStory(slot, Skeletonize, 0f);

    public static void StoryKnockedOut(byte slot, float seconds) => SendStory(slot, Knockout, seconds);

    /// <summary>Host: a rule in a remote player's conversation set or changed one of their stats.</summary>
    public static void StoryStat(byte slot, string name, bool set, float value)
    {
        if (!WorldSide.Running || !WorldSide.IsHost)
            return;
        var w = Begin(slot, Stat);
        w.Put(name);
        w.Put(set);
        w.Put(value);
        Send(slot, w);
    }

    private static void SendStory(byte slot, byte kind, float amount)
    {
        if (!WorldSide.Running || !WorldSide.IsHost)
            return;
        var w = Begin(slot, kind);
        w.Put(amount);
        Send(slot, w);
    }

    /// <summary>Host: a cop knocked something out of a remote player's hand (what it does to the host's player).</summary>
    public static void Disarmed(byte slot, int hand)
    {
        if (!WorldSide.Running || !WorldSide.IsHost)
            return;
        var w = Begin(slot, Disarm);
        w.Put((byte)hand);
        Send(slot, w);
    }

    /// <summary>After an explosion here: other players' bodies in reach get its push at home.</summary>
    public static void Exploded(Vector3 centre, float radius, float obliterateRadius)
    {
        if (!WorldSide.Running)
            return;
        foreach (var player in CoopSession.Players)
        {
            if (player.IsLocal || !player.InWorld)
                continue;
            var body = RemotePlayers.Find(player.Slot);
            float sqrDistance = body != null ? (body.transform.position - centre).sqrMagnitude : float.MaxValue;
            if (sqrDistance > radius * radius)
                continue;
            // Close enough to be gibbed, as the game's explosions do to anything that can be.
            if (obliterateRadius > 0f && sqrDistance < obliterateRadius * obliterateRadius)
                BodyMoves.Gibbed(player.Slot);
            var w = Begin(player.Slot, Blast);
            Put(w, centre);
            w.Put(radius);
            Send(player.Slot, w);
        }
    }

    internal static NetDataWriter Begin(byte target, byte kind)
    {
        var w = Outbox.Begin(MessageIds.WorldForward);
        w.Put(target);
        w.Put(kind);
        return w;
    }

    internal static void Send(byte target, NetDataWriter w)
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
        byte kind = reader.GetByte();
        switch (kind)
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
            case Kill:
            case Skeletonize:
            case Knockout:
                FromStory(kind, reader.GetFloat());
                break;
            case Stat:
                ChangeStat(reader.GetString(), reader.GetBool(), reader.GetFloat());
                break;
            case Disarm:
                LoseHeld(reader.GetByte());
                break;
            case Move:
            case Gib:
                BodyMoves.Receive(kind, reader);
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

    private static void LoseHeld(int hand)
    {
        var player = WorldSide.LocalPlayer;
        if (player != null && player.TryGetComponent(out Player_Abilities hands) && hand < hands.pickups.Length && hands.pickups[hand] != null)
            hands.Drop((Hands)hand);
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

    private static void ChangeStat(string name, bool set, float value)
    {
        if (WorldSide.LocalPlayer == null)
            return;
        try
        {
            if (set)
                RulePlayer.Local.SetStat(name, value);
            else
                RulePlayer.Local.AddStat(name, value);
        }
        catch (System.ArgumentException e)
        {
            CoopMod.Log.Warn($"A conversation changed a stat this game doesn't have: {e.Message}");
        }
    }

    // What Dialogue's KillPlayer, SkeletonizePlayer and KnockoutPlayer do, to this game's player.
    private static void FromStory(byte kind, float amount)
    {
        var player = WorldSide.LocalPlayer;
        if (player == null || !player.TryGetComponent(out Player_Abilities hands))
            return;
        if (kind == Kill)
            hands.DieAccessor();
        else if (kind == Skeletonize)
            hands.SkeletonizeAccessor();
        else if (player.TryGetComponent(out PlayerStats stats) && stats.knockout != null)
            stats.knockout.AddVal(amount);
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

    internal static void Put(NetDataWriter w, Vector3 v)
    {
        w.Put(v.x);
        w.Put(v.y);
        w.Put(v.z);
    }

    internal static Vector3 Get(NetDataReader r) => new Vector3(r.GetFloat(), r.GetFloat(), r.GetFloat());
}
