using HarmonyLib;
using PhoneExpansion.Calls;

namespace PhoneExpansion.Patches;

/// <summary>Routes connecting calls to modded knots and tidies up when they end.</summary>
[HarmonyPatch]
internal static class CallPatches
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Dialogue), nameof(Dialogue.InitCharacterDialogue))]
    private static bool BeforeDialogue(CharacterStories speaker, string flow, ref string charKnot) =>
        CallRouter.Route(speaker, flow, ref charKnot);

    // The game only forgets the "Phone" conversation when the knot's name contains "Phone";
    // hand-written call knots can be named anything.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Dialogue), nameof(Dialogue.CloseDialogue))]
    private static void BeforeCloseDialogue(Dialogue __instance)
    {
        if (CallRouter.ActiveKnot != null && __instance.currKnot == CallRouter.ActiveKnot)
            __instance.aliveFlowNames?.Remove(CallRouter.Flow);
    }

    // Use pressed on the last line with nothing left to say: hang up, even if a script forgot
    // the EndPhoneCall tag.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Dialogue), "UpdateChoices")]
    private static void AfterUpdateChoices(Dialogue __instance)
    {
        var phone = CallRouter.ActivePhone;
        if (phone == null || __instance.currSpeaker == null || __instance.currSpeaker.gameObject != phone.gameObject)
            return;
        var story = __instance.masterStory;
        if (!story.canContinue && story.currentChoices.Count == 0)
            PhoneLine.HangUp(phone);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Phone_Interface), nameof(Phone_Interface.ClearScreen))]
    private static void AfterClearScreen(Phone_Interface __instance)
    {
        if (__instance.screenText.text == __instance.initText)
            CallerScreen.Forget(__instance);
        if (CallRouter.ActivePhone == __instance && !__instance.talking)
            CallRouter.End();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Phone_Interface), "Start")]
    private static void AfterPhoneStart(Phone_Interface __instance) =>
        PhoneLine.SetNumbers(__instance, PhoneExpansionMod.ModdedNumbers());
}
