using HarmonyLib;
using NeetModKit.Desktop;

namespace NeetModKit.Patches;

/// <summary>The browser asks ComputerApps which page to show, so URLs can change and mods can add pages.</summary>
[HarmonyPatch(typeof(Computer_Browser_Addresser), nameof(Computer_Browser_Addresser.AddressWrapper))]
internal static class BrowserPatches
{
    private static bool Prefix(Computer_Browser_Addresser __instance)
    {
        ComputerApps.Navigate(__instance);
        return false;
    }
}
