using System.Collections.Generic;
using UnityEngine;

namespace UnofficialNeetPatches.Fixes;

/// <summary>
/// HouseManager.characterStoriesDict maps a character's name to its CharacterStories, and story
/// events (SwitchSegmentLine, SetOverrideKnot) look characters up there. CharacterStories.Start
/// only adds a name that isn't in the map yet, so once the first object is destroyed the map keeps
/// pointing at it and a respawned copy is never added.
/// </summary>
internal static class CharacterRegistry
{
    private const string CloneSuffix = "(Clone)";
    private static readonly HashSet<string> Warned = new HashSet<string>();

    private static Dictionary<string, CharacterStories> Map =>
        HouseManager._ != null ? HouseManager._.characterStoriesDict : null;

    /// <summary>The name a character is registered under, as the game computes it.</summary>
    public static string KeyOf(string objectName)
    {
        int clone = objectName.IndexOf(CloneSuffix, System.StringComparison.Ordinal);
        return clone >= 0 ? objectName.Remove(clone) : objectName;
    }

    /// <summary>Drops the entry for <paramref name="key"/> if its object is gone, so the next one can register.</summary>
    public static void ForgetIfGone(string key)
    {
        var map = Map;
        if (map != null && map.TryGetValue(key, out var stories) && stories == null)
            map.Remove(key);
    }

    /// <summary>Points every entry whose object is gone at a living copy of the character, or removes it.</summary>
    public static void Repair()
    {
        var map = Map;
        if (map == null)
            return;

        List<string> gone = null;
        foreach (var pair in map)
        {
            if (pair.Value == null)
                (gone ??= new List<string>()).Add(pair.Key);
        }
        if (gone == null)
            return;

        var living = new Dictionary<string, CharacterStories>();
        foreach (var stories in Object.FindObjectsOfType<CharacterStories>())
        {
            string key = KeyOf(stories.gameObject.name);
            if (!living.ContainsKey(key))
                living[key] = stories;
        }

        foreach (string key in gone)
        {
            if (living.TryGetValue(key, out var stories))
            {
                map[key] = stories;
                UnofficialNeetPatchesMod.Instance?.Log.Info($"{key} is back in the story.");
            }
            else
            {
                map.Remove(key);
            }
        }
    }

    /// <summary>
    /// True if a story event can use <paramref name="name"/> right now. Otherwise the event is skipped
    /// (with one warning per character) instead of throwing.
    /// </summary>
    public static bool IsThere(string name, string call, bool needsBrain)
    {
        var map = Map;
        if (map != null && map.TryGetValue(name, out var stories) && stories != null &&
            (!needsBrain || stories.GetComponent<BrainNeetAI>() != null))
            return true;

        if (Warned.Add(name))
            UnofficialNeetPatchesMod.Instance?.Log.Warn($"Skipped a story event for {name}: that character isn't in the house (details in errors.log).");
        PatchReports.CharacterMissing(name, call);
        return false;
    }
}
