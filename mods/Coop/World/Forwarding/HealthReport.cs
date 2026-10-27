using Coop.Net;
using Coop.Players;
using LiteNetLib.Utils;
using UnityEngine;

namespace Coop.World.Forwarding;

/// <summary>
/// Each game reports its own player's health (on change, at most twice a second, and every few
/// seconds anyway) so the stand-ins on the others show it: NPCs and cops stop going after someone who
/// is down, as they do with the host's own player.
///
/// WorldHealth: slot, health.
/// </summary>
internal static class HealthReport
{
    private const float Interval = 0.5f;
    private const float Refresh = 5f;

    private static float _next;
    private static float _sentAt;
    private static float _sent = float.NaN;

    public static void Init() => Messages.On(MessageIds.WorldHealth, OnHealth);

    public static void Clear() => _sent = float.NaN;

    public static void Tick()
    {
        float now = Time.time;
        if (now < _next)
            return;
        _next = now + Interval;
        var player = WorldSide.LocalPlayer;
        if (player == null || !player.TryGetComponent(out PlayerStats stats) || stats.health == null)
            return;
        float health = stats.health.GetVal();
        if (Mathf.Abs(health - _sent) < 1f && now - _sentAt < Refresh)
            return;
        _sent = health;
        _sentAt = now;
        var w = Outbox.Begin(MessageIds.WorldHealth);
        w.Put(WorldSide.LocalSlot);
        w.Put(health);
        Outbox.ToPlayers(w, Channel.Events);
    }

    private static void OnHealth(in Sender from, NetDataReader reader)
    {
        int start = reader.Position;
        int length = reader.AvailableBytes;
        byte slot = reader.GetByte();
        float health = reader.GetFloat();
        if (WorldSide.IsHost)
        {
            if (slot != from.Slot)
                return;
            Relay.Forward(MessageIds.WorldHealth, reader.RawData, start, length, Channel.Events, from.Slot);
        }
        var body = RemotePlayers.Find(slot);
        if (body != null && body.TryGetComponent(out RemoteActor actor))
            actor.ShowHealth(health);
    }
}
