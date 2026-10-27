using Coop.World.Painting;
using HarmonyLib;

namespace Coop.World.Patches;

/// <summary>Every splat and every drying surface goes through here; see <see cref="PaintSync"/>.</summary>
[HarmonyPatch]
[HarmonyPatchCategory(WorldPatches.World)]
internal static class PaintPatches
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(PaintTarget), nameof(PaintTarget.PaintSplat))]
    private static void BeforeSplat(PaintTarget __instance, global::Paint paint) => PaintSync.Painted(__instance, paint);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PaintTarget), nameof(PaintTarget.ClearWaterChannel))]
    private static void AfterDrying(PaintTarget __instance) => PaintSync.Dried(__instance);
}

/// <summary>
/// A room fire scorches under each of its burning spheres on every physics step. On the host those
/// splats are sent merged (<see cref="PaintRepeats"/>). A client's copy of the fire (one sphere in the
/// middle of the room, see FireSync) would scorch different places, so it doesn't: the host's arrive.
/// </summary>
[HarmonyPatch(typeof(FireSpreader), "PaintBurnMark")]
[HarmonyPatchCategory(WorldPatches.World)]
internal static class FireScorchPatch
{
    private static bool Prefix()
    {
        if (WorldSide.IsClient)
            return false;
        PaintSync.Scorching = true;
        return true;
    }

    private static void Finalizer() => PaintSync.Scorching = false;
}

