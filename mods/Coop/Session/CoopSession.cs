using System;
using System.Collections.Generic;
using Coop.Net;
using NeetModKit.Events;

namespace Coop.Session;

public enum SessionState
{
    Offline,

    /// <summary>Client: waiting for the host to say hello back.</summary>
    Connecting,

    /// <summary>Client: getting and loading the host's world.</summary>
    Joining,

    /// <summary>Client: in the host's world.</summary>
    Playing,

    /// <summary>Host: the house is open to others.</summary>
    Hosting,
}

/// <summary>
/// Who's playing and in what role. The handshake (<see cref="Handshake"/>) moves the state along;
/// everything else listens to the events here. All of it runs on the main thread.
/// </summary>
public static class CoopSession
{
    private static readonly List<CoopPlayer> PlayerList = new List<CoopPlayer>();

    public static SessionState State { get; private set; }
    public static bool IsActive => State != SessionState.Offline;
    public static bool IsHost => State == SessionState.Hosting;
    public static bool IsClient => State == SessionState.Connecting || State == SessionState.Joining || State == SessionState.Playing;

    /// <summary>This game is part of a shared world right now: the host, or a client that finished loading.</summary>
    public static bool InWorld => State == SessionState.Hosting || State == SessionState.Playing;

    public static IReadOnlyList<CoopPlayer> Players => PlayerList;
    public static CoopPlayer Local { get; private set; }

    /// <summary>Client: the host's peer; -1 otherwise.</summary>
    public static int HostPeer { get; internal set; } = -1;

    /// <summary>Why the last session ended, for the menu.</summary>
    public static string LastEndReason { get; private set; } = "";

    /// <summary>True while the session ends and its players are taken out (their PlayerLeft isn't news).</summary>
    public static bool IsEnding { get; private set; }

    /// <summary>The transport this session uses (the last one used when offline).</summary>
    internal static ITransport Transport { get; private set; }

    /// <summary>Which <see cref="TransportOption"/> it came from, kept across hot reloads.</summary>
    public static string TransportId { get; private set; } = Transports.DirectId;

    public static event Action Started;
    public static event Action<string> Ended;

    /// <summary>Host: a player said hello and was given a slot. Their mods and files come first, then the house.</summary>
    public static event Action<CoopPlayer> PlayerArrived;

    /// <summary>Host: a joining player has what it needs (mods, files) and wants the house now.</summary>
    public static event Action<CoopPlayer> HouseWanted;

    /// <summary>A player entered the world (host: also after a resync request). Puppets and full state follow this.</summary>
    public static event Action<CoopPlayer> PlayerReady;

    public static event Action<CoopPlayer> PlayerLeft;

    /// <summary>Client: the host accepted us and the world is on its way.</summary>
    public static event Action Welcomed;

    /// <summary>Client: in the host's world. Host: right after hosting starts.</summary>
    public static event Action WorldReady;

    /// <summary>The mod hot reloaded during a session; rebuild what lives in the scene.</summary>
    public static event Action Resumed;

    public static void Host(string transportId, int port, int maxPlayers, string name, string id)
    {
        if (IsActive)
            throw new InvalidOperationException("Already in a co-op game. Leave it first.");
        if (!GameEvents.IsHouseReady)
            throw new InvalidOperationException("Load a game first, then host it.");

        Use(Usable(Transports.Find(transportId)));
        Transport.Host(port, maxPlayers - 1);
        Reset();
        Local = Add(new CoopPlayer { Slot = CoopPlayer.HostSlot, Name = name, Id = id, IsLocal = true, InWorld = true });
        State = SessionState.Hosting;
        CoopMod.Log.Info(TransportId == Transports.DirectId
            ? $"Hosting on UDP port {port} as {name}."
            : $"Hosting over {Transports.Find(TransportId)?.Name} as {name}.");
        Started?.Invoke();
        WorldReady?.Invoke();
    }

