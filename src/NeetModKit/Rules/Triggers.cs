using System;
using System.Collections.Generic;
using UnityEngine;

namespace NeetModKit.Rules;

/// <summary>Picks what fires for a trigger, by <see cref="TriggerRules"/>.</summary>
public static class Triggers
{
    /// <summary>
    /// Could <paramref name="subject"/> fire for <paramref name="trigger"/> now? Checks the trigger and
    /// store, "once" (by <see cref="IRuleSubject.Times"/>), the cooldown and "when".
    /// </summary>
    /// <param name="secondsSinceLast">Play seconds since it last fired, or infinity if never.</param>
    public static bool CanFire(IRuleSubject subject, TriggerRules rules, string trigger, string store, float secondsSinceLast)
    {
        if (rules == null || rules.Trigger != trigger)
            return false;
        // Things that fire on their own happen where the house runs, not once per co-op player.
        if (trigger != TriggerRules.Manual && !Game.WorldRole.RunsWorld)
            return false;
        if (store != null && rules.Store != null && !string.Equals(rules.Store, store, StringComparison.OrdinalIgnoreCase))
            return false;
        if (rules.Once && subject.Times > 0)
            return false;
        if (rules.CooldownMinutes > 0f && secondsSinceLast < rules.CooldownMinutes * 60f)
            return false;
        return rules.When == null || rules.When.Test(subject);
    }

    /// <summary>
    /// One of <paramref name="items"/> that <paramref name="canFire"/> allows, chosen by weight, or null.
    /// <paramref name="buffer"/> is reused between calls so picking doesn't allocate.
    /// </summary>
    public static T Pick<T>(IEnumerable<T> items, Func<T, TriggerRules> rules, Func<T, bool> canFire, List<T> buffer) where T : class
    {
        buffer.Clear();
        float total = 0f;
        foreach (var item in items)
        {
            var r = rules(item);
            if (r == null || r.Weight <= 0f || !canFire(item))
                continue;
            buffer.Add(item);
            total += r.Weight;
        }

        float roll = UnityEngine.Random.value * total;
        foreach (var item in buffer)
        {
            roll -= rules(item).Weight;
            if (roll <= 0f)
                return item;
        }
        return buffer.Count > 0 ? buffer[buffer.Count - 1] : null;
    }
}
