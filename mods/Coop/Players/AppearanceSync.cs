using System.Collections.Generic;
using Coop.Net;
using Coop.Session;
using LiteNetLib.Utils;
using UnityEngine;

namespace Coop.Players;

/// <summary>
/// Keeps everyone's clothes and body shape up to date on the other bodies: each game sends its own
/// player's look when it changes and again whenever someone new is in the house; the host passes
/// clients' looks on to the others.
/// </summary>
internal static class AppearanceSync
{
    private const float CheckInterval = 0.5f;

    private static readonly Dictionary<byte, Appearance> Known = new Dictionary<byte, Appearance>();
    private static Appearance _sent;
    private static bool _hasSent;
    private static float _nextCheck;

    public static void Init()
    {
        Messages.On(MessageIds.Appearance, OnAppearance);
        CoopLoop.Update += Tick;
        CoopSession.PlayerReady += _ => _hasSent = false;
        CoopSession.WorldReady += () => _hasSent = false;
        CoopSession.Resumed += () => _hasSent = false;
        CoopSession.PlayerLeft += player => Known.Remove(player.Slot);
        CoopSession.Ended += _ => Known.Clear();
    }

    /// <summary>The last look we heard for a player, for a body built after it arrived.</summary>
    public static bool TryGet(byte slot, out Appearance look) => Known.TryGetValue(slot, out look);

    private static void Tick()
    {
        if (!CoopSession.InWorld || Time.time < _nextCheck)
            return;
        _nextCheck = Time.time + CheckInterval;
        RemotePlayers.Redress();
        var player = HouseManager._ != null ? HouseManager._.player : null;
        if (player == null || !player.TryGetComponent(out Player_Abilities abilities))
            return;

        var look = Appearance.Of(abilities);
        if (_hasSent && look.SameAs(_sent))
            return;
        _sent = look;
        _hasSent = true;
        var message = Outbox.Begin(MessageIds.Appearance);
        look.Write(message, CoopSession.Local.Slot);
        Outbox.ToPlayers(message, Channel.Events);
    }

    private static void OnAppearance(in Sender from, NetDataReader reader)
    {
        var look = Appearance.Read(reader, out byte slot);
        if (CoopSession.IsHost)
        {
            slot = from.Slot;
            var relay = Outbox.Begin(MessageIds.Appearance);
            look.Write(relay, slot);
            Outbox.ToPlayers(relay, Channel.Events, exceptSlot: slot);
        }
        if (slot == CoopSession.Local?.Slot)
            return;
        Known[slot] = look;
        RemotePlayers.Find(slot)?.Dress(look);
    }
}
