using System;
using System.Linq;
using DevBridge.Server;
using Newtonsoft.Json.Linq;

namespace DevBridge.Commands;

/// <summary>The computer's email: the game's email lists, what's in the inbox, sending one.</summary>
internal static class EmailCommands
{
    [BridgeCommand("email.lists", "The game's email lists (scheduled, random, conditional) and the inbox, with knot names and titles.")]
    private static object Lists(JObject p)
    {
        var handler = Handler();
        return new JObject
        {
            ["scheduled"] = Knots(handler.scheduledEmails?.knots),
            ["random"] = Knots(handler.randomEmailsInstance?.knots),
            ["conditional"] = Knots(handler.conditionalEmails?.knots),
            ["delivered"] = Knots(handler.deliveredEmails?.ToArray()),
            ["pending"] = Knots(handler.pendingEmails?.ToArray()),
        };
    }

    [BridgeCommand("email.send", "Send an email by its Ink knot name, with a title for the inbox.", Params = new[] { "knot", "title" })]
    private static object Send(JObject p)
    {
        var handler = Handler();
        var knot = new KnotList.Knot { inkName = p.Str("knot"), title = p.Str("title"), accessible = true };
        handler.SendEmail(knot);
        return "sent";
    }

    private static EmailHandler Handler() => EmailHandler._ ?? throw new InvalidOperationException("No EmailHandler; load the house first.");

    private static JArray Knots(KnotList.Knot[] knots) =>
        new JArray((knots ?? Array.Empty<KnotList.Knot>()).Select(k => (JToken)new JObject
        {
            ["knot"] = k.inkName,
            ["title"] = k.title,
            ["accessible"] = k.accessible,
            ["read"] = k.read,
        }).ToArray());
}
