using System.Collections.Generic;
using System.Reflection;
using Coop.World.Objects;
using Coop.World.Spawning;
using HarmonyLib;

namespace Coop.World.Patches;

/// <summary>Things appearing (Start) and going away (OnDestroy), and the game's save loading, which does both wholesale.</summary>
[HarmonyPatchCategory(WorldPatches.World)]
[HarmonyPatch]
internal static class SpawnPatches
{
    // Runs after whatever made the item has set it up (Start waits for the frame after Instantiate).
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Usable), nameof(Usable.Start))]
    private static void UsableStarted(Usable __instance) => SpawnSender.OnUsableStarted(__instance);

    // Every brain's Start override calls this base Start.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Brain), nameof(Brain.Start))]
    private static void BrainStarted(Brain __instance) => SpawnSender.OnBrainStarted(__instance);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Usable), "OnDestroy")]
    private static void UsableDestroyed(Usable __instance) => SpawnSender.OnDestroyed(__instance.gameObject);

    // LoadAll destroys and recreates every item and brain; none of that is news.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(SaveLoadHandler), nameof(SaveLoadHandler.LoadAll))]
    private static void BeforeLoad() => SpawnContext.BeginLoading();

    // LoadAll has just set the id counter to the save's; a client moves it into its own block before
    // its new body's clothes take ids.
    [HarmonyFinalizer]
    [HarmonyPatch(typeof(SaveLoadHandler), nameof(SaveLoadHandler.LoadAll))]
    private static void AfterLoad()
    {
        SpawnContext.EndLoading();
        if (WorldSide.IsClient)
            IdRanges.UseLocalBlock();
    }
}

/// <summary>
/// Brains going away. Seven brain types declare their own private OnDestroy, which hides the base
/// one, so every declared OnDestroy in the brain family is patched.
/// </summary>
[HarmonyPatchCategory(WorldPatches.World)]
[HarmonyPatch]
internal static class BrainDestroyPatches
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (var type in typeof(Brain).Assembly.GetTypes())
        {
            if (!typeof(Brain).IsAssignableFrom(type))
                continue;
            var method = AccessTools.DeclaredMethod(type, "OnDestroy");
            if (method != null)
                yield return method;
        }
    }

    private static void Prefix(UnityEngine.Component __instance) => SpawnSender.OnDestroyed(__instance.gameObject);
}
