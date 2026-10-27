using Coop.World.Hazards;
using HarmonyLib;

namespace Coop.World.Patches;

/// <summary>A flame put out on a client (water, the extinguisher) goes out on the host too; see <see cref="FireSync"/>.</summary>
[HarmonyPatchCategory(WorldPatches.World)]
[HarmonyPatch(typeof(FireParticle), nameof(FireParticle.Kill))]
internal static class FirePatches
{
    private static void Prefix(FireParticle __instance) => FireSync.FlameOut(__instance);
}
