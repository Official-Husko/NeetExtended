using System.Collections.Generic;
using Coop.Net;

namespace Coop.World.Talk.Watch;

/// <summary>
/// Host: tells everyone what a conversation's box does (see <see cref="WatchProtocol"/>), for remote
/// players' conversations (from <see cref="TalkSender"/> and <see cref="HostTalks"/>) and the host's
/// own (from the hooks in <see cref="Patches.WatchPatches"/>). The host feeds its own copy
/// (<see cref="Watching"/>) in the same call, so it sees a conversation when everyone else does.
/// </summary>
internal static class WatchSender
{
    /// <summary>A remote player's conversation took a step: its line, if it said one.</summary>
    public static void Line(Conversation c)
    {
        c.ChoicesSent = false;
        if (c.HasLine)
            Show(c.Ref, c.Slot, c.PlayerVoice, c.Seed, c.Text);
    }

    /// <summary>The talker's choices came up (their game said so).</summary>
    public static void Choices(Conversation c)
    {
        if (c.ChoicesSent || c.More || c.Closed || c.Choices.Count == 0)
            return;
        c.ChoicesSent = true;
        Choices(c.Ref, c.Choices);
    }

    public static void Pick(Conversation c, int index)
    {
        if (index >= 0 && index < c.Choices.Count)
            Pick(c.Ref, index);
    }

    public static void Close(Conversation c) => Close(c.Ref, c.KeepText);

    public static void Show(SpeakerRef speaker, byte talker, bool playerVoice, int seed, string text)
    {
        if (!speaker.IsValid)
            return;
        Watching.OnShow(speaker, talker, playerVoice, seed, text);
        var w = Outbox.Begin(MessageIds.WorldTalkWatch);
        BeginShow(w, speaker, talker, playerVoice, seed, text);
        Outbox.ToPlayers(w, Channel.Events);
    }

    public static void Choices(SpeakerRef speaker, List<string> choices)
    {
        if (!speaker.IsValid)
            return;
        Watching.OnChoices(speaker, choices);
        var w = Outbox.Begin(MessageIds.WorldTalkWatch);
        BeginChoices(w, speaker, choices);
        Outbox.ToPlayers(w, Channel.Events);
    }

    public static void Pick(SpeakerRef speaker, int index)
    {
        if (!speaker.IsValid)
            return;
        Watching.OnPick(speaker, index);
        var w = Outbox.Begin(MessageIds.WorldTalkWatch);
        w.Put(WatchProtocol.Pick);
        speaker.Write(w);
        w.Put((byte)index);
        Outbox.ToPlayers(w, Channel.Events);
    }

    public static void Close(SpeakerRef speaker, bool keepText)
    {
        if (!speaker.IsValid)
            return;
        Watching.OnClose(speaker, keepText);
        var w = Outbox.Begin(MessageIds.WorldTalkWatch);
        w.Put(WatchProtocol.Close);
        speaker.Write(w);
        w.Put(keepText);
        Outbox.ToPlayers(w, Channel.Events);
    }

    /// <summary>A player just joined: where each conversation stands, so they start watching from the current line.</summary>
    public static void SendAllTo(byte slot)
    {
        foreach (var t in Watching.All)
        {
            if (t.Lingering)
                continue;
            var w = Outbox.Begin(MessageIds.WorldTalkWatch);
            BeginShow(w, t.Ref, t.Talker, t.PlayerVoice, t.Seed, t.Text);
            Outbox.ToSlot(slot, w, Channel.Events);
            if (!t.ChoicesShown)
                continue;
            w = Outbox.Begin(MessageIds.WorldTalkWatch);
            BeginChoices(w, t.Ref, t.Choices);
            Outbox.ToSlot(slot, w, Channel.Events);
            if (t.Picked < 0)
                continue;
            w = Outbox.Begin(MessageIds.WorldTalkWatch);
            w.Put(WatchProtocol.Pick);
            t.Ref.Write(w);
            w.Put((byte)t.Picked);
            Outbox.ToSlot(slot, w, Channel.Events);
        }
    }

    private static void BeginShow(LiteNetLib.Utils.NetDataWriter w, SpeakerRef speaker, byte talker, bool playerVoice, int seed, string text)
    {
        w.Put(WatchProtocol.Show);
        speaker.Write(w);
        w.Put(talker);
        w.Put(playerVoice ? WatchProtocol.PlayerVoice : (byte)0);
        w.Put(seed);
        w.Put(text ?? "");
    }

    private static void BeginChoices(LiteNetLib.Utils.NetDataWriter w, SpeakerRef speaker, List<string> choices)
    {
        w.Put(WatchProtocol.Choices);
        speaker.Write(w);
        int count = System.Math.Min(choices.Count, byte.MaxValue);
        w.Put((byte)count);
        for (int i = 0; i < count; i++)
            w.Put(choices[i]);
    }
}
