using UnityEngine;

namespace Coop.World.Talk;

/// <summary>
/// Puts a remote player's conversation into the game's dialogue state for the length of a step
/// (<see cref="TalkRunner"/>) and the host's own back afterwards: its Ink flow, the speaker, knot,
/// offering and voice source, and who "the player" is for the speaker's story functions.
/// </summary>
internal static class TalkSwap
{
    private const string DefaultFlow = "DEFAULT_FLOW";

    // The host's own state while a step runs.
    private static string _flow;
    private static CharacterStories _speaker;
    private static string _knot;
    private static GameObject _offering;
    private static AudioSource _audio;
    private static GameObject _playerRef;
    private static Brain _playerBrain;

    public static void Enter(Conversation c)
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
    }

    public static void Exit(Conversation c)
    {
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
