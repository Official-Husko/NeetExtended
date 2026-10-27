using System.Collections.Generic;
using Coop.Net;
using LiteNetLib.Utils;

namespace Coop.Session;

/// <summary>
/// Getting into a session and keeping the player list right:
/// client connects, sends Hello; host gives it a slot (Welcome) and sends its mod setup and the
/// files in the game; once the client has them it asks for the house (SendHouse), loads it and
/// sends Ready; the host tells everyone (PlayerJoined). Leaving goes out as PlayerLeft.
/// </summary>
internal static class Handshake
{
    public static void Init()
    {
        Messages.On(MessageIds.Hello, OnHello);
        Messages.On(MessageIds.Welcome, OnWelcome);
        Messages.On(MessageIds.PlayerJoined, OnPlayerJoined);
        Messages.On(MessageIds.PlayerLeft, OnPlayerLeft);
        Messages.On(MessageIds.Ready, OnReady);
        Messages.On(MessageIds.SendHouse, OnSendHouse);
        Messages.On(MessageIds.Resync, OnResync);
    }

    /// <summary>Listens to the transport the session uses now.</summary>
    public static void Attach(ITransport transport)
    {
        transport.Connected += OnConnected;
        transport.Disconnected += OnDisconnected;
        transport.Received += OnReceived;
    }

    public static void Detach(ITransport transport)
    {
        transport.Connected -= OnConnected;
        transport.Disconnected -= OnDisconnected;
        transport.Received -= OnReceived;
    }

    /// <summary>Client: the host's world is loaded and running here.</summary>
    public static void SendReady()
    {
        if (!CoopSession.IsClient || CoopSession.Local == null)
            return;
        CoopSession.Local.InWorld = true;
        CoopSession.SetState(SessionState.Playing);
        Outbox.ToHost(Outbox.Begin(MessageIds.Ready), Channel.Events);
        CoopSession.RaiseWorldReady();
    }

    /// <summary>Client: has the mods and files; the house can come.</summary>
    public static void AskForHouse()
    {
        if (CoopSession.State == SessionState.Joining)
            Outbox.ToHost(Outbox.Begin(MessageIds.SendHouse), Channel.Events);
    }

    /// <summary>After a hot reload: the host sends everything again (client), or gets sent again (host).</summary>
    public static void Resync()
    {
        if (CoopSession.IsClient && CoopSession.State == SessionState.Playing)
        {
            Outbox.ToHost(Outbox.Begin(MessageIds.Resync), Channel.Events);
            return;
        }
        if (!CoopSession.IsHost)
            return;
        foreach (var player in CoopSession.Players)
        {
            if (!player.IsLocal && player.InWorld)
                CoopSession.RaiseReady(player);
        }
    }

    private static void OnReceived(int peer, NetDataReader reader, Channel channel)
    {
        byte slot;
        if (CoopSession.IsClient)
        {
            slot = CoopPlayer.HostSlot;
        }
        else
        {
            var player = CoopSession.FindPeer(peer);
            // Until it has said hello, a connection may only say hello.
            if (player == null && (reader.AvailableBytes < 2 || reader.PeekUShort() != MessageIds.Hello))
                return;
            slot = player?.Slot ?? Outbox.NoSlot;
        }
        Messages.Dispatch(new Sender(peer, slot), reader);
    }

    private static void OnConnected(int peer)
    {
        if (!CoopSession.IsClient)
            return;
        CoopSession.HostPeer = peer;
        var config = CoopMod.Config.Value;
        var hello = Outbox.Begin(MessageIds.Hello);
        hello.Put(Protocol.Version);
        hello.Put(CoopMod.Version);
        hello.Put(config.PlayerId);
        hello.Put(CoopMod.PlayerName);
        var mods = ModList.Available();
        hello.Put((ushort)mods.Count);
        foreach (string mod in mods)
            hello.Put(mod);
        Outbox.ToHost(hello, Channel.Events);
    }

    private static void OnDisconnected(int peer, string reason)
    {
        if (CoopSession.IsClient)
        {
            CoopSession.Transport.Stop();
            CoopSession.End(reason);
            return;
        }
        var player = CoopSession.FindPeer(peer);
        if (player == null)
            return;
        CoopMod.Log.Info($"{player} left: {reason}");
        CoopSession.Remove(player.Slot);
        var left = Outbox.Begin(MessageIds.PlayerLeft);
        left.Put(player.Slot);
        Outbox.ToPlayers(left, Channel.Events);
    }

