using System;
using System.Collections.Generic;
using Coop.Net;
using LiteNetLib.Utils;
using UnityEngine;

namespace Coop.World.Talk;

/// <summary>
/// Host: remote players' conversations. A client's start goes through the game's own
/// <c>Dialogue.InitCharacterDialogue</c>, so mods that route conversations (Phone Expansion's calls,
/// Unofficial NEET Patches' knot names) still have their say; World's last prefix then takes it over
/// for that player and <see cref="TalkRunner"/> runs it without the host's dialogue box.
/// One conversation per speaker: anyone else who tries gets a "busy" line, the host included.
/// </summary>
internal static class HostTalks
{
    private static readonly Dictionary<CharacterStories, Conversation> BySpeaker = new Dictionary<CharacterStories, Conversation>();
    private static readonly Dictionary<byte, Conversation> BySlot = new Dictionary<byte, Conversation>();

    private static int _nextId;

    // A start being carried out for a remote player, around the game's InitCharacterDialogue.
    private static byte _starting = WorldSide.None;
    private static string _held = "";
    private static string _body = "";

    public static void Clear()
    {
        BySpeaker.Clear();
        BySlot.Clear();
        _starting = WorldSide.None;
    }

    /// <summary>The slot talking to <paramref name="speaker"/>: the host's own player, a remote one, or none.</summary>
    public static byte TalkerOf(CharacterStories speaker)
    {
        if (speaker == null)
            return WorldSide.None;
        if (Dialogue._ != null && Dialogue._.currSpeaker == speaker && TalkRunner.Current == null)
            return WorldSide.HostSlot;
        return BySpeaker.TryGetValue(speaker, out var c) ? c.Slot : WorldSide.None;
    }

    /// <summary>The remote player's conversation with <paramref name="speaker"/>, or null.</summary>
    public static Conversation Of(CharacterStories speaker) => BySpeaker.TryGetValue(speaker, out var c) ? c : null;

    /// <summary>Runs <paramref name="start"/> (something that calls InitCharacterDialogue) as <paramref name="slot"/>'s start.</summary>
    public static void RunAs(byte slot, string held, string body, Action start)
    {
        _starting = slot;
        _held = held ?? "";
        _body = body ?? "";
        try
        {
            start();
        }
        finally
        {
            _starting = WorldSide.None;
        }
    }

    public static void OnRequest(in Sender from, NetDataReader r)
    {
        if (!WorldSide.IsHost || !WorldSide.Running || Dialogue._ == null)
            return;
        byte kind = r.GetByte();
        if (kind == TalkProtocol.Start)
        {
            var speaker = SpeakerRef.Read(r);
            byte how = r.GetByte();
            PlayerMasks.Read(r, from.Slot, out string held, out string body);
            StartFor(from.Slot, speaker, how, held, body);
            return;
        }
        int id = r.GetInt();
        if (!BySlot.TryGetValue(from.Slot, out var c) || c.Id != id)
            return;
        if (kind == TalkProtocol.Leave)
        {
            End(c, hangUp: true, tell: false);
            return;
        }
        int index = kind == TalkProtocol.Choose ? r.GetByte() : -1;
        PlayerMasks.Read(r, from.Slot, out c.Held, out c.Body);
        if (kind == TalkProtocol.Choose)
            TalkRunner.Pick(c, index);
        else if (c.More)
            TalkRunner.Advance(c);
        else if (c.IsPhone && c.Choices.Count == 0)
        {
            // Nothing left to say on the phone: hang up (what Phone Expansion does for the host).
            End(c, hangUp: true, tell: true);
            return;
        }
        else
            return;
        TalkSender.Line(c);
    }

    private static void StartFor(byte slot, SpeakerRef sref, byte how, string held, string body)
    {
        var speaker = Speakers.Resolve(sref);
        if (speaker == null || !speaker.enabled || Speakers.BodyOf(slot) == null || sref.Kind == SpeakerRef.Phone)
            return;
        BySlot.TryGetValue(slot, out var mine);
        if (how == TalkProtocol.Talk)
        {
            if (mine != null && mine.Speaker == speaker)
            {
                mine.HasLine = mine.Text.Length > 0;
                TalkSender.Line(mine);
                return;
            }
            if (speaker.leaving)
                return;
            byte other = TalkerOf(speaker);
            if (other != WorldSide.None && other != slot)
            {
                TalkSender.Busy(slot, sref, other);
                return;
            }
        }
        else
            Interrupt(speaker);

        string name = speaker.characterName;
        string flow, knot;
        bool restart;
        switch (how)
        {
            case TalkProtocol.Harm:
                if (speaker.noHarmResponse)
                    return;
                flow = name + "_Harm";
                knot = name + "Harm";
                restart = true;
                break;
            case TalkProtocol.Disgust:
                if (speaker.noDisgustResponse)
                    return;
                flow = name + "_Disgust";
                knot = name + "Disgust";
                restart = true;
                break;
            default:
                // CharacterStories.OnTriggerStay's knot, from the host's copy of the speaker.
                flow = name;
                knot = speaker.useFullName ? name
                    : name + (speaker.useNightId ? "Night" + EncounterSpawner.nightId : "") + (speaker.overrideKnot != "" ? "." + speaker.overrideKnot : "");
                restart = speaker.overrideKnotChanged;
                break;
        }
        if (mine != null)
            End(mine, hangUp: true, tell: true);
        RunAs(slot, held, body, () => Dialogue._.InitCharacterDialogue(speaker, flow, knot, restart, restart));
        if (how == TalkProtocol.Talk && BySpeaker.ContainsKey(speaker))
            speaker.overrideKnotChanged = false;
    }

