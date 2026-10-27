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

    /// <summary>Logs this game's hand and use presses with what they'd hit (tests).</summary>
    public static string ProbeInput(bool on)
    {
        Testing.InputProbe.Set(on);
        return on ? "Logging presses as [probe] lines." : "Stopped.";
    }

    /// <summary>Everything along this game's camera ray within 6 m, nearest first (tests).</summary>
    public static string RayAll()
    {
        var camera = HouseManager._ != null ? HouseManager._.mainCamera : null;
        if (camera == null)
            return "No player camera.";
        var eye = camera.transform;
        var hits = UnityEngine.Physics.RaycastAll(eye.position, eye.forward, 6f, ~0, UnityEngine.QueryTriggerInteraction.Collide);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        var text = new StringBuilder($"camera {camera.name} at {eye.position} looking {eye.forward}:");
        foreach (var hit in hits)
            text.Append($"\n  {hit.distance:0.00} m {hit.collider.name} ({UnityEngine.LayerMask.LayerToName(hit.collider.gameObject.layer)}{(hit.collider.isTrigger ? ", trigger" : "")})");
        return text.ToString();
    }

    /// <summary>Patched methods whose name contains <paramref name="filter"/>, with who patched them (tests).</summary>
    public static string Patched(string filter)
    {
        var text = new StringBuilder();
        foreach (var method in HarmonyLib.Harmony.GetAllPatchedMethods())
        {
            string name = method.DeclaringType?.Name + "." + method.Name;
            if (name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                continue;
            var info = HarmonyLib.Harmony.GetPatchInfo(method);
            text.Append(name).Append(':');
            foreach (var p in info.Prefixes) text.Append(" pre ").Append(p.owner).Append('/').Append(p.PatchMethod.DeclaringType?.Name);
            foreach (var p in info.Postfixes) text.Append(" post ").Append(p.owner).Append('/').Append(p.PatchMethod.DeclaringType?.Name);
            foreach (var p in info.Transpilers) text.Append(" trans ").Append(p.owner).Append('/').Append(p.PatchMethod.DeclaringType?.Name);
            text.Append('\n');
        }
        return text.Length > 0 ? text.ToString() : "None.";
    }

    /// <summary>An object's layer and its body's flags, by path or #id (tests).</summary>
    public static string LayerOf(string target)
    {
        var go = NeetModKit.Scenes.SceneQuery.Resolve(target);
        var body = go.GetComponent<UnityEngine.Rigidbody>();
        return $"{go.name}: layer {go.layer} ({UnityEngine.LayerMask.LayerToName(go.layer)})" +
            (body != null ? $", kinematic {body.isKinematic}, detects collisions {body.detectCollisions}" : "");
    }

    /// <summary>What this game's player is looking at (the first thing the camera ray hits within 4 m), for tests.</summary>
    public static string LookAt()
    {
        var camera = HouseManager._ != null ? HouseManager._.mainCamera : null;
        if (camera == null)
            return "No player camera.";
        var eye = camera.transform;
        // The mask the game's own pickup uses, so this sees what a click would.
        int mask = HouseManager._.player.TryGetComponent(out Player_Abilities abilities) ? abilities.usablesMask.value : ~0;
        if (!UnityEngine.Physics.Raycast(eye.position, eye.forward, out var hit, 4f, mask, UnityEngine.QueryTriggerInteraction.Ignore))
        {
            // What a click can't reach, and why: the layer of whatever is there.
            if (!UnityEngine.Physics.Raycast(eye.position, eye.forward, out var any, 4f, ~0, UnityEngine.QueryTriggerInteraction.Ignore))
                return "Nothing within 4 m.";
            return $"Nothing a click reaches; the ray meets {any.collider.name} on layer {UnityEngine.LayerMask.LayerToName(any.collider.gameObject.layer)} at {any.distance:0.00} m.";
        }
        var go = hit.collider.gameObject;
        var usable = go.GetComponentInParent<Usable>();
        return $"{go.name} (layer {UnityEngine.LayerMask.LayerToName(go.layer)}) at {hit.distance:0.00} m" +
            (usable != null ? $", usable {usable.name}{(usable.lever ? " lever" : "")}{(usable.button ? " button" : "")}, held by {(usable.player != null ? usable.player.name : "nobody")}" : ", not usable");
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
