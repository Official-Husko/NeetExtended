using System.Collections.Generic;
using UnityEngine;

namespace Coop.World.Talk.Watch;

/// <summary>What another player's conversation shows right now, as the last events described it.</summary>
internal sealed class WatchedTalk
{
    public SpeakerRef Ref;
    public byte Talker;
    public string Text = "";
    public bool PlayerVoice;
    public int Seed;
    public readonly List<string> Choices = new List<string>();
    public bool ChoicesShown;
    public int Picked = -1;

    /// <summary>The conversation ended but its last line stays up (the game's NPCReset) until the watcher walks off.</summary>
    public bool Lingering;

    private CharacterStories _speaker;
    private float _nextCheck;
    private bool _heard;

    public CharacterStories Speaker
    {
        get
        {
            if (_speaker == null)
                _speaker = Speakers.Resolve(Ref);
            return _speaker;
        }
    }

    /// <summary>Whether a listener at <paramref name="from"/> can take it in past walls (<see cref="Earshot"/>); checked four times a second.</summary>
    public bool Heard(Vector3 from, Vector3 speaker)
    {
        float now = Time.time;
        if (now >= _nextCheck)
        {
            _nextCheck = now + 0.25f;
            _heard = Earshot.Hears(speaker, from);
        }
        return _heard;
    }

    public void NewLine(byte talker, string text, bool playerVoice, int seed)
    {
        Talker = talker;
        Text = text;
        PlayerVoice = playerVoice;
        Seed = seed;
        Choices.Clear();
        ChoicesShown = false;
        Picked = -1;
        Lingering = false;
    }
}
