using System.Collections.Generic;
using Coop.Net;
using Coop.Session;
using LiteNetLib.Utils;
using UnityEngine;

namespace Coop.Players;

/// <summary>
/// Carrying a downed player: aim at them with an empty hand and tap its button (<see cref="Reviving"/>
/// tells a tap from a hold); press either hand again to put them down. The hand that picked them up
/// stays busy until then (<see cref="HandBusy"/>). The downed player's own game moves its body onto the carrier's shoulder (so every game sees
/// the same), and decides who carries them: one carrier at a time, told to everyone. The carrier's
/// game shows the body on its own shoulder straight away (<see cref="PlayerPuppet.Shoulder"/>). Being
/// revived, dying, the carrier going down or either player leaving ends it.
///
/// CarryRequest (carrier to the carried player's game): target, start. CarryState (the carried
/// player's game to everyone): target, carrier or none.
/// </summary>
internal static class Carrying
{
    private const byte None = byte.MaxValue;

    private static readonly Dictionary<byte, byte> CarriedBy = new Dictionary<byte, byte>();

    // This game's player carrying someone (or asking to).
    private static byte _carrying = None;
    private static int _hand = -1;

    // This game's player being carried.
    private static byte _carrier = None;
    private static Rigidbody _body;
    private static bool _wasKinematic;

    public static void Init()
    {
        Messages.On(MessageIds.CarryRequest, OnRequest);
        Messages.On(MessageIds.CarryState, OnState);
        CoopLoop.Update += Tick;
        CoopLoop.FixedUpdate += FixedTick;
        CoopSession.PlayerLeft += player => Forget(player.Slot);
        CoopSession.Ended += _ => Reset();
        CoopLoop.Unloading += Reset;
    }

    /// <summary>This game's player is being carried.</summary>
    public static bool LocalCarried => _carrier != None;

    /// <summary>The player this game's player carries (or has just asked to), or 255.</summary>
    public static byte Carried => _carrying;

    /// <summary>The hand holding a carried player can't pick up or punch.</summary>
    public static bool HandBusy(Hands hand) => _carrying != None && (int)hand == _hand;

    /// <summary>Who carries the player in <paramref name="slot"/>, or 255.</summary>
    public static byte CarrierOf(byte slot) => CarriedBy.TryGetValue(slot, out byte carrier) ? carrier : None;

    private static void Tick()
    {
        if (!CoopSession.InWorld || HouseManager._ == null || HouseManager._.player == null)
            return;
        CheckCarried();
        CheckCarrying();
        // Either hand button puts down who is carried.
        if (_carrying != None && HouseManager._.player.TryGetComponent(out BrainPlayer brain) && brain.intents != null
            && (brain.intents.LeftHand.Started || brain.intents.RightHand.Started))
            Ask(_carrying, false);
    }

    /// <summary>
    /// Pick up the downed player in <paramref name="slot"/> with <paramref name="hand"/> (an empty hand
    /// that tapped the button while looking at them).
    /// </summary>
    public static void PickUp(byte slot, int hand)
    {
        if (_carrying != None || Downed.LocalDown || !Downed.IsDown(slot) || CarrierOf(slot) != None)
            return;
        if (!HouseManager._.player.TryGetComponent(out Player_Abilities hands) || hands.pickups[hand] != null)
            return;
        _hand = hand;
        Ask(slot, true);
    }

    // Carrier: ask the carried player's game; show them on this shoulder straight away.
    private static void Ask(byte target, bool start)
    {
        var puppet = RemotePlayers.Find(target);
        if (start)
        {
            _carrying = target;
            puppet?.Carry(HouseManager._.player.transform);
        }
        else
        {
            StopCarrying();
        }
        var w = Outbox.Begin(MessageIds.CarryRequest);
        w.Put(target);
        w.Put(start);
        if (CoopSession.IsHost)
            Outbox.ToSlot(target, w, Channel.Events);
        else
            Outbox.ToHost(w, Channel.Events);
    }

    private static void StopCarrying()
    {
        if (_carrying != None)
            RemotePlayers.Find(_carrying)?.Carry(null);
        _carrying = None;
        _hand = -1;
    }

