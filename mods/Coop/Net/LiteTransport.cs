using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using LiteNetLib;
using LiteNetLib.Utils;
using NeetLoader.Runtime;

namespace Coop.Net;

/// <summary>
/// Direct UDP through LiteNetLib. LiteNetLib.dll is loaded once and never reloaded, so on a hot reload
/// the running NetManager and its listener are parked in ReloadState and the next copy of the mod
/// carries on with the same connection. Events are queued by LiteNetLib and raised in <see cref="Poll"/>.
/// </summary>
internal sealed class LiteTransport : ITransport, ITrafficStats
{
    private const string ManagerKey = "Coop.Net.Manager";
    private const string ListenerKey = "Coop.Net.Listener";
    private const string MaxPeersKey = "Coop.Net.MaxPeers";

    // A parked connection nobody picks up (the mod was switched off rather than reloaded) is closed.
    private const int ParkedLifetimeMs = 10000;

    private readonly Dictionary<int, NetPeer> _peers = new Dictionary<int, NetPeer>();
    private NetManager _manager;
    private EventBasedNetListener _listener;
    private int _maxPeers;

    public bool IsRunning => _manager != null && _manager.IsRunning;

    public long BytesSent => _manager?.Statistics.BytesSent ?? 0;
    public long BytesReceived => _manager?.Statistics.BytesReceived ?? 0;
    public long PacketsSent => _manager?.Statistics.PacketsSent ?? 0;
    public long PacketsReceived => _manager?.Statistics.PacketsReceived ?? 0;

    public event Action<int> Connected;
    public event Action<int, string> Disconnected;
    public event Action<int, NetDataReader, Channel> Received;

    /// <summary>Picks up a connection parked by the previous copy of the mod, if there is one.</summary>
    public static LiteTransport Resume()
    {
        var transport = new LiteTransport();
        if (ReloadState.Take(ManagerKey, out NetManager manager) && ReloadState.Take(ListenerKey, out EventBasedNetListener listener))
        {
            transport._manager = manager;
            transport._listener = listener;
            transport.Attach();
            foreach (var peer in manager.ConnectedPeerList)
                transport._peers[peer.Id] = peer;
            if (ReloadState.Take(MaxPeersKey, out int maxPeers))
                transport._maxPeers = maxPeers;
        }
        return transport;
    }

    public void Host(int port, int maxPeers)
    {
        Stop();
        _maxPeers = maxPeers;
        Create();
        if (!_manager.Start(port))
        {
            Stop();
            throw new InvalidOperationException($"Couldn't listen on UDP port {port}. Is another game or program using it?");
        }
    }

    public void Join(string address, int port)
    {
        Stop();
        Create();
        if (!_manager.Start())
        {
            Stop();
            throw new InvalidOperationException("Couldn't open a UDP socket.");
        }
        _manager.Connect(address, port, Protocol.ConnectionKey);
    }

    public void Poll() => _manager?.PollEvents();

    public void Send(int peer, Channel channel, NetDataWriter data)
    {
        // A connection carried over from an older build may have fewer channels; it lasts until the next join.
        if ((byte)channel >= _manager.ChannelsCount)
            return;
        if (_peers.TryGetValue(peer, out var target))
            target.Send(data.Data, 0, data.Length, (byte)channel, DeliveryFor(channel));
    }

    public void Kick(int peer, string reason)
    {
        if (_peers.TryGetValue(peer, out var target))
            _manager.DisconnectPeer(target, Encoding.UTF8.GetBytes(reason ?? ""));
    }

    public int PingOf(int peer) => _peers.TryGetValue(peer, out var target) ? target.RoundTripTime : -1;

    public void Stop()
    {
        if (_manager == null)
            return;
        Detach();
        _manager.Stop(true);
        _manager = null;
        _listener = null;
        _peers.Clear();
    }

