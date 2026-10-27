using System;
using System.Collections.Generic;
using NeetLoader.Runtime;
using Newtonsoft.Json.Linq;

namespace NeetModKit.Game;

/// <summary>
/// Player actions that change the shared house, like answering an email. They run where the house
/// runs: right here in single player or on a co-op host, and on the host when this game is a co-op
/// client (a co-op mod sets <see cref="Forward"/>). Handlers learn which player acted: 0 is this
/// game's own player, anything else a co-op player's slot.
/// </summary>
public static class HostActions
{
    public delegate void Handler(JToken args, int player);

    private static readonly Dictionary<string, Handler> Handlers = new Dictionary<string, Handler>();

    /// <summary>Set by a co-op mod while this game shows another game's house: sends an action there.</summary>
    public static Action<string, JToken> Forward { get; set; }

    /// <summary>Adds an action until <paramref name="owner"/> unloads. Name it after your mod ("MyMod.Thing").</summary>
    public static void Register(ITracker owner, string name, Handler handler)
    {
        Handlers[name] = handler;
        owner.Track(Disposable.Create(() =>
        {
            if (Handlers.TryGetValue(name, out var current) && current == handler)
                Handlers.Remove(name);
        }));
    }

    /// <summary>Runs an action where the house runs. False if it couldn't go anywhere.</summary>
    public static bool Run(string name, JToken args = null)
    {
        if (WorldRole.RunsWorld)
            return Invoke(name, args, 0);
        var forward = Forward;
        if (forward == null)
            return false;
        forward(name, args);
        return true;
    }

    /// <summary>For the co-op mod on the host: runs an action another player sent.</summary>
    public static bool Invoke(string name, JToken args, int player)
    {
        if (!Handlers.TryGetValue(name, out var handler))
        {
            KitMod.Instance?.Log.Warn($"Nothing handles the action {name}.");
            return false;
        }
        try
        {
            handler(args, player);
            return true;
        }
        catch (Exception e)
        {
            KitMod.Instance?.Log.Error($"The action {name} failed", e);
            return false;
        }
    }
}