    // Carrier: the carried player's game said someone else carries them, or nobody; or they got up.
    private static void CheckCarrying()
    {
        if (_carrying == None)
            return;
        byte me = CoopSession.Local?.Slot ?? None;
        bool waiting = !CarriedBy.ContainsKey(_carrying);
        if (!Downed.IsDown(_carrying) || Downed.LocalDown || (!waiting && CarrierOf(_carrying) != me))
            StopCarrying();
    }

    // The carried player's game: a carrier asked to pick this player up or put them down.
    private static void OnRequest(in Sender from, NetDataReader reader)
    {
        byte target = reader.GetByte();
        bool start = reader.GetBool();
        byte me = CoopSession.Local?.Slot ?? None;
        if (target != me)
        {
            if (CoopSession.IsHost)
            {
                var relay = Outbox.Begin(MessageIds.CarryRequest);
                relay.Put(target);
                relay.Put(start);
                relay.Put(from.Slot);
                Outbox.ToSlot(target, relay, Channel.Events);
            }
            return;
        }
        // Relayed by the host: the carrier's slot follows; sent straight from the host: it's the host.
        byte carrier = reader.AvailableBytes > 0 ? reader.GetByte() : from.Slot;
        if (start && _carrier == None && Downed.LocalDown)
            BeCarried(carrier);
        else if (!start && _carrier == carrier)
            PutDown();
        else
            Announce(_carrier); // Tell the asker who carries this player, if anyone.
    }

    private static void BeCarried(byte carrier)
    {
        _carrier = carrier;
        _body = HouseManager._.player.GetComponent<Rigidbody>();
        if (_body != null)
        {
            _wasKinematic = _body.isKinematic;
            _body.isKinematic = true;
        }
        Announce(carrier);
        CoopMod.Log.Info("You're being carried.");
    }

    private static void PutDown()
    {
        if (_carrier == None)
            return;
        var carrier = RemotePlayers.Find(_carrier);
        _carrier = None;
        if (_body != null)
        {
            _body.isKinematic = _wasKinematic;
            _body.velocity = carrier != null ? carrier.transform.forward * 1.5f + Vector3.up : Vector3.up;
        }
        _body = null;
        Announce(None);
    }

    // The carried player's game: stop when they're up, dead, or the carrier is gone or down.
    private static void CheckCarried()
    {
        if (_carrier == None)
            return;
        if (!Downed.LocalDown || _body == null || HouseManager._.player.GetComponent<Rigidbody>() != _body
            || RemotePlayers.Find(_carrier) == null || Downed.IsDown(_carrier))
            PutDown();
    }

    // The carried player's game: the body rides on the carrier's shoulder as this game sees them.
    private static void FixedTick()
    {
        if (_carrier == None || _body == null)
            return;
        var carrier = RemotePlayers.Find(_carrier);
        if (carrier == null)
            return;
        _body.MovePosition(CarryPose.Position(carrier.transform));
        _body.MoveRotation(CarryPose.Rotation(carrier.transform));
    }

    private static void Announce(byte carrier)
    {
        byte me = CoopSession.Local?.Slot ?? None;
        Set(me, carrier);
        var w = Outbox.Begin(MessageIds.CarryState);
        w.Put(me);
        w.Put(carrier);
        if (CoopSession.IsHost)
            Outbox.ToPlayers(w, Channel.Events);
        else
            Outbox.ToHost(w, Channel.Events);
    }

    private static void OnState(in Sender from, NetDataReader reader)
    {
        byte target = reader.GetByte();
        byte carrier = reader.GetByte();
        if (CoopSession.IsHost)
        {
            target = from.Slot;
            var relay = Outbox.Begin(MessageIds.CarryState);
            relay.Put(target);
            relay.Put(carrier);
            Outbox.ToPlayers(relay, Channel.Events, exceptSlot: target);
        }
        Set(target, carrier);
    }

    private static void Set(byte target, byte carrier)
    {
        if (carrier == None)
            CarriedBy.Remove(target);
        else
            CarriedBy[target] = carrier;
    }

    private static void Forget(byte slot)
    {
        CarriedBy.Remove(slot);
        var stale = new List<byte>();
        foreach (var pair in CarriedBy)
        {
            if (pair.Value == slot)
                stale.Add(pair.Key);
        }
        foreach (byte target in stale)
            CarriedBy.Remove(target);
    }

    private static void Reset()
    {
        StopCarrying();
        if (_body != null)
            _body.isKinematic = _wasKinematic;
        _body = null;
        _carrier = None;
        CarriedBy.Clear();
    }
}
