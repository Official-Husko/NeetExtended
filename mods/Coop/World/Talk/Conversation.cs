using System.Collections.Generic;
using UnityEngine;

namespace Coop.World.Talk;

/// <summary>A remote player's conversation, run by the host: who, with whom, where the story is, and the last step's result.</summary>
internal sealed class Conversation
{
    public int Id;
    public byte Slot;
    public CharacterStories Speaker;
    public SpeakerRef Ref;
    public string Flow;
    public string Knot;
    public GameObject Offering;
    public bool LastLineChoice;

    // The talking player's lists for the story (PlayerMasks), from their latest request.
    public string Held = "";
    public string Body = "";

    // The last step.
    public bool HasLine;
    public string Text = "";
    public bool IsChoice;
    public bool More;
    public readonly List<string> Choices = new List<string>();
    public bool OfferingTime;
    // The line is said in the talker's own voice (a choice they picked), for the players watching.
    public bool PlayerVoice;
    // The watchers were told the choices came up.
    public bool ChoicesSent;
    public bool BucketOff;

    // How it ended, if it did.
    public bool Closed;
    public bool KeepText;
    public bool Left;

    // HostTalks.End ran (the game's bookkeeping is done).
    public bool Ended;

    public bool IsPhone => Ref.Kind == SpeakerRef.Phone;

    public void BeginStep()
    {
        HasLine = false;
        OfferingTime = false;
        BucketOff = false;
    }
}
