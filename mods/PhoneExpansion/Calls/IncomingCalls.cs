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

    public static void Cancel(PhoneCall call) => PhoneMemory.Current.Rings.Remove(call.Id);

    public static void OnOrder(string store) => RingFor(IncomingRules.Order, store);

    public static void OnHouseReady()
    {
        _ringing = null;
        RingFor(IncomingRules.Arrive, null);
    }

    public static void OnWake() => RingFor(IncomingRules.Wake, null);

    /// <summary>Rings scheduled calls that are due. Checks once a second.</summary>
    public static void Tick()
    {
        var rings = PhoneMemory.Current.Rings;
        if (rings.Count == 0 || Time.time < _nextCheck)
            return;
        _nextCheck = Time.time + 1f;
        if (GameEvents.IsHouseReady)
            rings.TakeDue(RingDue, RetrySeconds);
    }

    /// <summary>Could <paramref name="call"/> ring for this trigger right now?</summary>
    public static bool CanRing(PhoneCall call, string trigger, string store = null) =>
        call.Incoming != null &&
        Triggers.CanFire(call, call.Incoming, trigger, store, PhoneMemory.SecondsSincePlayed(call.Id));

    private static bool RingDue(string id)
    {
        var call = PhoneBook.Find(id);
        return call == null || TryRingNow(call); // A call whose file or mod is gone is dropped.
    }

    private static void RingFor(string trigger, string store)
    {
        if (!Enabled)
            return;
        var call = Pick(trigger, store);
        if (call != null)
            Ring(call, call.Incoming.DelaySeconds);
    }

    private static PhoneCall PickRandom()
    {
        var config = PhoneExpansionMod.Config.Value;
        if (!Enabled || Random.value * 100f >= config.NewCallChance)
            return null;
        return Pick(IncomingRules.Random, null);
    }

    private static PhoneCall Pick(string trigger, string store) =>
        Triggers.Pick(PhoneBook.All, call => call.Incoming, call => CanRing(call, trigger, store), Candidates);

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

    private static void Schedule(PhoneCall call, float seconds) => PhoneMemory.Current.Rings.Add(call.Id, seconds);
}
