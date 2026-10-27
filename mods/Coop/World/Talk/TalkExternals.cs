using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Coop.World.Forwarding;
using HarmonyLib;
using Ink.Runtime;

namespace Coop.World.Talk;

/// <summary>
/// Host: the story's functions that act on "the player" (KillPlayer, SkeletonizePlayer and
/// KnockoutPlayer, which the game points at the speaker's playerRef, and TradeSoulForClone, which
/// uses the host's player) act on the talking player when the step is a remote player's: the effect
/// goes to their game and happens to their real body there (see <see cref="SoulTrade"/>).
/// In the host's own conversations they're the game's functions, untouched. The story keeps its
/// functions in a table; World swaps these in it and puts the game's back when it stops.
/// </summary>
internal static class TalkExternals
{
    private static readonly FieldInfo Table = AccessTools.Field(typeof(Story), "_externals");
    private static readonly FieldInfo Function = AccessTools.Field(AccessTools.Inner(typeof(Story), "ExternalFunctionDef"), "function");
    private static readonly Dictionary<string, Story.ExternalFunction> Originals = new Dictionary<string, Story.ExternalFunction>();

    private static Story _story;

    public static void Install()
    {
        var story = Dialogue._ != null ? Dialogue._.masterStory : null;
        if (story == null || story == _story)
            return;
        Uninstall();
        _story = story;
        Wrap("KillPlayer", (c, args) => Forwarder.StoryKilled(c.Slot));
        Wrap("SkeletonizePlayer", (c, args) => Forwarder.StorySkeletonized(c.Slot));
        Wrap("KnockoutPlayer", (c, args) => Forwarder.StoryKnockedOut(c.Slot, args.Length > 0 ? Convert.ToSingle(args[0]) : 0f));
        Wrap("TradeSoulForClone", (c, args) => SoulTrade.Request(c));
    }

    public static void Uninstall()
    {
        if (_story != null && Table.GetValue(_story) is IDictionary table)
        {
            foreach (var pair in Originals)
            {
                if (table.Contains(pair.Key))
                    Set(table, pair.Key, pair.Value);
            }
        }
        Originals.Clear();
        _story = null;
    }

    private static void Wrap(string name, Action<Conversation, object[]> remote)
    {
        if (!(Table.GetValue(_story) is IDictionary table) || !table.Contains(name))
            return;
        var original = (Story.ExternalFunction)Function.GetValue(table[name]);
        Originals[name] = original;
        Set(table, name, args =>
        {
            var c = TalkRunner.Current;
            if (c == null)
                return original(args);
            remote(c, args);
            return null;
        });
    }

    // The table holds structs: change a boxed copy and put it back.
    private static void Set(IDictionary table, string name, Story.ExternalFunction function)
    {
        object def = table[name];
        Function.SetValue(def, function);
        table[name] = def;
    }
}
