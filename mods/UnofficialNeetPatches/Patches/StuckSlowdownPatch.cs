using HarmonyLib;
using UnityEngine;
using UnofficialNeetPatches.Fixes;

namespace UnofficialNeetPatches.Patches;

/// <summary>
/// Player_Movement adds its crouch and weight slowdowns itself and only removes the copies it
/// added. Copies loaded from a save belong to nobody, and since the effects can't stack, the
/// movement code can't add its own either, so they stay. After a load, the movement's own
/// effects are removed again; it adds them back next frame if they apply.
/// </summary>
[HarmonyPatch(typeof(StatusHandler), nameof(StatusHandler.Load))]
[HarmonyPatchCategory(StuckSlowdownsFix.FeatureId)]
internal static class StuckSlowdownPatch
{
    private static void Postfix(StatusHandler __instance)
    {
        var movement = __instance.GetComponent<Player_Movement>();
        if (movement == null)
            return;
        Drop(__instance, movement.speedEffectCrouch);
        Drop(__instance, movement.speedEffectFat);
    }

    private static void Drop(StatusHandler handler, StatusEffect template)
    {
        if (template == null)
            return;
        string name = template.name + "(Clone)";
        for (int i = handler.effects.Count - 1; i >= 0; i--)
        {
            if (handler.effects[i].name == name)
                handler.RemoveEffect(handler.effects[i]);
        }
        // Loading also queues effects to add on the next update.
        var queued = handler.effectsToAdd;
        if (queued == null || queued.Count == 0)
            return;
        var keep = new System.Collections.Generic.List<StatusEffect>();
        while (queued.Count > 0)
        {
            var effect = queued.Pop();
            if (effect.name.Replace("(Clone)", "") != template.name)
                keep.Add(effect);
        }
        for (int i = keep.Count - 1; i >= 0; i--)
            queued.Push(keep[i]);
    }
}
