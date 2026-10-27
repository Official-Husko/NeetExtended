using System;
using Coop.Net;

namespace Coop.World.Talk;

/// <summary>Host: the WorldTalkView messages (see <see cref="TalkProtocol"/>).</summary>
internal static class TalkSender
{
    /// <summary>The conversation's last step to its player; a closed conversation's bookkeeping follows.</summary>
    public static void Line(Conversation c)
    {
        var w = Outbox.Begin(MessageIds.WorldTalkView);
        w.Put(TalkProtocol.Line);
        w.Put(c.Id);
        c.Ref.Write(w);
        byte flags = 0;
        if (c.HasLine)
            flags |= TalkProtocol.HasText;
        if (c.IsChoice)
            flags |= TalkProtocol.IsChoice;
        if (c.More)
            flags |= TalkProtocol.More;
        if (c.Closed)
            flags |= TalkProtocol.Closed;
        if (c.KeepText)
            flags |= TalkProtocol.KeepText;
        if (c.Left)
            flags |= TalkProtocol.SpeakerLeft;
        if (c.OfferingTime)
            flags |= TalkProtocol.OfferingTime;
        w.Put(flags);
        if (c.HasLine)
            w.Put(c.Text);
        int count = Math.Min(c.Choices.Count, byte.MaxValue);
        w.Put((byte)count);
        for (int i = 0; i < count; i++)
            w.Put(c.Choices[i]);
        Outbox.ToSlot(c.Slot, w, Channel.Events);
        if (c.Closed)
            HostTalks.End(c, hangUp: false, tell: false);
    }

    public static void Close(Conversation c)
    {
        var w = Outbox.Begin(MessageIds.WorldTalkView);
        w.Put(TalkProtocol.Close);
        w.Put(c.Id);
        Outbox.ToSlot(c.Slot, w, Channel.Events);
    }

    public static void Busy(byte slot, SpeakerRef speaker, byte talker)
    {
        var w = Outbox.Begin(MessageIds.WorldTalkView);
        w.Put(TalkProtocol.Busy);
        speaker.Write(w);
        w.Put(BusyLine(talker));
        Outbox.ToSlot(slot, w, Channel.Events);
    }

    /// <summary>To everyone: the speaker walked off.</summary>
    public static void Left(SpeakerRef speaker)
    {
        if (!speaker.IsValid)
            return;
        var w = Outbox.Begin(MessageIds.WorldTalkView);
        w.Put(TalkProtocol.Left);
        speaker.Write(w);
        Outbox.ToPlayers(w, Channel.Events);
    }

    public static string BusyLine(byte talker) =>
        string.Format(TalkProtocol.BusyLines[UnityEngine.Random.Range(0, TalkProtocol.BusyLines.Length)], Speakers.NameOf(talker));
}
