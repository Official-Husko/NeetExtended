using System.Collections.Generic;
using Coop.Net;
using LiteNetLib.Utils;
using UnityEngine;

namespace Coop.World.Talk;

/// <summary>
/// Client: this player's conversation, run by the host. The game's own detection starts it (Use at an
/// NPC, getting hurt or grossed out near one), but <c>InitCharacterDialogue</c> becomes a request
/// instead of running this game's copy of the story. Lines and choices come back and show in this
/// game's dialogue box (<see cref="TalkView"/>); Use and the choice buttons go back as requests.
/// <c>Dialogue.currSpeaker</c> is set to the speaker while it lasts, so the game's own checks
/// ("already talking to them") keep working.
/// </summary>
internal static class ClientTalk
{
    // The same speaker isn't asked again sooner than this (some NPCs try every physics step).
    private const float AskAgainAfter = 1f;

    private static readonly List<string> Choices = new List<string>();

    private static int _id;
    private static CharacterStories _speaker;
    private static bool _more;
    private static bool _closingHere;
    private static CharacterStories _asked;
    private static float _askedAt;
    private static BrainPlayer _brain;

    public static bool Talking => _speaker != null;

    public static void Clear()
    {
        _id = 0;
        _speaker = null;
        _asked = null;
        _brain = null;
        Choices.Clear();
    }

    /// <summary>Client, first prefix on <c>InitCharacterDialogue</c>: never runs here; the start goes to the host.</summary>
    public static bool BeforeInit(CharacterStories speaker, string flow)
    {
        if (!WorldSide.Running || !WorldSide.IsClient)
            return true;
        if (speaker == null || speaker == _speaker)
            return false;
        float now = Time.time;
        if (speaker == _asked && now < _askedAt + AskAgainAfter)
            return false;
        var sref = Speakers.Of(speaker);
        if (sref.Kind != SpeakerRef.Shared)
            return false;
        _asked = speaker;
        _askedAt = now;
        var w = Outbox.Begin(MessageIds.WorldTalk);
        w.Put(TalkProtocol.Start);
        sref.Write(w);
        w.Put(TalkProtocol.KindOf(flow));
        PlayerMasks.Write(w);
        Outbox.ToHost(w, Channel.Events);
        return false;
    }

    /// <summary>Client, before <c>Dialogue.MakeChoice</c>: the pick goes to the host.</summary>
    public static bool BeforeChoice(int choice)
    {
        if (!WorldSide.Running || !WorldSide.IsClient)
            return true;
        TalkView.HideChoices();
        if (_speaker != null && choice >= 0 && choice < Choices.Count)
        {
            var w = Begin(TalkProtocol.Choose);
            w.Put((byte)choice);
            PlayerMasks.Write(w);
            Outbox.ToHost(w, Channel.Events);
            Choices.Clear();
        }
        return false;
    }

    /// <summary>Client, before <c>Dialogue.CloseDialogue</c> (walked away, Use out of range): the host ends it too.</summary>
    public static void BeforeClose()
    {
        if (!WorldSide.Running || !WorldSide.IsClient || _closingHere || _speaker == null)
            return;
        Outbox.ToHost(Begin(TalkProtocol.Leave), Channel.Events);
        Forget();
    }

    /// <summary>Client, every frame: Use moves the conversation on, like <c>Dialogue.Update</c>.</summary>
    public static void Tick()
    {
        if (_speaker == null)
        {
            if (_id != 0)
                Close(keepText: false);
            return;
        }
        var brain = Brain();
        if (brain == null || !brain.intents.Use.Started || (Feedback_Sender._ != null && Feedback_Sender._.gameObject.activeInHierarchy))
            return;
        UsePressed();
    }

