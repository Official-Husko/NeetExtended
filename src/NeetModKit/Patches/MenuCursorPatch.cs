using HarmonyLib;
using NeetModKit.Menu;

namespace NeetModKit.Patches;

/// <summary>The camera script locks the cursor every frame; this keeps it free while the mod menu is open.</summary>
[HarmonyPatch(typeof(CameraControl), "Update")]
internal static class MenuCursorPatch
{
    private static void Postfix() => MenuInput.ApplyCursor();
}
