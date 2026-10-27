using System;
using LiteNetLib.Utils;

namespace Coop.Net;

/// <summary>
/// Moves bytes between this game and the others. Direct UDP is the first one; a Steam transport can
/// slot in behind the same interface. Peers are plain ints chosen by the transport. Every event is
/// raised on the main thread from <see cref="Poll"/>.
/// </summary>
public interface ITransport
{
    bool IsRunning { get; }

    event Action<int> Connected;
    event Action<int, string> Disconnected;

    /// <summary>The reader is only valid during the call.</summary>
    event Action<int, NetDataReader, Channel> Received;

    /// <summary>Listens for players. <paramref name="maxPeers"/> counts the others, not this game.</summary>
    void Host(int port, int maxPeers);

    void Join(string address, int port);

    void Poll();

    void Send(int peer, Channel channel, NetDataWriter data);

    /// <summary>Closes one connection; the other side sees <paramref name="reason"/>.</summary>
    void Kick(int peer, string reason);

    /// <summary>Round trip in milliseconds, or -1 for an unknown peer.</summary>
    int PingOf(int peer);

    /// <summary>Closes everything.</summary>
    void Stop();

    /// <summary>
    /// Hands the live connection to the next copy of the mod (hot reload). If no copy takes it up
    /// (the mod was switched off), it's closed after a few seconds and <paramref name="abandoned"/> runs.
    /// </summary>
    void Park(System.Action abandoned = null);
}
