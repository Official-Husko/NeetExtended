using System;
using System.Collections.Generic;
using NeetModKit.Events;
using NeetModKit.Rules;
using UnityEngine;
using Random = UnityEngine.Random;

namespace PhoneExpansion.Calls;

/// <summary>
/// Calls that ring in. When the game's own random call rings, a modded call can take its place;
/// calls can also be rung on purpose (now or later), after a store order, or when the house loads.
/// </summary>
internal static class IncomingCalls
{
    private const float RetrySeconds = 15f;

    private static readonly List<PhoneCall> Candidates = new List<PhoneCall>();
    private static int _roundFrame = -1;
    private static PhoneCall _forced;
    private static PhoneCall _ringing;
    private static float _nextCheck;

    /// <summary>Whether modded calls ring in by themselves (switched by the "New calls ring in" feature).</summary>
    public static bool Enabled { get; set; }

    /// <summary>The modded call ringing right now, or null when it's one of the game's.</summary>
    public static PhoneCall Ringing => _ringing;

    /// <summary>Called for each handset as it starts ringing. All handsets ring in the same frame.</summary>
    public static void OnPhoneStartsRinging(Phone_Interface phone)
    {
        if (Time.frameCount != _roundFrame)
        {
            _roundFrame = Time.frameCount;
            _ringing = _forced ?? PickRandom();
            _forced = null;
        }
        CallerScreen.Show(phone, _ringing != null ? _ringing.ScreenText : CallerScreen.Unknown);
    }

    /// <summary>The player picked up: hands over the modded call that was ringing, if any.</summary>
    public static PhoneCall TakeAnswered()
    {
        var call = _ringing;
        _ringing = null;
        return call;
    }

    /// <summary>Rings <paramref name="call"/> after <paramref name="delaySeconds"/> of play, or as soon as the line is free.</summary>
    public static void Ring(PhoneCall call, float delaySeconds)
    {
        if (delaySeconds <= 0f && TryRingNow(call))
            return;
        Schedule(call, Math.Max(delaySeconds, 1f));
    }

    public static void Cancel(PhoneCall call) =>
        PhoneMemory.Current.Scheduled.RemoveAll(s => string.Equals(s.CallId, call.Id, StringComparison.OrdinalIgnoreCase));

    public static void OnOrder(string store)
    {
        if (!Enabled)
            return;
        var call = Pick(IncomingRules.Order, store);
        if (call != null)
            Ring(call, call.Incoming.DelaySeconds);
    }

    public static void OnHouseReady()
    {
        _ringing = null;
        if (!Enabled)
            return;
        var call = Pick(IncomingRules.Arrive, null);
        if (call != null)
            Ring(call, call.Incoming.DelaySeconds);
    }

    /// <summary>Rings scheduled calls that are due. Checks once a second.</summary>
    public static void Tick()
    {
        var scheduled = PhoneMemory.Current.Scheduled;
        if (scheduled.Count == 0 || Time.time < _nextCheck)
            return;
        _nextCheck = Time.time + 1f;
        if (!GameEvents.IsHouseReady)
            return;

        float now = ModFlags.PlaySeconds;
        for (int i = 0; i < scheduled.Count; i++)
        {
            if (scheduled[i].DueAt > now)
                continue;
            var call = PhoneBook.Find(scheduled[i].CallId);
            if (call == null)
            {
                scheduled.RemoveAt(i--); // Its file or mod is gone.
                continue;
            }
            if (TryRingNow(call))
            {
                scheduled.RemoveAt(i);
                return; // One at a time; the line is busy now anyway.
            }
            scheduled[i].DueAt = now + RetrySeconds;
        }
    }

    /// <summary>Could <paramref name="call"/> ring for this trigger right now?</summary>
    public static bool CanRing(PhoneCall call, string trigger, string store = null)
    {
        var rules = call.Incoming;
        if (rules == null || rules.Trigger != trigger)
            return false;
        if (store != null && rules.Store != null && !string.Equals(rules.Store, store, StringComparison.OrdinalIgnoreCase))
            return false;
        if (rules.Once && PhoneMemory.TimesPlayed(call.Id) > 0)
            return false;
        if (rules.CooldownMinutes > 0f && PhoneMemory.SecondsSincePlayed(call.Id) < rules.CooldownMinutes * 60f)
            return false;
        return rules.When == null || rules.When.Test(call);
    }

    private static PhoneCall PickRandom()
    {
        var config = PhoneExpansionMod.Config.Value;
        if (!Enabled || Random.value * 100f >= config.NewCallChance)
            return null;
        return Pick(IncomingRules.Random, null);
    }

    private static PhoneCall Pick(string trigger, string store)
    {
        Candidates.Clear();
        float total = 0f;
        foreach (var call in PhoneBook.All)
        {
            if (!CanRing(call, trigger, store) || call.Incoming.Weight <= 0f)
                continue;
            Candidates.Add(call);
            total += call.Incoming.Weight;
        }

        float roll = Random.value * total;
        foreach (var call in Candidates)
        {
            roll -= call.Incoming.Weight;
            if (roll <= 0f)
                return call;
        }
        return Candidates.Count > 0 ? Candidates[Candidates.Count - 1] : null;
    }

    private static bool TryRingNow(PhoneCall call)
    {
        if (!GameEvents.IsHouseReady || !PhoneLine.IsIdle)
            return false;
        try
        {
            _forced = call;
            PhoneLine.RingAll(Random.Range(20f, 30f));
            return true;
        }
        finally
        {
            _forced = null;
        }
    }

    private static void Schedule(PhoneCall call, float seconds)
    {
        var scheduled = PhoneMemory.Current.Scheduled;
        foreach (var entry in scheduled)
        {
            if (string.Equals(entry.CallId, call.Id, StringComparison.OrdinalIgnoreCase))
                return; // Already waiting to ring.
        }
        scheduled.Add(new PhoneMemory.ScheduledRing { CallId = call.Id, DueAt = ModFlags.PlaySeconds + seconds });
    }
}
