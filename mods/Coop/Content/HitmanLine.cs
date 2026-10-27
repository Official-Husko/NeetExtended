using System;
using Coop.World;
using NeetModKit.Conversations;
using NeetModKit.Game;
using NeetModKit.Rules;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Coop.Content;

/// <summary>
/// The hitman line's job (phone/hitman.json): hire the hitman on a player, one job at a time, with a
/// cooldown (the host's Hitman line cooldown setting) kept in the save. Rules for JSON files and Ink
/// functions for hand-written scripts.
/// </summary>
internal static class HitmanLine
{
    // Seconds of play when the last hit was ordered, kept in the host's save like every flag.
    private const string LastHitFlag = "coop_hitman_at";

    public static void Register(CoopMod mod)
    {
        RuleConditions.Register(mod, "hitman", State,
            "\"busy\" while the hitman is on a job, \"free\" when he isn't, \"caller\" while he's after the one on the line");
        RuleConditions.Register(mod, "hitmanWait", value =>
        {
            var comparison = Comparison.Parse(value);
            return _ => comparison.Test(MinutesToWait());
        }, "minutes until the hitman line takes another job (0 when it would)");
        RuleActions.Register(mod, "hitman", a => Hire(StoryPlayers.ParseSlot(a.Str("target", "")), a.Num("price")), "target",
            "Sends the hitman for \"target\" (a slot, or \"me\" for the one on the line) and takes \"price\" if he takes the job.");
        RuleTexts.Register(mod, "hitmanWait", _ => MinutesToWait().ToString(), "minutes until the hitman line takes another job");
        RuleTexts.Register(mod, "hitmanTarget", _ => Contracts.HitmanBusy ? StoryPlayers.NameOf(Contracts.HitmanTarget) : "nobody",
            "who the hitman is after");

        InkFunctions.Register(mod, "CoopHitmanBusy", new string[0], _ => Contracts.HitmanBusy, lookaheadSafe: true);
        InkFunctions.Register(mod, "CoopHitmanWait", new string[0], _ => MinutesToWait(), lookaheadSafe: true);
        InkFunctions.Register(mod, "CoopHireHitman", new[] { "slot" }, a => Hire(InkValues.Int(a, 0), 0f));
    }

    private static Func<IRuleSubject, bool> State(JToken value)
    {
        string want = value.Value<string>()?.Trim().ToLowerInvariant();
        switch (want)
        {
            case "busy": return _ => Contracts.HitmanBusy;
            case "free": return _ => !Contracts.HitmanBusy;
            case "caller": return _ => Contracts.HitmanBusy && Contracts.HitmanTarget == StoryPlayers.Caller;
            default: throw new FormatException("\"hitman\" takes \"busy\", \"free\" or \"caller\".");
        }
    }

    private static int MinutesToWait()
    {
        int orderedAt = ModFlags.Get(LastHitFlag);
        if (orderedAt <= 0)
            return 0;
        float left = CoopMod.Config.Value.HitmanCooldown * 60f - (ModFlags.PlaySeconds - orderedAt);
        return left > 0f ? Mathf.CeilToInt(left / 60f) : 0;
    }

    /// <summary>Sends the hitman for <paramref name="slot"/> in about a minute; false when he can't take the job.</summary>
    private static bool Hire(int slot, float price)
    {
        if (!StoryPlayers.IsHere(slot) || MinutesToWait() > 0)
            return false;
        if (!Contracts.SendHitman((byte)slot, UnityEngine.Random.Range(40f, 80f)))
            return false;
        ModFlags.Set(LastHitFlag, Mathf.Max(1, (int)ModFlags.PlaySeconds));
        if (price > 0f)
            Money.Add(-price);
        return true;
    }
}
