using HarmonyLib;
using NeetModKit.Ui.Toolkit;

namespace NeetModKit.Patches;

/// <summary>The camera script locks the cursor every frame; this keeps it free while a window holds the input lock.</summary>
[HarmonyPatch(typeof(CameraControl), "Update")]
internal static class MenuCursorPatch
{
    private static void Postfix() => UiInputLock.ApplyCursor();
}