    private static void OnHello(in Sender from, NetDataReader reader)
    {
        if (!CoopSession.IsHost)
            return;
        int protocol = reader.GetInt();
        string version = reader.GetString();
        string id = reader.GetString();
        string name = reader.GetString();
        var mods = new List<string>(reader.GetUShort());
        for (int i = mods.Capacity; i > 0; i--)
            mods.Add(reader.GetString());

        if (protocol != Protocol.Version || version != CoopMod.Version)
        {
            CoopSession.Transport.Kick(from.Peer, $"Different Co-op versions: the host has {CoopMod.Version}, you have {version}.");
            return;
        }
        string mismatch = ModList.Mismatch(mods);
        if (mismatch != null)
        {
            CoopMod.Log.Info($"Turned away {name}: {mismatch}");
            CoopSession.Transport.Kick(from.Peer, mismatch);
            return;
        }
        byte slot = FreeSlot();
        if (slot == Outbox.NoSlot)
        {
            CoopSession.Transport.Kick(from.Peer, "The game is full.");
            return;
        }

        // Someone coming back before their old connection timed out replaces it.
        foreach (var stale in CoopSession.Players)
        {
            if (!stale.IsLocal && stale.Id == id && stale.Peer != from.Peer)
            {
                CoopSession.Transport.Kick(stale.Peer, "You joined again from somewhere else.");
                CoopSession.Remove(stale.Slot);
                break;
            }
        }

        var player = CoopSession.Add(new CoopPlayer { Slot = slot, Peer = from.Peer, Name = Clean(name, slot), Id = id });
        CoopMod.Log.Info($"{player} is joining.");

        var welcome = Outbox.Begin(MessageIds.Welcome);
        welcome.Put(slot);
        welcome.Put((byte)CoopSession.Players.Count);
        foreach (var p in CoopSession.Players)
        {
            welcome.Put(p.Slot);
            welcome.Put(p.Name);
            welcome.Put(p.Id);
            welcome.Put(p.InWorld);
        }
        Outbox.ToSlot(slot, welcome, Channel.Events);
        CoopSession.RaiseArrived(player);
    }

    private static void OnWelcome(in Sender from, NetDataReader reader)
    {
        if (CoopSession.State != SessionState.Connecting)
            return;
        byte mySlot = reader.GetByte();
        int count = reader.GetByte();
        for (int i = 0; i < count; i++)
        {
            var player = new CoopPlayer
            {
                Slot = reader.GetByte(),
                Name = reader.GetString(),
                Id = reader.GetString(),
                InWorld = reader.GetBool(),
            };
            if (player.Slot == mySlot)
            {
                player.IsLocal = true;
                player.InWorld = false;
                CoopSession.SetLocal(player);
            }
            else if (player.IsHost)
            {
                player.Peer = from.Peer;
            }
            CoopSession.Add(player);
        }
        CoopSession.SetState(SessionState.Joining);
        CoopMod.Log.Info($"Joined as {CoopSession.Local}. Getting the host's world.");
        CoopSession.RaiseWelcomed();
    }

    private static void OnReady(in Sender from, NetDataReader reader)
    {
        var player = CoopSession.FindSlot(from.Slot);
        if (!CoopSession.IsHost || player == null)
            return;
        player.InWorld = true;
        CoopMod.Log.Info($"{player} is in the house.");

        var joined = Outbox.Begin(MessageIds.PlayerJoined);
        joined.Put(player.Slot);
        joined.Put(player.Name);
        joined.Put(player.Id);
        Outbox.ToPlayers(joined, Channel.Events, exceptSlot: player.Slot);
        CoopSession.RaiseReady(player);
    }

    private static void OnSendHouse(in Sender from, NetDataReader reader)
    {
        var player = CoopSession.FindSlot(from.Slot);
        if (CoopSession.IsHost && player != null && !player.InWorld)
            CoopSession.RaiseHouseWanted(player);
    }

    private static void OnResync(in Sender from, NetDataReader reader)
    {
        var player = CoopSession.FindSlot(from.Slot);
        if (CoopSession.IsHost && player != null && player.InWorld)
            CoopSession.RaiseReady(player);
    }

    private static void OnPlayerJoined(in Sender from, NetDataReader reader)
    {
        if (!CoopSession.IsClient)
            return;
        var player = CoopSession.Add(new CoopPlayer
        {
            Slot = reader.GetByte(),
            Name = reader.GetString(),
            Id = reader.GetString(),
            InWorld = true,
        });
        if (CoopSession.InWorld)
            CoopSession.RaiseReady(player);
    }

    private static void OnPlayerLeft(in Sender from, NetDataReader reader)
    {
        if (CoopSession.IsClient)
            CoopSession.Remove(reader.GetByte());
    }

    private static byte FreeSlot()
    {
        int max = CoopMod.Config.Value.MaxPlayers;
        for (byte slot = 1; slot < max; slot++)
        {
            if (CoopSession.FindSlot(slot) == null)
                return slot;
        }
        return Outbox.NoSlot;
    }

    private static string Clean(string name, byte slot)
    {
        name = (name ?? "").Trim();
        if (name.Length > 24)
            name = name.Substring(0, 24);
        return name.Length > 0 ? name : "Player " + (slot + 1);
    }
}
