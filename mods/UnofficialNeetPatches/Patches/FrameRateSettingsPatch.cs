using System.Collections.Generic;
using HarmonyLib;
using NeetModKit.Patching;
using UnityEngine;
using UnofficialNeetPatches.Fixes;

namespace UnofficialNeetPatches.Patches;

/// <summary>
/// CameraControl.Start sets QualitySettings.vSyncCount = 0 and Application.targetFrameRate = 120.
/// Every character rig has a CameraControl, and BrainNeetAI calls Start on its camera again, so this
/// runs whenever the house loads or a character spawns, over the choice the settings menu
/// (Feedback_Sender) applied from PlayerPrefs. The transpiler keeps the game's values as the
/// default for players who never picked anything and otherwise keeps their choice.
/// </summary>
[HarmonyPatch(typeof(CameraControl), nameof(CameraControl.Start))]
[HarmonyPatchCategory(FrameRateSettingsFix.FeatureId)]
internal static class FrameRateSettingsPatch
{
    // The keys Feedback_Sender saves the settings under.
    private const string VSyncKey = "VerticalSync";
    private const string FrameRateKey = "Framerate";

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code)
    {
        code = CallSwap.Replace(code,
            AccessTools.PropertySetter(typeof(QualitySettings), nameof(QualitySettings.vSyncCount)),
            AccessTools.Method(typeof(FrameRateSettingsPatch), nameof(SetVSync)));
        return CallSwap.Replace(code,
            AccessTools.PropertySetter(typeof(Application), nameof(Application.targetFrameRate)),
            AccessTools.Method(typeof(FrameRateSettingsPatch), nameof(SetFrameRate)));
    }

    private static void SetVSync(int gameDefault) =>
        QualitySettings.vSyncCount = PlayerPrefs.HasKey(VSyncKey) ? PlayerPrefs.GetInt(VSyncKey) : gameDefault;

    // The saved frame rate is an index into the menu's dropdown; the menu applies it itself.
    private static void SetFrameRate(int gameDefault)
    {
        if (!PlayerPrefs.HasKey(FrameRateKey))
            Application.targetFrameRate = gameDefault;
    }
}
