using HarmonyLib;
using NeetModKit.Conversations;

namespace NeetModKit.Patches;

/// <summary>
/// The inbox is saved with each email's knot name. An email from a mod that's been removed points
/// at a knot the email story doesn't have anymore, and opening it threw inside the game. It shows a
/// note instead.
/// </summary>
[HarmonyPatch(typeof(EmailApp), nameof(EmailApp.LoadEmail))]
internal static class EmailGuardPatch
{
    internal const string Missing = "This email came from a mod that isn't installed anymore.";

    [HarmonyPriority(Priority.Last)]
    private static bool Prefix(EmailApp __instance, KnotList.Knot tmpKnot, bool __runOriginal)
    {
        if (!__runOriginal)
            return false; // Another patch (a mod's reader) already showed it.
        if (tmpKnot == null || InkStory.Email.Current == null || InkStory.Email.HasKnot(tmpKnot.inkName))
            return true;
        __instance.emailBody.text = Missing;
        return false;
    }
}
