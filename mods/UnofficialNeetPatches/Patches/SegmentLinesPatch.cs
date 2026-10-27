using System;
using HarmonyLib;
using UnofficialNeetPatches.Fixes;

namespace UnofficialNeetPatches.Patches;

/// <summary>
/// Dialogue.SwitchSegmentLine sets a character's routine by name without checking it exists. The bad
/// thief's delayed kill (scene data) switches Grandpa_NPC to "Thief_Intro_Blackout", but his routines
/// are Init, Thief_Intro and Blackout. BrainNeetAI.GetCurrentSegmentLine then throws in InitSegments,
/// and again whenever his behaviour tree reaches a target (SetDestinationReachedRot). A missing name
/// becomes the routine its name ends with ("Blackout"), or the switch is skipped and reported.
/// </summary>
[HarmonyPatch(typeof(Dialogue), nameof(Dialogue.SwitchSegmentLine), typeof(string), typeof(string))]
[HarmonyPatchCategory(SegmentLinesFix.FeatureId)]
internal static class SegmentLinesPatch
{
    // After LostCharacters' check, which skips the call when the character isn't in the house.
    [HarmonyPriority(Priority.Low)]
    private static bool Prefix(string characterName, ref string segmentLineName, bool __runOriginal)
    {
        if (!__runOriginal)
            return false;
        var house = HouseManager._;
        if (house?.characterStoriesDict == null || !house.characterStoriesDict.TryGetValue(characterName, out var stories) || stories == null)
            return true; // The game's own error for an unknown character.
        var brain = stories.GetComponent<BrainNeetAI>();
        if (brain?.segmentLines == null || Has(brain, segmentLineName))
            return true;

        foreach (var line in brain.segmentLines)
        {
            if (segmentLineName.EndsWith("_" + line.segmentLineName, StringComparison.Ordinal))
            {
                UnofficialNeetPatchesMod.Instance?.Log.Info($"{characterName} has no routine \"{segmentLineName}\"; using \"{line.segmentLineName}\".");
                segmentLineName = line.segmentLineName;
                return true;
            }
        }
        PatchReports.RoutineMissing(characterName, segmentLineName);
        return false;
    }

    private static bool Has(BrainNeetAI brain, string name)
    {
        foreach (var line in brain.segmentLines)
        {
            if (line.segmentLineName == name)
                return true;
        }
        return false;
    }
}
