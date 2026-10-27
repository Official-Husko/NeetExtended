using System;
using Coop.World;
using NeetModKit.Conversations;
using NeetModKit.Rules;
using Newtonsoft.Json.Linq;

namespace Coop.Content;

/// <summary>
/// What co-op content can ask: rules and text values for JSON files, Ink functions for hand-written
/// scripts. Calls run in the host's game, so these answer there; on a client they're never used.
/// </summary>
internal static class CoopStory
{
    public static void Register(CoopMod mod)
    {
        RuleConditions.Register(mod, "coop", Coop, "true while playing co-op (false otherwise)");
        RuleConditions.Register(mod, "otherPlayer", value =>
        {
            int slot = value.Value<int>();
            return _ => StoryPlayers.IsOther(slot);
        }, "another player is in the house in this slot (0 is the host), and it's not the one on the line");
        RuleTexts.Register(mod, "player", arg => StoryPlayers.NameOf(StoryPlayers.ParseSlot(arg)),
            "a player's name: {{player:1}}, or {{player:me}} for the one on the line");

        InkFunctions.Register(mod, "CoopCaller", new string[0], _ => (int)StoryPlayers.Caller, lookaheadSafe: true);
        InkFunctions.Register(mod, "CoopHere", new[] { "slot" }, a => StoryPlayers.IsHere(InkValues.Int(a, 0)), lookaheadSafe: true);
        InkFunctions.Register(mod, "CoopName", new[] { "slot" }, a => StoryPlayers.NameOf(InkValues.Int(a, 0)), lookaheadSafe: true);

        HitmanLine.Register(mod);
    }

    private static Func<IRuleSubject, bool> Coop(JToken value)
    {
        bool want = value.Type == JTokenType.Boolean ? value.Value<bool>() : throw new FormatException("\"coop\" takes true or false.");
        return _ => WorldSide.Running == want;
    }
}
