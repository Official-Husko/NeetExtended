using System;
using Ink.Runtime;
using NeetLoader.Runtime;
using NeetModKit.Events;

namespace NeetModKit.Conversations;

/// <summary>
/// The game's dialogue story: one Ink story (<see cref="Dialogue.masterStory"/>) that holds every
/// conversation, phone call and NPC line. Short for <see cref="InkStory.Dialogue"/>.
/// </summary>
public static class GameStory
{
    /// <summary>The live story, or null when the scene has no dialogue.</summary>
    public static Story Current => InkStory.Dialogue.Current;

    /// <summary>
    /// Raised when the game has created its story, before it runs and before a save is loaded into
    /// it. Mod scripts and functions are added here.
    /// </summary>
    public static GameEvent<Story> Created => InkStory.Dialogue.Created;

    /// <summary>Runs <paramref name="action"/> for every new story, and right away for the current one.</summary>
    public static void WhenCreated(ITracker owner, Action<Story> action) => InkStory.Dialogue.WhenCreated(owner, action);
}
