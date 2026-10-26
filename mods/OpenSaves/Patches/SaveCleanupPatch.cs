using HarmonyLib;
using OpenSaves.Writing;

namespace OpenSaves.Patches;

[HarmonyPatch(typeof(SaveLoadHandler), nameof(SaveLoadHandler.SaveAll))]
[HarmonyPatchCategory(Features.SaveCleanupFeature.FeatureId)]
internal static class SaveCleanupPatch
{
    private static void Postfix() => SaveCleanup.Prune();
}
