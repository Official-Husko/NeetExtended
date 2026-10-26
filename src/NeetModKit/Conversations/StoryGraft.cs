using System;
using System.Collections.Generic;
using Ink.Runtime;

namespace NeetModKit.Conversations;

/// <summary>
/// Moves the knots of a compiled script into the game's story. Knots are placed at the top level,
/// exactly where they were in the script, so every path compiled into them stays valid.
/// </summary>
internal static class StoryGraft
{
    private const string GlobalDeclarations = "global decl";

    /// <summary>Adds every knot of <paramref name="compiledJson"/>. Adds nothing if any name is taken.</summary>
    public static List<KeyValuePair<string, INamedContent>> Add(Story story, string compiledJson)
    {
        // A fresh copy each time: one runtime object can only live in one story.
        var source = new Story(compiledJson).mainContentContainer;
        var root = story.mainContentContainer;

        var knots = new List<KeyValuePair<string, INamedContent>>();
        foreach (var pair in source.namedContent)
        {
            if (pair.Key == GlobalDeclarations)
                continue;
            if (root.namedContent.ContainsKey(pair.Key))
                throw new InvalidOperationException($"'{pair.Key}' already exists in the game's story (or another mod's script). Rename it.");
            knots.Add(pair);
        }

        foreach (var knot in knots)
        {
            source.namedContent.Remove(knot.Key);
            root.AddToNamedContentOnly(knot.Value);
        }
        return knots;
    }

    /// <summary>Removes knots added earlier, leaving anything that replaced them alone.</summary>
    public static void Remove(Story story, IEnumerable<KeyValuePair<string, INamedContent>> knots)
    {
        var named = story.mainContentContainer.namedContent;
        foreach (var knot in knots)
        {
            if (named.TryGetValue(knot.Key, out var current) && current == knot.Value)
                named.Remove(knot.Key);
        }
    }

    /// <summary>Name of the top-level knot <paramref name="container"/> belongs to, or null.</summary>
    public static string KnotOf(Container container)
    {
        if (container == null)
            return null;
        var current = container;
        while (current.parent is Container parent && parent.parent != null)
            current = parent;
        return current.parent != null ? current.name : null;
    }
}
