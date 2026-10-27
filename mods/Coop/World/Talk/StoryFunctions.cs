using System;
using Coop.Net;
using LiteNetLib.Utils;

namespace Coop.World.Talk;

/// <summary>
/// Items that run a bit of the story when picked up or used (Usable.GoToDialogueFunctions, wired in
/// the scene). The story lives on the host, so a client's pickup sends the knot there; the host runs
/// it in the "Usable" flow like the game does, then goes back to whatever flow it was in, so a
/// conversation the host is having carries on.
///
/// WorldStoryFunction (client to host): the knot's full path.
/// </summary>
internal static class StoryFunctions
{
    private const string Flow = "Usable";
    private const string DefaultFlow = "DEFAULT_FLOW";

    public static void Init() => Messages.On(MessageIds.WorldStoryFunction, OnRequest);

    /// <summary>Client, before Usable.GoToDialogueFunctions: false keeps this game's copy of the story still.</summary>
    public static bool Before(string knot)
    {
        if (!WorldSide.Running || !WorldSide.IsClient)
            return true;
        if (!string.IsNullOrEmpty(knot))
        {
            var w = Outbox.Begin(MessageIds.WorldStoryFunction);
            w.Put(knot);
            Outbox.ToHost(w, Channel.Events);
        }
        return false;
    }

    private static void OnRequest(in Sender from, NetDataReader r)
    {
        string knot = r.GetString();
        if (!WorldSide.Running || !WorldSide.IsHost || Dialogue._ == null || TalkRunner.Current != null)
            return;
        var story = Dialogue._.masterStory;
        string before = story.currentFlowName;
        try
        {
            story.SwitchFlow(Flow);
            story.ChoosePathString(knot, true);
            story.Continue();
        }
        catch (Exception e)
        {
            CoopMod.Log.Warn($"World: player {from.Slot}'s item story \"{knot}\" failed here: {e.Message}");
        }
        finally
        {
            if (before == DefaultFlow)
                story.SwitchToDefaultFlow();
            else
                story.SwitchFlow(before);
        }
    }
}
