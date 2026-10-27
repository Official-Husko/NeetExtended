using System;
using System.Collections.Generic;
using UnityEngine;

namespace Coop.World.Talk;

/// <summary>
/// Host: runs a remote player's conversation in the game's own story, without the host's dialogue
/// box. Each step swaps that conversation in (its Ink flow, and the speaker and offering in
/// <c>Dialogue</c> that the story's functions use), runs what <c>Dialogue.InitCharacterDialogue</c>,
/// <c>AdvanceStory</c> or <c>MakeChoice</c> would, and swaps the host's own conversation back.
/// The host can be talking to someone else meanwhile; only one step runs at a time.
/// </summary>
internal static class TalkRunner
{
    private const string DefaultFlow = "DEFAULT_FLOW";

    private static readonly List<string> Tags = new List<string>();

    /// <summary>The conversation a step is running for, or null between steps.</summary>
    public static Conversation Current { get; private set; }

    // The host's own state while a step runs.
    private static string _flow;
    private static CharacterStories _speaker;
    private static string _knot;
    private static GameObject _offering;
    private static AudioSource _audio;
    private static GameObject _playerRef;
    private static Brain _playerBrain;

    /// <summary>InitCharacterDialogue's story part: start the knot, or pick the flow up where it stopped.</summary>
    public static void Begin(Conversation c, bool enableCanvasOnly, bool forceChoosePath)
    {
        Run(c, () =>
        {
            var d = Dialogue._;
            var story = d.masterStory;
            bool first = !d.aliveFlowNames.Contains(c.Flow);
            if (first)
            {
                Choose(c.Knot);
                d.aliveFlowNames.Add(c.Flow);
            }
            if (forceChoosePath)
            {
                Choose(c.Knot);
                if (!first && story.canContinue)
                    story.Continue();
            }
            if (first)
                Step(c, isChoice: false, again: false);
            else if (!enableCanvasOnly && d.pausedFlowOnChoiceAnswer.Remove(story.currentFlowName))
                Step(c, isChoice: true, again: true);
            else
                Step(c, isChoice: false, again: true);
        });
    }

    /// <summary>Use pressed in the conversation: the next line, if there is one.</summary>
    public static void Advance(Conversation c)
    {
        Run(c, () =>
        {
            if (Dialogue._.masterStory.canContinue)
                Step(c, isChoice: false, again: false);
        });
    }

    /// <summary>MakeChoice: the answer, then the line it leads to.</summary>
    public static void Pick(Conversation c, int index)
    {
        Run(c, () =>
        {
            var story = Dialogue._.masterStory;
            if (index < 0 || index >= story.currentChoices.Count)
                return;
            story.ChooseChoiceIndex(index);
            c.Choices.Clear();
            if (story.canContinue)
                Step(c, isChoice: true, again: false);
        });
    }

    /// <summary>ProcessOffering's story part: the offering's lists for the story, then the knot's "got something" branch.</summary>
    public static void Offered(Conversation c, GameObject item)
    {
        c.Offering = item;
        Run(c, () =>
        {
            var story = Dialogue._.masterStory;
            var offering = item.GetComponent<Offering>();
            story.variablesState["tempCurrOffering"] = offering.OfferingStatsToInkList(story);
            if (offering.isMoney && item.TryGetComponent(out Usable usable))
                story.variablesState["offeringValue"] = usable.price;
            Offerings.BucketOff(c);
            Choose(c.Knot + ".got_something");
            Step(c, isChoice: false, again: false);
        });
    }

    /// <summary>The conversation ends from inside a step (a tag, or something the story's functions closed).</summary>
    public static void Close(Conversation c, bool keepText = false, bool left = false)
    {
        c.Closed = true;
        c.KeepText |= keepText;
        c.Left |= left;
    }

    private static void Run(Conversation c, Action step)
    {
        if (c.Closed || c.Speaker == null || Dialogue._ == null)
            return;
        c.BeginStep();
        Enter(c);
        try
        {
            step();
            Peek(c);
        }
        catch (Exception e)
        {
            CoopMod.Log.Warn($"World: {Speakers.NameOf(c.Slot)}'s conversation with {c.Speaker.name} ({c.Knot}) broke: {e.Message}");
            Close(c);
            Peek(c);
        }
        finally
        {
            Exit(c);
        }
    }

