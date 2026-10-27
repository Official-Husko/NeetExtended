using System;
using NeetModKit.Rules;
using PhoneExpansion.Calls;

namespace PhoneExpansion.Rules;

/// <summary>
/// The action only the phone has. It joins the shared ones (money, deliver, flag, ...), so an
/// email can ring a call too.
/// </summary>
internal static class CallActions
{
    public static void Register(PhoneExpansionMod mod)
    {
        RuleActions.Register(mod, "ring", Ring, "call",
            "Rings a call (by id) after \"after\" seconds of play. Survives saving. Short ids mean a call from the same mod.");
    }

    private static void Ring(ActionArgs args)
    {
        string id = args.Require("call");
        var call = PhoneBook.Find(id, RuleContext.ModOf(args.Subject)) ?? throw new ArgumentException($"No call '{id}'.");
        IncomingCalls.Ring(call, args.Num("after"));
    }
}
