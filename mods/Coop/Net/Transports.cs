using System;
using System.Collections.Generic;
using NeetLoader.Runtime;

namespace Coop.Net;

/// <summary>One way to connect, like direct UDP (built in) or Steam (its own mod).</summary>
public sealed class TransportOption
{
    /// <summary>Stable id, also kept with a session across hot reloads ("direct", "steam").</summary>
    public string Id;

    /// <summary>Shown in the menu ("Direct", "Steam").</summary>
    public string Name;

    /// <summary>
    /// A transport ready to host or join. If this option parked a running connection in a hot reload,
    /// it hands that one back (<see cref="ITransport.IsRunning"/> is then true).
    /// </summary>
    public Func<ITransport> Create;

    /// <summary>Whether a typed join address is meant for this option (a Steam id, for example).</summary>
    public Func<string, bool> Handles;

    /// <summary>What others type to join this game while it hosts with this option, or null.</summary>
    public Func<string> HostAddress;

    /// <summary>Null when it can be used now; otherwise why not ("Steam isn't running").</summary>
    public Func<string> Unavailable;
}

/// <summary>The ways this game can connect. Other mods add theirs with <see cref="Register"/>.</summary>
public static class Transports
{
    public const string DirectId = "direct";

    private static readonly List<TransportOption> Options = new List<TransportOption>();

    public static IReadOnlyList<TransportOption> All => Options;

    /// <summary>Raised when an option is added (a carried-over session may be waiting for it).</summary>
    public static event Action<TransportOption> Registered;

    /// <summary>Adds an option until <paramref name="owner"/> unloads.</summary>
    public static void Register(ITracker owner, TransportOption option)
    {
        Options.RemoveAll(o => o.Id == option.Id);
        Options.Add(option);
        owner.Track(Disposable.Create(() => Options.Remove(option)));
        Registered?.Invoke(option);
    }

    public static TransportOption Find(string id) => Options.Find(o => o.Id == id);

    /// <summary>The option a typed address belongs to: one that claims it, else direct.</summary>
    public static TransportOption ForAddress(string address)
    {
        foreach (var option in Options)
        {
            if (option.Id != DirectId && option.Handles != null && option.Handles(address))
                return option;
        }
        return Find(DirectId);
    }
}
