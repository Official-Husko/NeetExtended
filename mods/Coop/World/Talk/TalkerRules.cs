using System;
using System.Collections.Generic;
using Coop.Players;
using Coop.World.Forwarding;
using NeetLoader;
using NeetModKit.Rules;
using UnityEngine;

namespace Coop.World.Talk;

/// <summary>
/// Host: while a remote player's conversation step runs, the framework's rules are about that
/// player. Conditions read the stats and items their game sent (<see cref="TalkerFacts"/>) and where
/// their body is here; stat changes and knockouts go to their game and happen to their real body.
/// </summary>
internal static class TalkerRules
{
    private static readonly RemoteRulePlayer Talker = new RemoteRulePlayer();

    public static void Init(NeetMod mod) => RulePlayer.AddSource(mod, Current);

    private static IRulePlayer Current()
    {
        var c = TalkRunner.Current;
        if (c == null)
            return null;
        Talker.Slot = c.Slot;
        return Talker;
    }

    private sealed class RemoteRulePlayer : IRulePlayer
    {
        public byte Slot;

        private Transform Body =>
            RemotePlayers.Find(Slot)?.transform ?? throw new InvalidOperationException($"Player {Slot} has no body here.");

        private TalkerFacts.Facts Facts =>
            TalkerFacts.Of(Slot) ?? throw new InvalidOperationException($"Player {Slot}'s game hasn't sent their stats yet.");

        public Vector3 Position => Body.position;

        public Vector3 InFront(float distance)
        {
            var body = Body;
            return body.position + Vector3.up * 0.5f + body.forward * distance;
        }

        public float GetStat(string name)
        {
            var stats = Facts.Stats;
            if (stats.TryGetValue(name, out float value))
                return value;
            throw new ArgumentException($"Unknown stat '{name}'. Known: {string.Join(", ", stats.Keys)}");
        }

        public void SetStat(string name, float value)
        {
            GetStat(name);
            Facts.Stats[name] = value;
            Forwarder.StoryStat(Slot, name, set: true, value);
        }

        public void AddStat(string name, float amount)
        {
            float now = GetStat(name);
            Facts.Stats[name] = now + amount;
            Forwarder.StoryStat(Slot, name, set: false, amount);
        }

        public void Knockout(float seconds) => Forwarder.StoryKnockedOut(Slot, seconds);

        public bool Holds(ICollection<string> names)
        {
            var held = TalkerFacts.Of(Slot)?.Held;
            if (held == null)
                return false;
            foreach (string name in held)
            {
                if (names.Contains(name))
                    return true;
            }
            return false;
        }
    }
}
