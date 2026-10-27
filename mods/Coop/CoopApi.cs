using System;
using System.Text;
using Coop.Net;
using Coop.Session;

namespace Coop;

/// <summary>
/// Plain entry points for scripts and the dev bridge (`neet call type.call type=Coop.CoopApi method=Status`).
/// Each returns a line saying what happened.
/// </summary>
public static class CoopApi
{
    public static string Host() => HostWith(Transports.DirectId);

    /// <summary>Hosts with one of <see cref="Transports.All"/> ("direct", "steam").</summary>
    public static string HostWith(string transportId) => Try(() =>
    {
        var config = CoopMod.Config.Value;
        CoopSession.Host(transportId, config.Port, config.MaxPlayers, CoopMod.PlayerName, config.PlayerId);
        string address = Transports.Find(transportId)?.HostAddress?.Invoke();
        return address != null ? $"Hosting. Others join with {address}." : $"Hosting on UDP port {config.Port}.";
    });

    public static string Join(string address) => Try(() =>
    {
        ParseAddress(address, CoopMod.Config.Value.Port, out string host, out int port);
        CoopSession.Join(host, port);
        CoopMod.Config.Value.LastAddress = address.Trim();
        CoopMod.Config.Save();
        return $"Connecting to {host}:{port}.";
    });

    public static string Leave()
    {
        CoopSession.Leave();
        return "Left.";
    }

    public static string Status()
    {
        var text = new StringBuilder(CoopSession.State.ToString());
        foreach (var player in CoopSession.Players)
        {
            text.Append("\n  ").Append(player.Slot).Append(' ').Append(player.Name);
            if (player.IsLocal)
                text.Append(" (you)");
            else
                text.Append(", ping ").Append(CoopSession.Transport.PingOf(player.Peer)).Append(" ms");
            if (!player.InWorld)
                text.Append(", loading");
        }
        if (!CoopSession.IsActive && CoopSession.LastEndReason.Length > 0)
            text.Append("\n  last: ").Append(CoopSession.LastEndReason);
        return text.ToString();
    }

    private static long _lastSent, _lastReceived;
    private static float _lastAt = -1f;

    /// <summary>Traffic since the last call (or the session start): KB/s each way and the totals.</summary>
    public static string Traffic()
    {
        if (!(CoopSession.Transport is ITrafficStats stats))
            return "This connection doesn't count its traffic.";
        float now = UnityEngine.Time.realtimeSinceStartup;
        long sent = stats.BytesSent, received = stats.BytesReceived;
        string rate = "";
        if (_lastAt >= 0f && now > _lastAt && sent >= _lastSent)
        {
            float seconds = now - _lastAt;
            rate = $"{(sent - _lastSent) / 1024f / seconds:0.0} KB/s out, {(received - _lastReceived) / 1024f / seconds:0.0} KB/s in over {seconds:0.0} s; ";
        }
        _lastSent = sent;
        _lastReceived = received;
        _lastAt = now;
        return rate + $"total {sent / 1024} KB out ({stats.PacketsSent} packets), {received / 1024} KB in ({stats.PacketsReceived} packets).";
    }

    /// <summary>"1.2.3.4", "1.2.3.4:8889", "host.name:9000".</summary>
    internal static void ParseAddress(string address, int defaultPort, out string host, out int port)
    {
        address = (address ?? "").Trim();
        if (address.Length == 0)
            throw new ArgumentException("Type the host's address first.");
        port = defaultPort;
        host = address;
        int colon = address.LastIndexOf(':');
        if (colon > 0 && address.IndexOf(':') == colon)
        {
            if (!int.TryParse(address.Substring(colon + 1), out port) || port < 1 || port > 65535)
                throw new ArgumentException($"\"{address.Substring(colon + 1)}\" isn't a port.");
            host = address.Substring(0, colon);
        }
    }

    private static string Try(Func<string> action)
    {
        try
        {
            return action();
        }
        catch (Exception e) when (e is InvalidOperationException || e is ArgumentException)
        {
            return e.Message;
        }
    }
}
