using Steamworks;

namespace CoopSteam.Transport;

/// <summary>Settings for the listen socket and the outgoing connection, and the lanes of a connection.</summary>
internal static class ConnectionOptions
{
    // Steam's default is 256 KB/s, which makes the join snapshot take seconds.
    private const int SendRate = 2 * 1024 * 1024;
    // Room for a join snapshot (several chunks) queued at once.
    private const int SendBuffer = 8 * 1024 * 1024;
    // The main thread can be busy loading a house for a while; the connection shouldn't give up on it.
    private const int TimeoutMs = 20000;

    /// <summary>Lane 0: everything small. Lane 1: bulk, only gets what lane 0 leaves over.</summary>
    public const int Lanes = 2;
    public const ushort NormalLane = 0;
    public const ushort BulkLane = 1;
    public static readonly int[] LanePriorities = { 0, 1 };
    public static readonly ushort[] LaneWeights = { 1, 1 };

    public static SteamNetworkingConfigValue_t[] Create() => new[]
    {
        Int(ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_SendRateMin, SendRate),
        Int(ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_SendRateMax, SendRate),
        Int(ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_SendBufferSize, SendBuffer),
        Int(ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_TimeoutConnected, TimeoutMs),
    };

    private static SteamNetworkingConfigValue_t Int(ESteamNetworkingConfigValue key, int value)
    {
        var option = new SteamNetworkingConfigValue_t
        {
            m_eValue = key,
            m_eDataType = ESteamNetworkingConfigDataType.k_ESteamNetworkingConfig_Int32,
        };
        option.m_val.m_int32 = value;
        return option;
    }
}