    public static void Join(string address, int port)
    {
        if (IsActive)
            throw new InvalidOperationException("Already in a co-op game. Leave it first.");
        Use(Usable(Transports.ForAddress(address)));
        Reset();
        Transport.Join(address, port);
        State = SessionState.Connecting;
        CoopMod.Log.Info($"Connecting to {address}:{port}.");
        Started?.Invoke();
    }

    public static void Leave(string reason = "You left the game.")
    {
        if (!IsActive)
            return;
        Transport.Stop();
        End(reason);
    }

    public static CoopPlayer FindSlot(byte slot)
    {
        for (int i = 0; i < PlayerList.Count; i++)
        {
            if (PlayerList[i].Slot == slot)
                return PlayerList[i];
        }
        return null;
    }

    public static CoopPlayer FindPeer(int peer)
    {
        for (int i = 0; i < PlayerList.Count; i++)
        {
            if (!PlayerList[i].IsLocal && PlayerList[i].Peer == peer)
                return PlayerList[i];
        }
        return null;
    }

    internal static CoopPlayer Add(CoopPlayer player)
    {
        Remove(player.Slot);
        PlayerList.Add(player);
        return player;
    }

    internal static void Remove(byte slot)
    {
        var player = FindSlot(slot);
        if (player == null)
            return;
        PlayerList.Remove(player);
        if (player.InWorld && !player.IsLocal)
            PlayerLeft?.Invoke(player);
    }

    /// <summary>Switches to a transport (a new one per host or join, or the one carried over a reload).</summary>
    internal static void Use(TransportOption option, ITransport transport)
    {
        if (Transport != null && Transport != transport)
        {
            Handshake.Detach(Transport);
            Transport.Stop();
        }
        if (Transport != transport && transport != null)
            Handshake.Attach(transport);
        Transport = transport;
        TransportId = option.Id;
        Outbox.Transport = transport;
    }

    private static void Use(TransportOption option) => Use(option, option.Create());

    private static TransportOption Usable(TransportOption option)
    {
        if (option == null)
            throw new InvalidOperationException("That way of connecting isn't installed.");
        string why = option.Unavailable?.Invoke();
        if (why != null)
            throw new InvalidOperationException(why);
        return option;
    }

    internal static void SetLocal(CoopPlayer player) => Local = player;

    internal static void SetState(SessionState state) => State = state;

    internal static void RaiseArrived(CoopPlayer player) => PlayerArrived?.Invoke(player);
    internal static void RaiseReady(CoopPlayer player) => PlayerReady?.Invoke(player);
    internal static void RaiseHouseWanted(CoopPlayer player) => HouseWanted?.Invoke(player);
    internal static void RaiseWelcomed() => Welcomed?.Invoke();
    internal static void RaiseWorldReady() => WorldReady?.Invoke();
    internal static void RaiseResumed() => Resumed?.Invoke();

    /// <summary>The connection is gone (left, kicked, timed out or the host closed).</summary>
    internal static void End(string reason)
    {
        if (!IsActive)
            return;
        LastEndReason = reason;
        CoopMod.Log.Info($"Co-op ended: {reason}");
        IsEnding = true;
        try
        {
            Reset();
        }
        finally
        {
            IsEnding = false;
        }
        Ended?.Invoke(reason);
    }

    /// <summary>Restores the session after a hot reload (see <see cref="SessionMemory"/>).</summary>
    internal static void Restore(SessionState state, int hostPeer, List<CoopPlayer> players, byte localSlot)
    {
        Reset();
        State = state;
        HostPeer = hostPeer;
        PlayerList.AddRange(players);
        Local = FindSlot(localSlot);
    }

    private static void Reset()
    {
        // Players leaving the world first, so puppets and their state get cleaned up.
        for (int i = PlayerList.Count - 1; i >= 0; i--)
        {
            var player = PlayerList[i];
            PlayerList.RemoveAt(i);
            if (player.InWorld && !player.IsLocal)
                PlayerLeft?.Invoke(player);
        }
        State = SessionState.Offline;
        HostPeer = -1;
        Local = null;
    }
}
