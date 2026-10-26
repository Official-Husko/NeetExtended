using System;
using System.Collections.Generic;
using System.Diagnostics;
using NeetLoader.Logging;
using NeetModKit.Game;

namespace UnofficialNeetPatches.Fixes;

/// <summary>
/// What the fixes caught, written to the error report (NeetLoader/logs/errors.log) so it can be
/// sent along with a bug report. Each entry says where in the game it happened.
/// </summary>
internal static class PatchReports
{
    private const string Source = "Unofficial NEET Patches";

    public static void StoryEventFailed(string sequence, Exception error) =>
        ErrorReport.Add(Source, $"Story event {sequence} failed and was stopped instead of repeating", error, Where());

    /// <summary><paramref name="call"/> is the game call that was skipped, like SwitchSegmentLine(Grandpa_NPC, Thief_Intro).</summary>
    public static void CharacterMissing(string character, string call) =>
        ErrorReport.Add(Source, $"Story event skipped: {character} isn't in the house", $"{call}\n{Where()}\nCalled from:\n{Caller()}");

    public static void GhostsDropped(List<string> things)
    {
        string list = string.Join(", ", things);
        UnofficialNeetPatchesMod.Instance?.Log.Info($"Left {things.Count} thing(s) that no longer exist out of the save: {list}");
        ErrorReport.Add(Source, $"Left things that no longer exist out of the save: {list}", Where());
    }

    /// <summary>"Usable Fuse" or "FireSpreader": the type, plus the item or object name when there is one.</summary>
    public static string Describe(object owner)
    {
        if (owner is Usable usable && !string.IsNullOrEmpty(usable.prefabName))
            return $"{owner.GetType().Name} {usable.prefabName}";
        if (owner is UnityEngine.Object unityObject && unityObject != null)
            return $"{owner.GetType().Name} {unityObject.name}";
        return owner?.GetType().Name ?? "a static save handler";
    }

    private static string Where()
    {
        var player = GameRefs.Player;
        string room = player != null ? Rooms.At(player.transform.position).ToString() : "no player";
        return $"Night {EncounterSpawner.nightId}, player in {room}.";
    }

    // Skips this class and the patch, so the first line is the game code that asked.
    private static string Caller() => new StackTrace(4, false).ToString().TrimEnd();
}
