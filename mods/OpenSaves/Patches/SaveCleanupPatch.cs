using HarmonyLib;
using OpenSaves.Writing;

namespace OpenSaves.Patches;

[HarmonyPatch(typeof(SaveLoadHandler), nameof(SaveLoadHandler.SaveAll))]
[HarmonyPatchCategory(Features.SaveCleanupFeature.FeatureId)]
internal static class SaveCleanupPatch
{
    // Not when the save was skipped (SaveLock): those saves aren't ours to tidy up after.
    private static void Postfix(bool __runOriginal)
    {
        if (__runOriginal)
            SaveCleanup.Prune();
    }
}
