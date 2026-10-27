namespace Coop.Net;

/// <summary>Running byte and packet counts, for transports that keep them (direct UDP does).</summary>
public interface ITrafficStats
{
    long BytesSent { get; }
    long BytesReceived { get; }
    long PacketsSent { get; }
    long PacketsReceived { get; }
}