    /// <summary>
    /// Host, the last prefix on <c>Dialogue.InitCharacterDialogue</c>: false when World takes the
    /// conversation (a remote player's) or turns it down (someone else is talking to the speaker).
    /// </summary>
    public static bool BeforeInit(CharacterStories speaker, string flow, string charKnot, bool enableCanvasOnly, bool forceChoosePath, bool runOriginal)
    {
        if (!WorldSide.Running || !WorldSide.IsHost || speaker == null)
            return true;
        if (TalkRunner.Current != null)
            return false;
        byte slot = _starting != WorldSide.None ? _starting : PhoneRequests.CallerOf(speaker);
        if (slot == WorldSide.None || slot == WorldSide.HostSlot)
            return HostStarts(speaker, flow);
        if (!runOriginal || Speakers.BodyOf(slot) == null)
            return false;

        if (BySlot.TryGetValue(slot, out var old))
            End(old, hangUp: old.Speaker != speaker, tell: false);
        var c = new Conversation
        {
            Id = ++_nextId,
            Slot = slot,
            Speaker = speaker,
            Ref = Speakers.Of(speaker),
            Flow = flow,
            Knot = charKnot,
            Held = _held,
            Body = _body,
        };
        if (!c.Ref.IsValid)
            return false;
        BySpeaker[speaker] = c;
        BySlot[slot] = c;
        if (speaker.bucket == null)
            speaker.bucket = Dialogue._.bucket;
        TalkRunner.Begin(c, enableCanvasOnly, forceChoosePath);
        TalkSender.Line(c);
        return false;
    }

    // The host's own player starts one: fine unless a remote player is talking to them.
    private static bool HostStarts(CharacterStories speaker, string flow)
    {
        if (!BySpeaker.TryGetValue(speaker, out var busy))
            return true;
        // Hurting them or a call coming in cuts the other player off, like the game closing the dialogue first.
        if (TalkProtocol.KindOf(flow) != TalkProtocol.Talk || busy.IsPhone)
        {
            End(busy, hangUp: false, tell: true);
            return true;
        }
        if (speaker.talkInput)
            TalkView.Toast(speaker, TalkSender.BusyLine(busy.Slot));
        return false;
    }

    /// <summary>Host, before a handset's ClearScreen: a remote player's call ends with it, and only the host's own call closes the host's dialogue box.</summary>
    public static void BeforeClearScreen(Phone_Interface phone, bool noDistanceCheck, ref bool endPhoneCall)
    {
        if (!WorldSide.Running || !WorldSide.IsHost || TalkRunner.Current != null)
            return;
        var speaker = phone.GetComponent<CharacterStories>();
        if (endPhoneCall && Dialogue._ != null && Dialogue._.currSpeaker != speaker)
            endPhoneCall = false;
        bool acts = noDistanceCheck || (phone.player != null && Vector3.Distance(phone.player.transform.position, phone.transform.position) < 2.2f);
        if (acts && speaker != null && BySpeaker.TryGetValue(speaker, out var c))
            End(c, hangUp: false, tell: true);
    }

    /// <summary>Host: true when a remote player is on <paramref name="phone"/> or dialing it (the host walking past leaves it alone).</summary>
    public static bool RemoteOnPhone(Phone_Interface phone)
    {
        var speaker = phone.GetComponent<CharacterStories>();
        if (speaker != null && BySpeaker.ContainsKey(speaker))
            return true;
        byte caller = speaker != null ? PhoneRequests.CallerOf(speaker) : WorldSide.None;
        return caller != WorldSide.None && caller != WorldSide.HostSlot;
    }

    /// <summary>Host, after CharacterStories.Leaving: everyone's copy stops taking conversations.</summary>
    public static void AfterLeaving(CharacterStories speaker)
    {
        if (!WorldSide.Running || !WorldSide.IsHost)
            return;
        if (BySpeaker.TryGetValue(speaker, out var c) && TalkRunner.Current != c)
            End(c, hangUp: false, tell: false);
        TalkSender.Left(Speakers.Of(speaker));
    }

    public static void PlayerLeft(byte slot)
    {
        if (BySlot.TryGetValue(slot, out var c))
            End(c, hangUp: true, tell: false);
        PhoneRequests.Forget(slot);
    }

    private static void Interrupt(CharacterStories speaker)
    {
        if (Dialogue._.currSpeaker == speaker)
            Dialogue._.CloseDialogue();
        else if (BySpeaker.TryGetValue(speaker, out var c))
            End(c, hangUp: false, tell: true);
    }

    /// <summary>A conversation is over: the game's bookkeeping CloseDialogue would do, and (when <paramref name="tell"/>) the talker's box closes.</summary>
    public static void End(Conversation c, bool hangUp, bool tell)
    {
        if (c.Ended)
            return;
        c.Ended = true;
        if (c.Speaker != null && BySpeaker.TryGetValue(c.Speaker, out var known) && known == c)
            BySpeaker.Remove(c.Speaker);
        if (BySlot.TryGetValue(c.Slot, out var current) && current == c)
            BySlot.Remove(c.Slot);
        var d = Dialogue._;
        if (d != null)
        {
            if (c.LastLineChoice)
                d.pausedFlowOnChoiceAnswer.Add(c.Flow);
            if (c.IsPhone)
                d.aliveFlowNames.Remove(c.Flow);
        }
        // CloseDialogue puts the bucket away.
        if (c.Speaker != null)
            Offerings.BucketOff(c);
        if (hangUp && c.Speaker != null && c.Speaker.TryGetComponent(out Phone_Interface phone))
            phone.ClearScreen(noDistanceCheck: true);
        if (tell && !c.Closed)
            TalkSender.Close(c);
        c.Closed = true;
    }
}