    // AdvanceStory without the dialogue box: the line, its tags, then what's left to say or pick.
    private static void Step(Conversation c, bool isChoice, bool again)
    {
        var d = Dialogue._;
        var story = d.masterStory;
        PlayerMasks.Apply(story, c.Held, c.Body);
        if (again)
            Line(c, story.currentText, isChoice);
        else if (story.canContinue)
            Line(c, story.Continue(), isChoice);

        // A copy: DeniedOffering moves the story on while the tags are read.
        Tags.Clear();
        Tags.AddRange(story.currentTags);
        bool audioTag = false, playerTag = false;
        foreach (string tag in Tags)
        {
            if (Starts(tag, "SpeakerAudio:"))
            {
                audioTag = true;
                playerTag = tag.Substring(13) == "Player";
            }
        }
        // AdvanceStory's choice of voice: a picked choice is the player's unless a tag says the speaker's.
        c.PlayerVoice = isChoice && (!audioTag || playerTag);
        foreach (string tag in Tags)
        {
            if (c.Closed)
                break;
            if (Starts(tag, "OfferingTime"))
                Offerings.BucketOn(c);
            else if (Starts(tag, "DeniedOffering"))
                Denied(c);
            else if (Starts(tag, "Leaving"))
            {
                c.Speaker.Leaving();
                Close(c, left: true);
            }
            else if (Starts(tag, "ClearCharacter"))
                Close(c);
            else if (Starts(tag, "EndPhoneCall"))
            {
                if (c.Speaker.TryGetComponent(out Phone_Interface phone))
                    phone.ClearScreen(noDistanceCheck: true);
                Close(c);
            }
            else if (Starts(tag, "NextBrainSegment"))
                NextSegment(c, tag);
        }
        if (!c.Closed)
            c.LastLineChoice = isChoice;
    }

    private static void Line(Conversation c, string text, bool isChoice)
    {
        c.HasLine = true;
        c.PlayerVoice = false;
        c.Text = text ?? "";
        c.IsChoice = isChoice;
    }

    // Dialogue.DeniedOffering: the knot's "got nothing" branch, then the speaker is done talking.
    private static void Denied(Conversation c)
    {
        if (string.IsNullOrEmpty(c.Knot))
            return;
        var story = Dialogue._.masterStory;
        Choose(c.Knot + ".got_nothing");
        bool cleared = false;
        if (story.canContinue)
        {
            Line(c, story.Continue(), false);
            foreach (string tag in story.currentTags)
            {
                if (Starts(tag, "Leaving"))
                {
                    c.Speaker.Leaving();
                    c.Left = true;
                    cleared = true;
                }
                else if (Starts(tag, "ClearCharacter"))
                    cleared = true;
            }
        }
        // NPCReset leaves the last line up; a Leaving or ClearCharacter line closed the box first.
        Close(c, keepText: !cleared);
    }

    private static void NextSegment(Conversation c, string tag)
    {
        int steps = 1;
        if (tag.Length > 16)
            int.TryParse(tag.Substring(16), out steps);
        if (c.Speaker.TryGetComponent(out BrainNeetAI brain))
            brain.IncrementSegments(steps);
        var story = Dialogue._.masterStory;
        if (story.canContinue)
            story.Continue();
    }

    // After the step, still in the conversation's flow: what the player can do next.
    private static void Peek(Conversation c)
    {
        c.Choices.Clear();
        c.More = false;
        if (c.Closed)
            return;
        var story = Dialogue._.masterStory;
        c.More = story.canContinue;
        if (c.More)
            return;
        foreach (var choice in story.currentChoices)
            c.Choices.Add(choice?.text ?? "");
    }

    private static void Choose(string knot)
    {
        try
        {
            Dialogue._.masterStory.ChoosePathString(knot, true);
        }
        catch (Exception) when (knot.IndexOf("Harm", StringComparison.Ordinal) >= 0 || knot.IndexOf("Disgust", StringComparison.Ordinal) >= 0)
        {
            // Like the game: speakers without a Harm or Disgust knot just don't answer.
        }
    }

    private static bool Starts(string tag, string name) => tag.StartsWith(name, StringComparison.Ordinal);

    private static void Enter(Conversation c)
    {
        var d = Dialogue._;
        _flow = d.masterStory.currentFlowName;
        _speaker = d.currSpeaker;
        _knot = d.currKnot;
        _offering = d.currentOffering;
        _audio = d.currentAudio;
        _playerRef = c.Speaker.playerRef;
        _playerBrain = c.Speaker.playerBrain;

        d.currSpeaker = c.Speaker;
        d.currKnot = c.Knot;
        d.currentOffering = c.Offering;
        // Story functions that look at "the player" through the speaker find the talker's body.
        var body = Speakers.BodyOf(c.Slot);
        if (body != null)
            c.Speaker.playerRef = body;
        d.masterStory.SwitchFlow(c.Flow);
        Current = c;
    }

    private static void Exit(Conversation c)
    {
        Current = null;
        var d = Dialogue._;
        c.Offering = d.currentOffering;
        if (_flow == DefaultFlow)
            d.masterStory.SwitchToDefaultFlow();
        else
            d.masterStory.SwitchFlow(_flow);
        d.currSpeaker = _speaker;
        d.currKnot = _knot;
        d.currentOffering = _offering;
        d.currentAudio = _audio;
        if (c.Speaker != null)
        {
            c.Speaker.playerRef = _playerRef;
            c.Speaker.playerBrain = _playerBrain;
        }
        _speaker = null;
        _offering = null;
        _audio = null;
        _playerRef = null;
        _playerBrain = null;
    }
}
