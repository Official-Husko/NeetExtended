using EmailExpansion.Reader;
using HarmonyLib;

namespace EmailExpansion.Patches;

/// <summary>Opens modded emails with the mod's reader; the game's own emails open as usual.</summary>
[HarmonyPatch(typeof(EmailApp), nameof(EmailApp.LoadEmail))]
internal static class EmailAppPatches
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(EmailApp __instance, KnotList.Knot tmpKnot) => MailReader.Open(__instance, tmpKnot);
}