    /// <summary>Use in a conversation: the next line, the choices, or (out of range) goodbye.</summary>
    public static void UsePressed()
    {
        if (_speaker == null)
            return;
        if (!_speaker.playerInRange)
        {
            Dialogue._.CloseDialogue();
            return;
        }
        if (_more || (Choices.Count == 0 && _speaker.GetComponent<Phone_Interface>() != null))
        {
            var w = Begin(TalkProtocol.Advance);
            PlayerMasks.Write(w);
            Outbox.ToHost(w, Channel.Events);
        }
        else if (Choices.Count > 0)
            TalkView.ShowChoices(Choices);
    }

    public static void OnView(in Sender from, NetDataReader r)
    {
        if (!WorldSide.Running || !WorldSide.IsClient)
            return;
        byte kind = r.GetByte();
        switch (kind)
        {
            case TalkProtocol.Line:
                OnLine(r);
                break;
            case TalkProtocol.Close:
                if (r.GetInt() == _id)
                    Close(keepText: false);
                break;
            case TalkProtocol.Busy:
                var speaker = Speakers.Resolve(SpeakerRef.Read(r));
                string line = r.GetString();
                if (speaker != null && _speaker == null)
                    TalkView.Toast(speaker, line);
                break;
            case TalkProtocol.Left:
                OnLeft(Speakers.Resolve(SpeakerRef.Read(r)));
                break;
        }
    }

    private static void OnLine(NetDataReader r)
    {
        int id = r.GetInt();
        var speaker = Speakers.Resolve(SpeakerRef.Read(r));
        byte flags = r.GetByte();
        string text = (flags & TalkProtocol.HasText) != 0 ? r.GetString() : null;
        int count = r.GetByte();
        Choices.Clear();
        for (int i = 0; i < count; i++)
            Choices.Add(r.GetString());
        if (speaker == null)
            return;

        _id = id;
        _speaker = speaker;
        _more = (flags & TalkProtocol.More) != 0;
        _asked = null;
        var d = Dialogue._;
        d.currSpeaker = speaker;
        if (text != null)
            TalkView.ShowLine(speaker, text, (flags & TalkProtocol.IsChoice) != 0);
        if ((flags & TalkProtocol.OfferingTime) != 0)
            Offerings.Show(speaker, on: true);
        if ((flags & TalkProtocol.BucketOff) != 0)
            Offerings.Show(speaker, on: false);
        if ((flags & TalkProtocol.SpeakerLeft) != 0)
            MarkLeft(speaker);
        if ((flags & TalkProtocol.Closed) != 0)
            Close((flags & TalkProtocol.KeepText) != 0);
    }

    private static void OnLeft(CharacterStories speaker)
    {
        if (speaker == null)
            return;
        MarkLeft(speaker);
        if (speaker == _speaker)
            Close(keepText: true);
    }

    // What CharacterStories.Leaving does to the speaker's talk zone (the rest is the host's NPC).
    private static void MarkLeft(CharacterStories speaker)
    {
        speaker.leaving = true;
        if (speaker.TryGetComponent(out BoxCollider zone))
            zone.center = new Vector3(0f, -200f, 0f);
    }

    // The box closes, or (keepText) the speaker stops talking and the last line stays up, like the game's NPCReset.
    private static void Close(bool keepText)
    {
        var d = Dialogue._;
        if (d != null)
        {
            if (keepText)
            {
                TalkView.HideChoices();
                Offerings.Show(_speaker, on: false);
                d.NPCReset();
            }
            else
            {
                _closingHere = true;
                try
                {
                    d.CloseDialogue();
                }
                finally
                {
                    _closingHere = false;
                }
            }
        }
        Forget();
    }

    private static void Forget()
    {
        _id = 0;
        _speaker = null;
        _more = false;
        Choices.Clear();
    }

    private static NetDataWriter Begin(byte kind)
    {
        var w = Outbox.Begin(MessageIds.WorldTalk);
        w.Put(kind);
        w.Put(_id);
        return w;
    }

    private static BrainPlayer Brain()
    {
        if (_brain == null || _brain.gameObject != WorldSide.LocalPlayer)
        {
            var player = WorldSide.LocalPlayer;
            _brain = player != null ? player.GetComponent<BrainPlayer>() : null;
        }
        return _brain;
    }
}
