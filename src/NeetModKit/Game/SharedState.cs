using System.Collections.Generic;
using NeetModKit.Events;
using Newtonsoft.Json.Linq;

namespace NeetModKit.Game;

/// <summary>
/// State the game running the house keeps for everyone, like where an email conversation stands.
/// Mods publish it where the house runs (<see cref="WorldRole.RunsWorld"/>); every co-op player sees
/// it through <see cref="Changed"/>. A co-op mod carries it to the other games and fills them in
/// when they join. Values belong to the house: loading another scene clears them. Keys are named
/// after the mod ("MyMod.Thing.42").
/// </summary>
public static class SharedState
{
    private static readonly Dictionary<string, JToken> Values = new Dictionary<string, JToken>();

    /// <summary>Raised for every change, published here or arriving from the host.</summary>
    public static readonly GameEvent<KeyValuePair<string, JToken>> Changed =
        new GameEvent<KeyValuePair<string, JToken>>(nameof(Changed));

    /// <summary>Raised where the house runs when a mod publishes; the co-op mod passes it on.</summary>
    public static readonly GameEvent<KeyValuePair<string, JToken>> Published =
        new GameEvent<KeyValuePair<string, JToken>>(nameof(Published));

    public static IEnumerable<KeyValuePair<string, JToken>> All => Values;

    /// <summary>Sets a value for everyone. Ignored in a game that shows another game's house.</summary>
    public static void Publish(string key, JToken value)
    {
        if (!WorldRole.RunsWorld)
            return;
        Values[key] = value;
        var change = new KeyValuePair<string, JToken>(key, value);
        Published.Raise(change, KitMod.Instance?.Log);
        Changed.Raise(change, KitMod.Instance?.Log);
    }

    public static bool TryGet(string key, out JToken value) => Values.TryGetValue(key, out value);

    /// <summary>For the co-op mod on a client: a value the host published.</summary>
    public static void Apply(string key, JToken value)
    {
        Values[key] = value;
        Changed.Raise(new KeyValuePair<string, JToken>(key, value), KitMod.Instance?.Log);
    }

    internal static void Clear() => Values.Clear();
}
