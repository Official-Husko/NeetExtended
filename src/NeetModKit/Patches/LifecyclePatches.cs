using HarmonyLib;
using NeetLoader.Runtime;
using NeetModKit.Events;

namespace NeetModKit.Patches;

/// <summary>Turns the game's save/load and setup calls into <see cref="GameEvents"/>.</summary>
[HarmonyPatch]
internal static class LifecyclePatches
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(PickupBank), nameof(PickupBank.Init))]
    private static void AfterPickupBankInit() => GameEvents.Raise(GameEvents.ItemsRegistering);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(SaveLoadHandler), nameof(SaveLoadHandler.SaveAll))]
    private static void BeforeSave()
    {
        GameEvents.Raise(GameEvents.BeforeSave);
        Conversations.SaveGuard.BeforeSave();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(SaveLoadHandler), nameof(SaveLoadHandler.SaveAll))]
    private static void AfterSave(bool __runOriginal)
    {
        // Nothing was saved when SaveLock (or another mod) skipped the save.
        if (__runOriginal)
            GameEvents.Raise(GameEvents.AfterSave);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(SaveLoadHandler), nameof(SaveLoadHandler.LoadAll))]
    private static void BeforeLoad() => GameEvents.Raise(GameEvents.BeforeLoad);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(SaveLoadHandler), nameof(SaveLoadHandler.LoadAll))]
    private static void AfterLoad() => GameEvents.Raise(GameEvents.AfterLoad);

    // SaveLoadHandler.Start runs LoadAll. Waiting one frame lets every other Start finish too.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(SaveLoadHandler), "Start")]
    private static void AfterSaveHandlerStart() => MainThread.Post(GameEvents.RaiseHouseReady);
}
