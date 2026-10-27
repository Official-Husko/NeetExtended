using System;
using System.Threading;
using NeetLoader.Runtime;
using Steamworks;

namespace CoopSteam.Transport;

/// <summary>
/// A live connection handed from one copy of the mod to the next across a hot reload. Steam's handles are
/// plain numbers and the API itself stays up (Steamworks.NET never reloads), so only the numbers go into
/// ReloadState: hosting flag, listen socket, poll group, max peers, then the peer table.
/// </summary>
internal static class ParkedSession
{
    private const string Key = "CoopSteam.Parked";
    private const int ParkedLifetimeMs = 10000;
    private const int Header = 4;

    public static void Park(bool hosting, HSteamListenSocket listen, HSteamNetPollGroup group, int maxPeers, PeerTable peers, Action abandoned)
    {
        var table = peers.Export();
        var data = new ulong[Header + table.Length];
        data[0] = hosting ? 1UL : 0UL;
        data[1] = listen.m_HSteamListenSocket;
        data[2] = group.m_HSteamNetPollGroup;
        data[3] = (ulong)maxPeers;
        Array.Copy(table, 0, data, Header, table.Length);
        ReloadState.Put(Key, data);

        Timer timer = null;
        timer = new Timer(_ =>
        {
            timer?.Dispose();
            // Still there: no new copy of the mod came for it.
            if (ReloadState.Take(Key, out ulong[] left) && ReferenceEquals(left, data))
            {
                CloseAll(left);
                abandoned?.Invoke();
            }
        }, null, ParkedLifetimeMs, Timeout.Infinite);
    }

    public static bool Take(out bool hosting, out HSteamListenSocket listen, out HSteamNetPollGroup group, out int maxPeers, PeerTable peers)
    {
        hosting = false;
        listen = HSteamListenSocket.Invalid;
        group = HSteamNetPollGroup.Invalid;
        maxPeers = 0;
        if (!ReloadState.Take(Key, out ulong[] data))
            return false;
        hosting = data[0] != 0;
        listen = new HSteamListenSocket((uint)data[1]);
        group = new HSteamNetPollGroup((uint)data[2]);
        maxPeers = (int)data[3];
        peers.Import(data, Header);
        return true;
    }

    private static void CloseAll(ulong[] data)
    {
        foreach (uint conn in PeerTable.ConnectionsIn(data, Header))
            SteamNetworkingSockets.CloseConnection(new HSteamNetConnection(conn), SteamTransport.LeaveReason, "", true);
        if (data[1] != 0)
            SteamNetworkingSockets.CloseListenSocket(new HSteamListenSocket((uint)data[1]));
        if (data[2] != 0)
            SteamNetworkingSockets.DestroyPollGroup(new HSteamNetPollGroup((uint)data[2]));
    }
}
