using System.Collections.Generic;

namespace Coop.World.Talk.Watch;

/// <summary>What another player's conversation shows right now, as the last events described it.</summary>
internal sealed class WatchedTalk
{
    public SpeakerRef Ref;
    public byte Talker;
    public string Text = "";
    public bool PlayerVoice;
    public readonly List<string> Choices = new List<string>();
    public bool ChoicesShown;
    public int Picked = -1;

    /// <summary>The conversation ended but its last line stays up (the game's NPCReset) until the watcher walks off.</summary>
    public bool Lingering;

    private CharacterStories _speaker;

    public CharacterStories Speaker
    {
        get
        {
            if (_speaker == null)
                _speaker = Speakers.Resolve(Ref);
            return _speaker;
        }
    }

    public void NewLine(byte talker, string text, bool playerVoice)
    {
        Talker = talker;
        Text = text;
        PlayerVoice = playerVoice;
        Choices.Clear();
        ChoicesShown = false;
        Picked = -1;
        Lingering = false;
    }
}
