using System;
using System.Diagnostics;
using Coop.Net;
using Coop.Players;
using Coop.Session;
using NeetModKit.Saves;

namespace Coop.Join;

/// <summary>Host: sends a joining player the house as it is right now.</summary>
internal static class SnapshotSender
{
    public static void Init() => CoopSession.HouseWanted += Send;

    private static void Send(CoopPlayer player)
    {
        try
        {
            var timer = Stopwatch.StartNew();
            var snapshot = SaveCapture.Take();
            SnapshotSanitizer.ForJoiner(snapshot.Save, BodyBook.Find(player.Id));
            byte[] data = snapshot.Encode();

            var message = Outbox.Begin(MessageIds.Snapshot);
            // LiteNetLib's own length prefix is 16 bits; a house is bigger than that.
            message.Put(data.Length);
            message.Put(data);
            Outbox.ToSlot(player.Slot, message, Channel.Bulk);
            CoopMod.Log.Info($"Sent the house to {player}: {data.Length / 1024} KB, {snapshot.Paint.Count} painted surfaces, {timer.ElapsedMilliseconds} ms.");
        }
        catch (Exception e)
        {
            CoopMod.Log.Error($"Couldn't send the house to {player}: {e}");
            CoopSession.Transport.Kick(player.Peer, "The host couldn't send their house. Their log has the details.");
        }
    }
}
