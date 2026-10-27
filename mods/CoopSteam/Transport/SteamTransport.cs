using System;
using System.Collections.Generic;
using Coop.Net;
using CoopSteam.Native;
using LiteNetLib.Utils;
using Steamworks;

namespace CoopSteam.Transport;

/// <summary>
/// Co-op over ISteamNetworkingSockets peer to peer: the host listens on virtual port 0, clients connect
/// to the host's SteamID64, and Steam picks a direct route or its relay network. Steam tells us about
/// connections through a callback; those are queued and raised from <see cref="Poll"/> like the other
/// events. Who is allowed in is Co-op's own handshake; Steam only proves the SteamID (and that the
/// player owns the game).
/// </summary>
internal sealed class SteamTransport : ITransport
{
    // Close codes in the range Steam leaves to the app.
    public const int LeaveReason = 1000;
    private const int FullReason = 1001;
    private const int PingEveryMs = 2000;

    private readonly PeerTable _peers = new PeerTable();
    private readonly List<Pending> _pending = new List<Pending>();
    private readonly List<Pending> _raising = new List<Pending>();
    private readonly MessageSender _sender = new MessageSender();
    private readonly MessageReceiver _receiver;

    private Callback<SteamNetConnectionStatusChangedCallback_t> _status;
    private HSteamListenSocket _listen = HSteamListenSocket.Invalid;
    private HSteamNetPollGroup _group = HSteamNetPollGroup.Invalid;
    private bool _running;
    private bool _hosting;
    private int _maxPeers;
    private int _pingAt;
    private SteamNetConnectionRealTimeStatus_t _realTime = new SteamNetConnectionRealTimeStatus_t { reserved = new uint[15] };
    private SteamNetConnectionRealTimeLaneStatus_t _lane = new SteamNetConnectionRealTimeLaneStatus_t { reserved = new uint[10] };
    private bool _pingBroken;

    private struct Pending
    {
        public bool IsConnect;
        public int Peer;
        public string Text;
    }

    public SteamTransport()
    {
        _receiver = new MessageReceiver(_peers, Deliver, Misbehaved, () => _running);
    }

    public bool IsRunning => _running;

    public event Action<int> Connected;
    public event Action<int, string> Disconnected;
    public event Action<int, NetDataReader, Channel> Received;

    /// <summary>Picks up a connection parked by the previous copy of the mod, if there is one.</summary>
    public static SteamTransport Resume()
    {
        var transport = new SteamTransport();
        if (SteamRuntime.Ensure() == null
            && ParkedSession.Take(out bool hosting, out var listen, out var group, out int maxPeers, transport._peers))
        {
            transport._hosting = hosting;
            transport._listen = listen;
            transport._group = group;
            transport._maxPeers = maxPeers;
            transport.Begin();
            transport.Reconcile();
        }
        return transport;
    }

    public void Host(int port, int maxPeers)
    {
        Require();
        Stop();
        _hosting = true;
        _maxPeers = maxPeers;
        _group = SteamNetworkingSockets.CreatePollGroup();
        var options = ConnectionOptions.Create();
        _listen = SteamNetworkingSockets.CreateListenSocketP2P(0, options.Length, options);
        if (_listen == HSteamListenSocket.Invalid)
        {
            Stop();
            throw new InvalidOperationException("Steam wouldn't open a listen socket. Is Steam online?");
        }
        Begin();
    }

    public void Join(string address, int port)
    {
        Require();
        if (!SteamAddress.TryParse(address, out ulong steamId))
            throw new InvalidOperationException("That isn't a Steam ID. It's the 17 digit number of the host (their SteamID64).");
        Stop();
        _hosting = false;
        _group = SteamNetworkingSockets.CreatePollGroup();

        var identity = new SteamNetworkingIdentity();
        identity.Clear();
        identity.SetSteamID64(steamId);
        var options = ConnectionOptions.Create();
        var conn = SteamNetworkingSockets.ConnectP2P(ref identity, 0, options.Length, options);
        if (conn == HSteamNetConnection.Invalid)
        {
            Stop();
            throw new InvalidOperationException("Steam wouldn't start the connection. Is Steam online?");
        }
        _peers.Add(conn.m_HSteamNetConnection, steamId);
        SteamNetworkingSockets.SetConnectionPollGroup(conn, _group);
        Begin();
    }