    public void Park(Action abandoned = null)
    {
        if (_manager == null)
            return;
        Detach();
        ReloadState.Put(ManagerKey, _manager);
        ReloadState.Put(ListenerKey, _listener);
        ReloadState.Put(MaxPeersKey, _maxPeers);
        var parked = _manager;
        _manager = null;
        _listener = null;
        _peers.Clear();

        Timer timer = null;
        timer = new Timer(_ =>
        {
            timer?.Dispose();
            // Still parked: no new copy of the mod came for it.
            if (ReloadState.Take(ManagerKey, out NetManager left) && ReferenceEquals(left, parked))
            {
                ReloadState.Take(ListenerKey, out EventBasedNetListener _);
                left.Stop(true);
                abandoned?.Invoke();
            }
        }, null, ParkedLifetimeMs, Timeout.Infinite);
    }

    private void Create()
    {
        _listener = new EventBasedNetListener();
        _manager = new NetManager(_listener)
        {
            ChannelsCount = 5,
            AutoRecycle = true,
            UnsyncedEvents = false,
            // IPv6 dual-mode sockets go through Wine's Winsock; plain IPv4 is the safe choice.
            IPv6Enabled = false,
            // Loading the host's house can keep the main thread busy for a while.
            DisconnectTimeout = 20000,
            UpdateTime = 15,
            EnableStatistics = true,
        };
        Attach();
    }

    private void Attach()
    {
        // Whatever the previous copy of the mod hooked up belongs to code that no longer runs.
        _listener.ClearConnectionRequestEvent();
        _listener.ClearPeerConnectedEvent();
        _listener.ClearPeerDisconnectedEvent();
        _listener.ClearNetworkReceiveEvent();
        _listener.ClearNetworkErrorEvent();

        _listener.ConnectionRequestEvent += OnConnectionRequest;
        _listener.PeerConnectedEvent += OnPeerConnected;
        _listener.PeerDisconnectedEvent += OnPeerDisconnected;
        _listener.NetworkReceiveEvent += OnReceive;
        _listener.NetworkErrorEvent += (endPoint, error) => CoopMod.Log?.Warn($"Network error from {endPoint}: {error}");
    }

    private void Detach()
    {
        _listener?.ClearConnectionRequestEvent();
        _listener?.ClearPeerConnectedEvent();
        _listener?.ClearPeerDisconnectedEvent();
        _listener?.ClearNetworkReceiveEvent();
        _listener?.ClearNetworkErrorEvent();
    }

    private void OnConnectionRequest(ConnectionRequest request)
    {
        if (_manager.ConnectedPeersCount >= _maxPeers)
        {
            request.Reject(Encoding.UTF8.GetBytes("The game is full."));
            return;
        }
        request.AcceptIfKey(Protocol.ConnectionKey);
    }

    private void OnPeerConnected(NetPeer peer)
    {
        _peers[peer.Id] = peer;
        Connected?.Invoke(peer.Id);
    }

    private void OnPeerDisconnected(NetPeer peer, DisconnectInfo info)
    {
        _peers.Remove(peer.Id);
        Disconnected?.Invoke(peer.Id, Describe(info));
    }

    private void OnReceive(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod method) =>
        Received?.Invoke(peer.Id, reader, (Channel)channel);

    private static string Describe(DisconnectInfo info)
    {
        if (info.AdditionalData != null && info.AdditionalData.AvailableBytes > 0)
            return Encoding.UTF8.GetString(info.AdditionalData.RawData, info.AdditionalData.Position, info.AdditionalData.AvailableBytes);
        return info.Reason switch
        {
            DisconnectReason.ConnectionFailed => "Nobody answered at that address.",
            DisconnectReason.Timeout => "The connection timed out.",
            DisconnectReason.ConnectionRejected => "The host turned the connection down (different Co-op version?).",
            DisconnectReason.RemoteConnectionClose => "The other side closed the game or left.",
            DisconnectReason.DisconnectPeerCalled => "Left the game.",
            DisconnectReason.HostUnreachable or DisconnectReason.NetworkUnreachable => "That address can't be reached.",
            _ => info.Reason.ToString(),
        };
    }

    private static DeliveryMethod DeliveryFor(Channel channel) => channel switch
    {
        Channel.Stream => DeliveryMethod.Sequenced,
        Channel.WorldStream => DeliveryMethod.Unreliable,
        _ => DeliveryMethod.ReliableOrdered,
    };
}
