using System;
using Ink.Runtime;
using NeetLoader.Logging;
using NeetLoader.Runtime;
using NeetModKit.Events;

namespace NeetModKit.Conversations;

/// <summary>
/// The game's dialogue story: one Ink story (<see cref="Dialogue.masterStory"/>) that holds every
/// conversation, phone call and NPC line. The game creates a new one whenever a scene with
/// dialogue loads, so anything added to it has to be added again each time.
/// </summary>
public static class GameStory
{
    private static ModLogger _log;

    /// <summary>The live story, or null when the scene has no dialogue.</summary>
    public static Story Current => Dialogue._ != null ? Dialogue._.masterStory : null;

    /// <summary>
    /// Raised when the game has created its story, before it runs and before a save is loaded into
    /// it. Mod scripts and functions are added here.
    /// </summary>
    public static readonly GameEvent<Story> Created = new GameEvent<Story>(nameof(Created));

    /// <summary>Runs <paramref name="action"/> for every new story, and right away for the current one.</summary>
    public static void WhenCreated(ITracker owner, Action<Story> action)
    {
        Created.Subscribe(owner, action);
        var story = Current;
        if (story == null)
            return;
        try { action(story); }
        catch (Exception e) { _log.Error("A story handler threw", e); }
    }

    internal static void Init(ModLogger log) => _log = log;

    internal static void OnCreated(Story story) => Created.Raise(story, _log);
}
