using System;
using System.Collections.Generic;
using Ink.Runtime;
using NeetLoader;
using NeetLoader.Logging;
using NeetLoader.Runtime;

namespace NeetModKit.Conversations;

/// <summary>
/// Adds Ink scripts to the game's stories. A script's knots become part of the story, so the game
/// plays them like its own: in the dialogue story with the dialogue box's choices, tags and text
/// effects, in the email story as emails.
/// <code>
/// InkScripts.Add(this, "hello.ink", "=== MyMod_Hello ===\nHi there!\n-> DONE");
/// Dialogue._.InitCharacterDialogue(speaker, "MyMod", "MyMod_Hello");
/// </code>
/// The story's lists, global variables and functions (and anything in <see cref="InkFunctions"/>)
/// are available without declaring them. Scripts compile once, when the game first creates their
/// story, and are added again to every new copy. Prefix knot names with something unique.
/// </summary>
public static class InkScripts
{
    private static readonly List<InkScript> Scripts = new List<InkScript>();
    private static readonly Dictionary<InkStory, HashSet<string>> GraftedKnots = new Dictionary<InkStory, HashSet<string>>();
    private static ModLogger _log;

    public static IReadOnlyList<InkScript> All => Scripts;

    /// <summary>Adds a script to the dialogue story; it's removed again when <paramref name="owner"/> cleans up.</summary>
    public static InkScript Add(ITracker owner, string name, string inkSource) => Add(owner, InkStory.Dialogue, name, inkSource);

    /// <summary>Adds a script to <paramref name="target"/>; it's removed again when <paramref name="owner"/> cleans up.</summary>
    public static InkScript Add(ITracker owner, InkStory target, string name, string inkSource)
    {
        var script = new InkScript(target, name, inkSource ?? "");
        Scripts.Add(script);
        owner.Track(Disposable.Create(() => Remove(script)));

        var story = target.Current;
        if (story != null)
            Apply(story, script);
        return script;
    }

    /// <summary>True if the top-level knot <paramref name="knotName"/> came from a mod script in the current dialogue story.</summary>
    public static bool IsModKnot(string knotName) => IsModKnot(InkStory.Dialogue, knotName);

    /// <summary>True if the top-level knot <paramref name="knotName"/> came from a mod script in the current copy of <paramref name="story"/>.</summary>
    public static bool IsModKnot(InkStory story, string knotName) =>
        knotName != null && GraftedKnots.TryGetValue(story, out var knots) && knots.Contains(knotName);

    /// <summary>True if a path like "Knot.stitch.0" points into a mod script in the dialogue story.</summary>
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
        foreach (var target in InkStory.All)
        {
            var story = target;
            GraftedKnots[story] = new HashSet<string>(StringComparer.Ordinal);
            story.WhenCreated(kit, created => OnStoryCreated(story, created));
        }
    }

    private static void OnStoryCreated(InkStory target, Story story)
    {
        GraftedKnots[target].Clear();
        foreach (var script in Scripts)
        {
            if (script.Target == target)
                Apply(story, script);
        }
    }

    private static void Apply(Story story, InkScript script)
    {
        if (script.Failed || script.GraftedInto == story)
            return;

        if (script.Json == null)
        {
            var result = InkCompiler.Compile(script.Target, story, script.Name, script.Source);
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
                GraftedKnots[script.Target].Add(knot.Key);
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
        var story = script.Target.Current;
        if (story != null && script.GraftedInto == story)
        {
            StoryGraft.Remove(story, script.Grafted);
            foreach (var knot in script.Grafted)
                GraftedKnots[script.Target].Remove(knot.Key);
        }
        script.Grafted.Clear();
        script.GraftedInto = null;
    }
}