    public void Poll()
    {
        SteamRuntime.Pump();
        if (!_running)
            return;
        Raise();
        if (!_running)
            return;
        _receiver.Pump(_group);
        Raise();
        RefreshPing();
    }

    public void Send(int peer, Channel channel, NetDataWriter data)
    {
        if (!_running || !_peers.TryGetById(peer, out Peer target) || !target.Connected)
            return;
        int result = _sender.Send(target, channel, data.Data, data.Length);
        if (result == 0)
            return;
        bool reliable = channel != Channel.Stream && channel != Channel.WorldStream;
        if (reliable && result == -(int)EResult.k_EResultLimitExceeded)
            Kick(peer, "Too much data was waiting to be sent to you. Rejoin.");
        else if (reliable && result != -(int)EResult.k_EResultNoConnection && result != -(int)EResult.k_EResultInvalidState)
            CoopSteamMod.Log?.Warn($"Sending on {channel} failed: {-result}");
    }

    public void Kick(int peer, string reason)
    {
        if (!_running || !_peers.TryGetById(peer, out Peer target))
            return;
        SteamNetworkingSockets.CloseConnection(new HSteamNetConnection(target.Conn), LeaveReason, reason ?? "", true);
        _peers.Remove(target);
        if (target.Connected)
            _pending.Add(new Pending { Peer = peer, Text = "Left the game." });
    }

    public int PingOf(int peer) => _peers.TryGetById(peer, out Peer target) ? target.Ping : -1;

    public void Stop()
    {
        End();
        if (_group != HSteamNetPollGroup.Invalid)
            SteamNetworkingSockets.DestroyPollGroup(_group);
        _group = HSteamNetPollGroup.Invalid;
        _pending.Clear();
        _running = false;
    }

    public void Park(Action abandoned = null)
    {
        if (!_running)
            return;
        _status?.Dispose();
        _status = null;
        ParkedSession.Park(_hosting, _listen, _group, _maxPeers, _peers, abandoned);
        _peers.Clear();
        _pending.Clear();
        _listen = HSteamListenSocket.Invalid;
        _group = HSteamNetPollGroup.Invalid;
        _running = false;
    }

    private static void Require()
    {
        string why = SteamRuntime.Ensure();
        if (why != null)
            throw new InvalidOperationException(why);
    }

    private void Begin()
    {
        _status = Callback<SteamNetConnectionStatusChangedCallback_t>.Create(OnStatus);
        _running = true;
        _pingAt = Environment.TickCount;
    }

    // Closes connections (their last reliable data still goes out) and the listen socket.
    private void End()
    {
        _status?.Dispose();
        _status = null;
        for (int i = 0; i < _peers.Count; i++)
            SteamNetworkingSockets.CloseConnection(new HSteamNetConnection(_peers[i].Conn), LeaveReason, "", true);
        _peers.Clear();
        if (_listen != HSteamListenSocket.Invalid)
            SteamNetworkingSockets.CloseListenSocket(_listen);
        _listen = HSteamListenSocket.Invalid;
    }

    private void OnStatus(SteamNetConnectionStatusChangedCallback_t change) =>
        HandleState(change.m_hConn, change.m_info, change.m_eOldState);

    private void HandleState(HSteamNetConnection conn, SteamNetConnectionInfo_t info, ESteamNetworkingConnectionState old)
    {
        var state = info.m_eState;
        bool closed = state == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer
                      || state == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally;
        if (!_running)
        {
            // A late callback for something already let go of; the handle still has to be released.
            if (closed)
                SteamNetworkingSockets.CloseConnection(conn, 0, "", false);
            return;
        }

        if (state == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connecting
            && _hosting && info.m_hListenSocket == _listen)
            Incoming(conn, info);
        else if (state == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected)
            Established(conn);
        else if (closed)
            Closed(conn, old, info);
    }

