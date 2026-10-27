using System.Collections.Generic;
using Coop.Net;
using Coop.Session;
using LiteNetLib.Utils;
using NeetModKit.Events;
using UnityEngine;

namespace Coop.Players;

/// <summary>
/// The other players' bodies in this game. A body appears with the first pose that arrives while
/// we're in the world and goes when the player leaves, the session ends or the mod unloads. The host
/// also passes each client's poses on to the other clients.
/// </summary>
internal static class RemotePlayers
{
    private static readonly Dictionary<byte, PlayerPuppet> Puppets = new Dictionary<byte, PlayerPuppet>();

    public static void Init()
    {
        Messages.On(MessageIds.Pose, OnPose);
        CoopSession.PlayerLeft += player => Remove(player.Slot);
        CoopSession.Ended += _ => Clear();
        CoopLoop.Unloading += Clear;
    }

    public static PlayerPuppet Find(byte slot) =>
        Puppets.TryGetValue(slot, out var puppet) && puppet != null ? puppet : null;

    private static void OnPose(in Sender from, NetDataReader reader)
    {
        var pose = PlayerPose.Read(reader, out byte slot);
        if (CoopSession.IsHost)
        {
            // A client only speaks for itself.
            slot = from.Slot;
            var relay = Outbox.Begin(MessageIds.Pose);
            pose.Write(relay, slot);
            Outbox.ToPlayers(relay, Channel.Stream, exceptSlot: slot);
        }

        if (!CoopSession.InWorld || !GameEvents.IsHouseReady || slot == CoopSession.Local?.Slot)
            return;
        RemoteClock.Observe(slot, pose.Time);
        var player = CoopSession.FindSlot(slot);
        if (player == null || !player.InWorld)
            return;

        var puppet = Find(slot);
        if (puppet == null)
        {
            puppet = PuppetBuilder.Build(slot, player.Name);
            if (puppet == null)
                return;
            Puppets[slot] = puppet;
            if (AppearanceSync.TryGet(slot, out var look))
                puppet.Dress(look);
            CoopMod.Log.Info($"{player} appeared in the house.");
        }
        puppet.Push(pose);
    }

    private static void Remove(byte slot)
    {
        if (Puppets.TryGetValue(slot, out var puppet))
        {
            if (puppet != null)
                Object.Destroy(puppet.gameObject);
            Puppets.Remove(slot);
        }
    }

    private static void Clear()
    {
        foreach (var puppet in Puppets.Values)
        {
            if (puppet != null)
                Object.Destroy(puppet.gameObject);
        }
        Puppets.Clear();
    }
}
