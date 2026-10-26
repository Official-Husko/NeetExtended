using System;
using System.Collections.Generic;
using Ink.Runtime;
using NeetLoader;
using NeetLoader.Logging;
using NeetLoader.Runtime;

namespace NeetModKit.Conversations;

/// <summary>
/// Adds Ink scripts to the game's dialogue story. A script's knots become part of the story, so the
/// game's dialogue box can play them like its own lines, with choices, tags and text effects:
/// <code>
/// InkScripts.Add(this, "hello.ink", "=== MyMod_Hello ===\nHi there!\n-> DONE");
/// Dialogue._.InitCharacterDialogue(speaker, "MyMod", "MyMod_Hello");
/// </code>
/// The game's lists, global variables and functions (and anything in <see cref="InkFunctions"/>)
/// are available without declaring them. Scripts compile once, when the game first creates its
/// story, and are added again to every new story. Prefix knot names with something unique.
/// </summary>
public static class InkScripts
{
    private static readonly List<InkScript> Scripts = new List<InkScript>();
    private static readonly HashSet<string> GraftedKnots = new HashSet<string>(StringComparer.Ordinal);
    private static ModLogger _log;

    public static IReadOnlyList<InkScript> All => Scripts;

    /// <summary>Adds a script; it's removed from the story again when <paramref name="owner"/> cleans up.</summary>
    public static InkScript Add(ITracker owner, string name, string inkSource)
    {
        var script = new InkScript(name, inkSource ?? "");
        Scripts.Add(script);
        owner.Track(Disposable.Create(() => Remove(script)));

        var story = GameStory.Current;
        if (story != null)
            Apply(story, script);
        return script;
    }

    /// <summary>True if the top-level knot <paramref name="knotName"/> came from a mod script in the current story.</summary>
    public static bool IsModKnot(string knotName) => knotName != null && GraftedKnots.Contains(knotName);

    /// <summary>True if a path like "Knot.stitch.0" points into a mod script.</summary>
    public static bool IsModPath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;
        int dot = path.IndexOf('.');
        return IsModKnot(dot < 0 ? path : path.Substring(0, dot));
    }

    internal static bool IsModContent(Container container) => IsModKnot(StoryGraft.KnotOf(container));

    internal static void Init(NeetMod kit)
    {
        _log = kit.Log;
        GameStory.WhenCreated(kit, OnStoryCreated);
    }

    private static void OnStoryCreated(Story story)
    {
        GraftedKnots.Clear();
        foreach (var script in Scripts)
            Apply(story, script);
    }

    private static void Apply(Story story, InkScript script)
    {
        if (script.Failed || script.GraftedInto == story)
            return;

        if (script.Json == null)
        {
            var result = InkCompiler.Compile(story, script.Name, script.Source);
            script.ProblemList.AddRange(result.Problems);
            foreach (string problem in result.Problems)
                _log.Warn($"{script.Name}: {problem}");
            if (!result.Ok)
            {
                script.Failed = true;
                return;
            }
            script.Json = result.Json;
        }

        try
        {
            var knots = StoryGraft.Add(story, script.Json);
            script.Grafted.Clear();
            script.Grafted.AddRange(knots);
            script.GraftedInto = story;
            script.KnotList.Clear();
            foreach (var knot in knots)
            {
                script.KnotList.Add(knot.Key);
                GraftedKnots.Add(knot.Key);
            }
        }
        catch (InvalidOperationException e)
        {
            script.Failed = true;
            script.ProblemList.Add("ERROR: " + e.Message);
            _log.Warn($"{script.Name}: {e.Message}");
        }
    }

    private static void Remove(InkScript script)
    {
        Scripts.Remove(script);
        var story = GameStory.Current;
        if (story != null && script.GraftedInto == story)
        {
            StoryGraft.Remove(story, script.Grafted);
            foreach (var knot in script.Grafted)
                GraftedKnots.Remove(knot.Key);
        }
        script.Grafted.Clear();
        script.GraftedInto = null;
    }
}