    private void Incoming(HSteamNetConnection conn, SteamNetConnectionInfo_t info)
    {
        if (_peers.Count >= _maxPeers)
        {
            SteamNetworkingSockets.CloseConnection(conn, FullReason, "The game is full.", false);
            return;
        }
        if (SteamNetworkingSockets.AcceptConnection(conn) != EResult.k_EResultOK)
        {
            SteamNetworkingSockets.CloseConnection(conn, 0, "", false);
            return;
        }
        _peers.Add(conn.m_HSteamNetConnection, info.m_identityRemote.GetSteamID64());
        SteamNetworkingSockets.SetConnectionPollGroup(conn, _group);
    }

    private void Established(HSteamNetConnection conn)
    {
        if (!_peers.TryGet(conn.m_HSteamNetConnection, out Peer peer) || peer.Connected)
            return;
        peer.Connected = true;
        SteamNetworkingSockets.ConfigureConnectionLanes(conn, ConnectionOptions.Lanes, ConnectionOptions.LanePriorities, ConnectionOptions.LaneWeights);
        _pending.Add(new Pending { IsConnect = true, Peer = peer.Id });
    }

    private void Closed(HSteamNetConnection conn, ESteamNetworkingConnectionState old, SteamNetConnectionInfo_t info)
    {
        bool byPeer = info.m_eState == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer;
        string debug = info.m_szEndDebug;
        CoopSteamMod.Log?.Info($"Connection {conn} ended: {info.m_eState}, reason {info.m_eEndReason}, \"{debug}\"");
        Lost(conn, ReasonText(byPeer, byPeer ? debug : null, old));
    }

    private void Lost(HSteamNetConnection conn, string text)
    {
        SteamNetworkingSockets.CloseConnection(conn, 0, "", false);
        if (!_peers.TryGet(conn.m_HSteamNetConnection, out Peer peer))
            return;
        _peers.Remove(peer);
        // A guest that never finished connecting isn't a player yet; a client always wants to hear why.
        if (!peer.Connected && _hosting)
            return;
        _pending.Add(new Pending { Peer = peer.Id, Text = text });
    }

    private static string ReasonText(bool byPeer, string theirs, ESteamNetworkingConnectionState old)
    {
        if (!string.IsNullOrEmpty(theirs))
            return theirs;
        if (byPeer)
            return "The other side closed the game or left.";
        bool neverConnected = old == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connecting
                              || old == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_FindingRoute;
        return neverConnected ? "Couldn't reach that player over Steam. Are they hosting, and is the ID right?" : "The connection timed out.";
    }

    // After a hot reload: whatever happened to the connections in the meantime wasn't seen.
    private void Reconcile()
    {
        for (int i = _peers.Count - 1; i >= 0; i--)
        {
            var conn = new HSteamNetConnection(_peers[i].Conn);
            if (SteamNetworkingSockets.GetConnectionInfo(conn, out SteamNetConnectionInfo_t info))
                HandleState(conn, info, ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected);
            else
                Lost(conn, "The connection timed out.");
        }
    }

    private void Raise()
    {
        if (_pending.Count == 0)
            return;
        _raising.AddRange(_pending);
        _pending.Clear();
        for (int i = 0; i < _raising.Count; i++)
        {
            var item = _raising[i];
            if (item.IsConnect)
                Connected?.Invoke(item.Peer);
            else
                Disconnected?.Invoke(item.Peer, item.Text);
        }
        _raising.Clear();
    }

    private void Deliver(Peer peer, NetDataReader reader, Channel channel) => Received?.Invoke(peer.Id, reader, channel);

    private void Misbehaved(Peer peer, string reason) => Kick(peer.Id, reason);

    private void RefreshPing()
    {
        if (_pingBroken || unchecked(Environment.TickCount - _pingAt) < PingEveryMs)
            return;
        _pingAt = Environment.TickCount;
        try
        {
            for (int i = 0; i < _peers.Count; i++)
            {
                var peer = _peers[i];
                if (peer.Connected && SteamNetworkingSockets.GetConnectionRealTimeStatus(new HSteamNetConnection(peer.Conn), ref _realTime, 0, ref _lane) == EResult.k_EResultOK)
                    peer.Ping = _realTime.m_nPing;
            }
        }
        catch (Exception e)
        {
            _pingBroken = true;
            CoopSteamMod.Log?.Warn("Reading the ping failed, it will show -1: " + e.Message);
        }
    }
}
