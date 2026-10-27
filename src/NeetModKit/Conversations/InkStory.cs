using System;
using System.Collections.Generic;
using Ink.Runtime;
using NeetLoader.Logging;
using NeetLoader.Runtime;
using NeetModKit.Events;

namespace NeetModKit.Conversations;

/// <summary>
/// One of the game's Ink stories. <see cref="Dialogue"/> holds every conversation, phone call and
/// NPC line; <see cref="Email"/> holds the text of every email on the computer. The game creates a
/// new copy of each whenever the house loads, so anything added to one is added again every time.
/// <see cref="InkScripts"/> and <see cref="InkFunctions"/> work with both.
/// </summary>
public sealed class InkStory
{
    private static ModLogger _log;
    private readonly Func<Story> _current;

    /// <summary>The dialogue story (Dialogue.masterStory), with the game's dialogue functions (GiveItem, AddMoney, ...).</summary>
    public static readonly InkStory Dialogue = new InkStory("dialogue",
        () => global::Dialogue._ != null ? global::Dialogue._.masterStory : null, GameInkFunctions.All);

    /// <summary>The email story (EmailHandler.masterStory). The game binds no functions to it.</summary>
    public static readonly InkStory Email = new InkStory("email",
        () => EmailHandler._ != null ? EmailHandler._.masterStory : null, Array.Empty<KeyValuePair<string, string[]>>());

    public static IReadOnlyList<InkStory> All { get; } = new[] { Dialogue, Email };

    private InkStory(string name, Func<Story> current, IReadOnlyList<KeyValuePair<string, string[]>> gameFunctions)
    {
        Name = name;
        _current = current;
        GameFunctions = gameFunctions;
        Created = new GameEvent<Story>(name + "StoryCreated");
    }

    /// <summary>"dialogue" or "email".</summary>
    public string Name { get; }

    /// <summary>The live story, or null when the scene doesn't have it.</summary>
    public Story Current => _current();

    /// <summary>Functions the game itself binds to this story, with their parameter names.</summary>
    public IReadOnlyList<KeyValuePair<string, string[]>> GameFunctions { get; }

    /// <summary>
    /// Raised when the game has created the story, before it runs and before a save is loaded into
    /// it. Mod scripts and functions are added here.
    /// </summary>
    public GameEvent<Story> Created { get; }

    /// <summary>Runs <paramref name="action"/> for every new copy of the story, and right away for the current one.</summary>
    public void WhenCreated(ITracker owner, Action<Story> action)
    {
        Created.Subscribe(owner, action);
        var story = Current;
        if (story == null)
            return;
        try { action(story); }
        catch (Exception e) { _log.Error($"A {Name} story handler threw", e); }
    }

    /// <summary>
    /// Whether the live story has the knot <paramref name="path"/> starts in ("Knot" or
    /// "Knot.stitch"). False without a story. Knots from mods that are gone fail this.
    /// </summary>
    public bool HasKnot(string path)
    {
        var story = Current;
        if (story == null || string.IsNullOrEmpty(path))
            return false;
        int dot = path.IndexOf('.');
        return story.mainContentContainer.namedContent.ContainsKey(dot < 0 ? path : path.Substring(0, dot));
    }

    public override string ToString() => Name + " story";

    internal static void Init(ModLogger log) => _log = log;

    internal void OnCreated(Story story) => Created.Raise(story, _log);
}
