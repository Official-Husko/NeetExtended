using System;
using System.Collections.Generic;
using HarmonyLib;
using NeetModKit.Conversations;
using UnofficialNeetPatches.Fixes;

namespace UnofficialNeetPatches.Patches;

/// <summary>
/// CharacterStories asks for characterName + "Harm" / "Disgust" (CharacterStories.cs:212,217). Poopman's
/// characterName is "Poopman", but his knots are PoopManHarm and PoopManDisgust, so ChoosePathString
/// fails and Dialogue.InitCharacterDialogue swallows it (Dialogue.cs:410-416). When the knot asked
/// for doesn't exist, this uses one whose name only differs in upper and lower case.
/// </summary>
[HarmonyPatch(typeof(Dialogue), nameof(Dialogue.InitCharacterDialogue))]
[HarmonyPatchCategory(KnotNamesFix.FeatureId)]
internal static class KnotNamesPatch
{
    private static void Prefix(ref string charKnot)
    {
        if (string.IsNullOrEmpty(charKnot) || InkStory.Dialogue.HasKnot(charKnot))
            return;
        var story = InkStory.Dialogue.Current;
        if (story == null)
            return;
        int dot = charKnot.IndexOf('.');
        string knot = dot < 0 ? charKnot : charKnot.Substring(0, dot);
        foreach (KeyValuePair<string, Ink.Runtime.INamedContent> named in story.mainContentContainer.namedContent)
        {
            if (string.Equals(named.Key, knot, StringComparison.OrdinalIgnoreCase))
            {
                charKnot = named.Key + (dot < 0 ? "" : charKnot.Substring(dot));
                return;
            }
        }
    }
}
