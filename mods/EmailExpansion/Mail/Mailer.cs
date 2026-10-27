using System;
using System.Collections.Generic;
using NeetModKit.Conversations;
using NeetModKit.Events;
using NeetModKit.Rules;
using UnityEngine;

namespace EmailExpansion.Mail;

/// <summary>
/// Puts emails into the game's inbox: now, after some play time, or when their trigger comes up
/// (every few minutes at random, after a store order, when the house loads, after sleeping).
/// </summary>
internal static class Mailer
{
    private const float RetrySeconds = 10f;

    private static readonly List<MailItem> Candidates = new List<MailItem>();
    private static float _nextCheck;

    /// <summary>Whether emails arrive by themselves (switched by the "New emails arrive" feature).</summary>
    public static bool Enabled { get; set; }

    /// <summary>Sends <paramref name="mail"/> after <paramref name="delaySeconds"/> of play, or right away when the inbox is there.</summary>
    public static void Send(MailItem mail, float delaySeconds = 0f)
    {
        if (delaySeconds <= 0f && TrySendNow(mail))
            return;
        MailMemory.Current.Waiting.Add(mail.Id, Math.Max(delaySeconds, 1f));
    }

    public static void Cancel(MailItem mail) => MailMemory.Current.Waiting.Remove(mail.Id);

    public static bool IsWaiting(MailItem mail) => MailMemory.Current.Waiting.Contains(mail.Id);

    public static void OnHouseReady()
    {
        if (MailMemory.Current.NextRandomAt < 0f)
            PlanNextRandom();
        SendFor(TriggerRules.Arrive, null);
    }

    public static void OnWake() => SendFor(TriggerRules.Wake, null);

    public static void OnOrder(StoreOrder order) => SendFor(TriggerRules.Order, order.Store);

    /// <summary>Sends what's due. Checks once a second.</summary>
    public static void Tick()
    {
        if (Time.time < _nextCheck)
            return;
        _nextCheck = Time.time + 1f;
        if (!GameEvents.IsHouseReady)
            return;

        var state = MailMemory.Current;
        if (state.Waiting.Count > 0)
            state.Waiting.TakeDue(SendDue, RetrySeconds);

        if (state.NextRandomAt >= 0f && ModFlags.PlaySeconds >= state.NextRandomAt)
        {
            PlanNextRandom();
            if (Enabled)
            {
                var mail = Pick(TriggerRules.Random, null);
                if (mail != null)
                    Send(mail);
            }
        }
    }

    /// <summary>Could <paramref name="mail"/> arrive for this trigger right now?</summary>
    public static bool CanSend(MailItem mail, string trigger, string store = null) =>
        mail.Send != null && Triggers.CanFire(mail, mail.Send, trigger, store, MailMemory.SecondsSinceSent(mail.Id));

    private static bool TrySendNow(MailItem mail)
    {
        var handler = EmailHandler._;
        if (!GameEvents.IsHouseReady || handler == null || !InkStory.Email.HasKnot(mail.Knot))
            return false;
        handler.SendEmail(new KnotList.Knot
        {
            inkName = mail.Knot,
            title = mail.Subject,
            // The game doesn't use it; it tells apart two copies of the same email in the inbox.
            timeStamp = ModFlags.PlaySeconds,
        });
        MailMemory.RecordSent(mail.Id);
        return true;
    }

    private static bool SendDue(string id)
    {
        var mail = MailBook.Find(id);
        return mail == null || TrySendNow(mail); // An email whose file or mod is gone is dropped.
    }

    private static void SendFor(string trigger, string store)
    {
        if (!Enabled)
            return;
        var mail = Pick(trigger, store);
        if (mail != null)
            Send(mail, mail.Send.DelaySeconds);
    }

    private static MailItem Pick(string trigger, string store) =>
        Triggers.Pick(MailBook.All, mail => mail.Send, mail => !IsWaiting(mail) && CanSend(mail, trigger, store), Candidates);

    private static void PlanNextRandom()
    {
        var config = EmailExpansionMod.Config.Value;
        float min = Mathf.Max(1, config.MinMinutes);
        float max = Mathf.Max(min, config.MaxMinutes);
        MailMemory.Current.NextRandomAt = ModFlags.PlaySeconds + UnityEngine.Random.Range(min, max) * 60f;
    }
}
