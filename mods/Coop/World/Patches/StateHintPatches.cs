using Coop.World.State;
using HarmonyLib;

namespace Coop.World.Patches;

/// <summary>
/// The moments a player changes shared state by hand. Each marks the component so its new state goes
/// out at the end of the frame, from whichever game it happened in (the sweep would catch it too, but
/// later, and only on the game that watches it).
/// </summary>
[HarmonyPatchCategory(WorldPatches.World)]
[HarmonyPatch]
internal static class StateHintPatches
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Door), nameof(Door.Locked), MethodType.Setter)]
    private static void DoorLock(Door __instance) => StateSync.MarkDirty(__instance);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(LightSwitch), nameof(LightSwitch.Use))]
    private static void LightSwitched(LightSwitch __instance)
    {
        StateSync.MarkDirty(__instance);
        if (__instance.threewaySwitch != null)
            StateSync.MarkDirty(__instance.threewaySwitch);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(FuseBox_Button), nameof(FuseBox_Button.IsOn), MethodType.Setter)]
    private static void Breaker(FuseBox_Button __instance) => StateSync.MarkDirty(__instance);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(FuseBox_Slot), nameof(FuseBox_Slot.InsertFuse))]
    private static void FuseIn(FuseBox_Slot __instance) => StateSync.MarkDirty(__instance);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(FuseBox_Slot), nameof(FuseBox_Slot.ExpellFuse))]
    private static void FuseOut(FuseBox_Slot __instance) => StateSync.MarkDirty(__instance);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(TV), nameof(TV.Control), typeof(int), typeof(bool))]
    private static void Television(TV __instance) => StateSync.MarkDirty(__instance);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(SinkFaucet), nameof(SinkFaucet.Toggle))]
    private static void Tap(SinkFaucet __instance) => StateSync.MarkDirty(__instance);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GasButton), nameof(GasButton.Toggle))]
    private static void Stove(GasButton __instance) => StateSync.MarkDirty(__instance);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Flashlight), nameof(Flashlight.SetLight))]
    private static void Torch(Flashlight __instance) => StateSync.MarkDirty(__instance);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Lighter_BBQ), "Toggle")]
    private static void Lighter(Lighter_BBQ __instance) => StateSync.MarkDirty(__instance);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Radio), nameof(Radio.Fuse), MethodType.Setter)]
    private static void RadioBattery(Radio __instance) => StateSync.MarkDirty(__instance);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Radio), nameof(Radio.InsertCassette))]
    private static void TapeIn(Radio __instance) => StateSync.MarkDirty(__instance);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Radio), "EjectCassette")]
    private static void TapeOut(Radio __instance) => StateSync.MarkDirty(__instance);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Radio), nameof(Radio.SetUse), typeof(bool))]
    private static void RadioLid(Radio __instance) => StateSync.MarkDirty(__instance);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Radio), nameof(Radio.MusicSwitch))]
    private static void RadioMusic(Radio __instance) => StateSync.MarkDirty(__instance);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Radio), nameof(Radio.PunchTrigger))]
    private static void RadioPunched(Radio __instance) => StateSync.MarkDirty(__instance);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Cardboard_Box), nameof(Cardboard_Box.Use))]
    private static void BoxUsed(Cardboard_Box __instance) => StateSync.MarkDirty(__instance);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Cardboard_Box), "BreakTape")]
    private static void BoxCut(Cardboard_Box __instance) => StateSync.MarkDirty(__instance);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(SmokeDetector), nameof(SmokeDetector.RemoveBattery))]
    private static void DetectorEmptied(SmokeDetector __instance) => StateSync.MarkDirty(__instance);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(SmokeDetector), "OnCollisionEnter")]
    private static void DetectorFilled(SmokeDetector __instance) => StateSync.MarkDirty(__instance);
}
