using Coop.World.Painting;
using HarmonyLib;
using UnityEngine;

namespace Coop.World.Patches;

/// <summary>Tells <see cref="PaintSources"/> which object paints, and which blobs a tap made.</summary>
[HarmonyPatch]
[HarmonyPatchCategory(WorldPatches.World)]
internal static class PainterPatches
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(CollisionPainter), "HandleCollision")]
    private static void BeforeCollision(CollisionPainter __instance) => PaintSources.Painter = __instance.gameObject;

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(CollisionPainter), "HandleCollision")]
    private static void AfterCollision() => PaintSources.Painter = null;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ParticlePainter), "OnParticleCollision")]
    private static void BeforeParticles(ParticlePainter __instance) => PaintSources.Painter = __instance.gameObject;

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(ParticlePainter), "OnParticleCollision")]
    private static void AfterParticles() => PaintSources.Painter = null;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Spawner), nameof(Spawner.Spawn))]
    private static void AfterSpawn(Spawner __instance, GameObject __result) => PaintSources.Spawned(__instance, __result);
}
