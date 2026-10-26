using System.Collections.Generic;
using HarmonyLib;
using Ink.Runtime;

namespace NeetModKit.Conversations;

/// <summary>
/// Keeps saves loadable without the mods. Ink saves where every conversation ("flow") stopped, and
/// a position inside a mod script can't be found in a story without that script, which makes the
/// game fail to load the save. So right before saving, conversations that stopped inside mod
/// scripts are dropped; the next time they start from the top, as finished ones do anyway.
/// </summary>
internal static class SaveGuard
{
    private const string DefaultFlow = "DEFAULT_FLOW";

    private static readonly AccessTools.FieldRef<StoryState, Dictionary<string, Flow>> NamedFlows =
        AccessTools.FieldRefAccess<StoryState, Dictionary<string, Flow>>("_namedFlows");

    /// <summary>Runs after the mods' BeforeSave handlers, so they get the chance to end their own conversations first.</summary>
    public static void BeforeSave()
    {
        var dialogue = Dialogue._;
        var story = dialogue != null ? dialogue.masterStory : null;
        if (story == null || InkScripts.All.Count == 0)
            return;

        if (story.state.currentFlowName != DefaultFlow && IsModFlow(story.state.callStack, story.state.currentChoices))
        {
            KitMod.Instance?.Log.Info("Closing a mod conversation before saving.");
            dialogue.CloseDialogue();
        }

        var flows = NamedFlows(story.state);
        if (flows == null)
            return;

        List<string> drop = null;
        foreach (var pair in flows)
        {
            if (pair.Key != story.state.currentFlowName && IsModFlow(pair.Value.callStack, pair.Value.currentChoices))
                (drop ??= new List<string>()).Add(pair.Key);
        }
        if (drop == null)
            return;

        foreach (string name in drop)
        {
            story.RemoveFlow(name);
            dialogue.aliveFlowNames?.Remove(name);
            dialogue.pausedFlowOnChoiceAnswer?.Remove(name);
        }
    }

    private static bool IsModFlow(CallStack callStack, List<Choice> choices)
    {
        foreach (var element in callStack.elements)
        {
            if (InkScripts.IsModContent(element.currentPointer.container))
                return true;
        }
        if (choices != null)
        {
            foreach (var choice in choices)
            {
                if (InkScripts.IsModPath(choice.pathStringOnChoice))
                    return true;
            }
        }
        return false;
    }
}
