using System;
using NeetModKit.Game;
using NeetModKit.Rules;
using PhoneExpansion.Calls;
using UnityEngine;

namespace PhoneExpansion.Rules;

/// <summary>
/// Actions only the phone has. They join the shared ones (money, deliver, flag, ...), so an email
/// can ring a call too.
/// </summary>
internal static class CallActions
{
    public static void Register(PhoneExpansionMod mod)
    {
        RuleActions.Register(mod, "give", a => a.Repeat(() => Spawning.Item(a.Require("item"), BesideSpeaker())), "item",
            "Pops an item out next to the phone, like the game's own GiveItem. \"count\" for more.");
        RuleActions.Register(mod, "ring", Ring, "call",
            "Rings a call (by id) after \"after\" seconds of play. Survives saving. Short ids mean a call from the same mod.");
    }

    private static Vector3 BesideSpeaker()
    {
        var speaker = Dialogue._ != null ? Dialogue._.currSpeaker : null;
        if (speaker == null)
            return Spawning.InFrontOfPlayer();
        // Same spot the game uses for GiveItem.
        return speaker.transform.position + Vector3.up * 0.6f + speaker.transform.forward * 0.3f;
    }

    private static void Ring(ActionArgs args)
    {
        string id = args.Require("call");
        var call = PhoneBook.Find(id, RuleContext.ModOf(args.Subject)) ?? throw new ArgumentException($"No call '{id}'.");
        IncomingCalls.Ring(call, args.Num("after"));
    }
}
