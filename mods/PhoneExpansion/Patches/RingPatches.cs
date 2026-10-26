using System.Collections;
using HarmonyLib;
using PhoneExpansion.Calls;

namespace PhoneExpansion.Patches;

/// <summary>Picks modded calls when the phones ring, and keeps caller ID from confusing the buttons.</summary>
[HarmonyPatch]
internal static class RingPatches
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Phone_Interface), nameof(Phone_Interface.PrankCall))]
    private static void BeforeRinging(Phone_Interface __instance) => IncomingCalls.OnPhoneStartsRinging(__instance);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Phone_Interface), nameof(Phone_Interface.PrankCall))]
    private static void WrapRinging(Phone_Interface __instance, ref IEnumerator __result) =>
        __result = ClearWhenDone(__result, __instance);

    /// <summary>Nobody answered: take the caller's name off the screen again.</summary>
    private static IEnumerator ClearWhenDone(IEnumerator ringing, Phone_Interface phone)
    {
        while (ringing.MoveNext())
            yield return ringing.Current;
        CallerScreen.Clear(phone);
    }

    // Answering one handset stops the others ringing (the game stops their coroutines, so
    // ClearWhenDone never finishes there).
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Phone_Dispatch), nameof(Phone_Dispatch.AnsweredPhone), MethodType.Setter)]
    private static void AfterAnswered(Phone_Dispatch __instance, Phone_Interface value)
    {
        foreach (var phone in __instance.phones)
        {
            if (phone != value)
                CallerScreen.Clear(phone);
        }
    }

    // The call button dials whatever is on the screen; never dial a caller's name.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Phone_Interface), nameof(Phone_Interface.Call))]
    private static bool BeforeCall(Phone_Interface __instance) => !CallerScreen.IsShowing(__instance);

    // Typing a number while a name shows: start from an empty screen like the game expects.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Phone_Interface), nameof(Phone_Interface.ComposeNumber))]
    private static void BeforeCompose(Phone_Interface __instance, int number)
    {
        if (number >= 0 && CallerScreen.IsShowing(__instance))
            CallerScreen.Clear(__instance);
    }
}
