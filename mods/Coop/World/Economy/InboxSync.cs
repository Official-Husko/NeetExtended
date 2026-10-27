using System.Collections.Generic;
using Coop.Net;
using LiteNetLib.Utils;

namespace Coop.World.Economy;

/// <summary>
/// The computer's inbox. Emails arrive on the host (the game's spam, Email Expansion's mail); each
/// one is passed on, and a client puts it into its own inbox the same way the game does, so the inbox
/// fills, beeps and opens as usual. A player who joins or resyncs gets the whole list, in case mail
/// came in while they were loading. Reading is per player.
///
/// WorldEmail: mode (New or All), count, per email: knot, subject, time stamp, accessible.
/// </summary>
internal static class InboxSync
{
    private const byte New = 0;
    private const byte All = 1;

    // Client, for a full list: how many of each email the inbox here already has.
    private static readonly Dictionary<(string, string, float), int> Have = new Dictionary<(string, string, float), int>();
    private static bool _applying;

    public static void Init() => Messages.On(MessageIds.WorldEmail, OnEmail);

    /// <summary>Host, after the game put an email into its inbox.</summary>
    public static void Sent(KnotList.Knot mail)
    {
        if (_applying || mail == null || !WorldSide.Running || !WorldSide.IsHost)
            return;
        var w = Outbox.Begin(MessageIds.WorldEmail);
        w.Put(New);
        w.Put(1);
        Put(w, mail);
        Outbox.ToPlayers(w, Channel.Events);
    }

    /// <summary>Host: everything in the inbox for one player.</summary>
    public static void SendAllTo(byte slot)
    {
        var inbox = EmailHandler._;
        if (inbox == null)
            return;
        var w = Outbox.Begin(MessageIds.WorldEmail);
        w.Put(All);
        w.Put(inbox.deliveredEmails.Count + inbox.pendingEmails.Count);
        foreach (var mail in inbox.deliveredEmails)
            Put(w, mail);
        foreach (var mail in inbox.pendingEmails)
            Put(w, mail);
        Outbox.ToSlot(slot, w, Channel.Events);
    }

    private static void OnEmail(in Sender from, NetDataReader reader)
    {
        var inbox = EmailHandler._;
        if (WorldSide.IsHost || !WorldSide.Running || inbox == null)
            return;
        bool all = reader.GetByte() == All;
        int count = reader.GetInt();
        if (all)
            Remember(inbox);
        _applying = true;
        try
        {
            for (int i = 0; i < count; i++)
            {
                var mail = Get(reader);
                // In a full list, what's already here (from the house's save, an earlier list) is skipped.
                var key = (mail.inkName, mail.title, mail.timeStamp);
                if (all && Have.TryGetValue(key, out int here) && here > 0)
                {
                    Have[key] = here - 1;
                    continue;
                }
                inbox.SendEmail(mail);
            }
        }
        finally
        {
            _applying = false;
        }
    }

    private static void Remember(EmailHandler inbox)
    {
        Have.Clear();
        foreach (var mail in inbox.deliveredEmails)
            Count(mail);
        foreach (var mail in inbox.pendingEmails)
            Count(mail);
    }

    private static void Count(KnotList.Knot mail)
    {
        var key = (mail.inkName, mail.title, mail.timeStamp);
        Have.TryGetValue(key, out int here);
        Have[key] = here + 1;
    }

    private static void Put(NetDataWriter w, KnotList.Knot mail)
    {
        w.Put(mail.inkName ?? "");
        w.Put(mail.title ?? "");
        w.Put(mail.timeStamp);
        w.Put(mail.accessible);
    }

    private static KnotList.Knot Get(NetDataReader r) => new KnotList.Knot
    {
        inkName = r.GetString(),
        title = r.GetString(),
        timeStamp = r.GetFloat(),
        accessible = r.GetBool(),
    };
}
