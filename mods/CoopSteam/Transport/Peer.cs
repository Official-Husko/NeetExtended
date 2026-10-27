using Coop.Net;

namespace CoopSteam.Transport;

/// <summary>One connection: who it is, where the stream numbers are, and the state of big messages in flight.</summary>
internal sealed class Peer
{
    public readonly int Id;
    public readonly uint Conn;
    public readonly ulong SteamId;

    public bool Connected;
    public int Ping = -1;

    // Stream packets carry a counter so old ones that arrive late are dropped.
    public ushort OutSeq;
    public ushort InSeq;
    public bool HasInSeq;

    // Reliable channels only (Events, State, Bulk).
    private readonly ChunkAssembler[] _assemblers = new ChunkAssembler[3];

    public Peer(int id, uint conn, ulong steamId)
    {
        Id = id;
        Conn = conn;
        SteamId = steamId;
    }

    public ChunkAssembler AssemblerFor(Channel channel) => _assemblers[(int)channel - 1] ??= new ChunkAssembler();
}
