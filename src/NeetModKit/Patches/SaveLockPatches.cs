using HarmonyLib;
using NeetModKit.Saves;

namespace NeetModKit.Patches;

/// <summary>What <see cref="SaveLock"/> blocks: every save, and the pause menu's "reset save".</summary>
[HarmonyPatch]
internal static class SaveLockPatches
{
    // First, so it skips the other SaveAll prefixes too (the framework's BeforeSave hooks among them).
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    [HarmonyPatch(typeof(SaveLoadHandler), nameof(SaveLoadHandler.SaveAll))]
    private static bool BlockSave()
    {
        if (!SaveLock.IsLocked)
            return true;
        KitMod.Instance.Log.Debug($"Save skipped: {SaveLock.Reason}.");
        return false;
    }

    // It deletes the player's save files, which this game isn't showing.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Feedback_Sender), nameof(Feedback_Sender.ResetSave))]
    private static bool BlockReset(bool reset)
    {
        if (!reset || !SaveLock.IsLocked)
            return true;
        KitMod.Instance.Log.Warn($"Save reset ignored: {SaveLock.Reason}.");
        return false;
    }
}
