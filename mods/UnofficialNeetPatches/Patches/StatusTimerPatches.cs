using HarmonyLib;
using UnityEngine;
using UnofficialNeetPatches.Fixes;

namespace UnofficialNeetPatches.Patches;

/// <summary>
/// StatusHandler.Update ticks every effect but only counts down the duration of the last one in
/// the list (<c>if (effects.Count - 1 == num)</c>, meant as a "still there" check), so older effects
/// freeze until everything newer has ended. This is the same update with each effect counting
/// down its own duration. Ended effects (instantiated per application) are destroyed, which also
/// makes Player_Movement add its weight slowdown again later: it only does so while its reference
/// to the old one compares equal to null.
/// </summary>
[HarmonyPatch]
[HarmonyPatchCategory(StatusTimersFix.FeatureId)]
internal static class StatusTimerPatches
{
    private static readonly AccessTools.FieldRef<Stat, float> InitValue = AccessTools.FieldRefAccess<Stat, float>("initValue");

    [HarmonyPrefix]
    [HarmonyPatch(typeof(StatusHandler), "Update")]
    private static bool Update(StatusHandler __instance)
    {
        foreach (var sticky in __instance.stickyStats)
            sticky.SetVal(InitValue(sticky));

        var effects = __instance.effects;
        for (int i = effects.Count - 1; i >= 0; i--)
        {
            // An effect's tick can add or end effects, so the list may have shrunk.
            if (i >= effects.Count)
                continue;
            var effect = effects[i];
            if (effect.startDelayTimer > 0f)
                continue;

            switch (effect.statusType)
            {
                case StatusEffectType.ticker:
                    if (Time.time > effect.timeToLastTick + effect.tickerFreq)
                    {
                        effect.OnTick();
                        effect.timeToLastTick = Time.time;
                    }
                    break;
                case StatusEffectType.persistent:
                    effect.OnTick();
                    break;
                case StatusEffectType.oneTime:
                    effect.OnTick();
                    __instance.RemoveEffect(effect);
                    continue;
            }

            effect.durationTimer -= Time.deltaTime;
            if (effect.durationTimer <= 0f && effects.Contains(effect))
                __instance.RemoveEffect(effect);
        }

        var toAdd = __instance.effectsToAdd;
        while (toAdd != null && toAdd.Count != 0)
            effects.Add(toAdd.Pop());
        return false;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(StatusHandler), nameof(StatusHandler.RemoveEffect))]
    private static void AfterRemove(StatusHandler __instance, StatusEffect st)
    {
        // Only copies made by AddEffect, never the templates in HouseManager.statusEffectDict.
        if (st != null && st.name.EndsWith("(Clone)") && !__instance.effects.Contains(st))
            Object.Destroy(st);
    }
}
