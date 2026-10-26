using HarmonyLib;
using UnofficialNeetPatches.Fixes;

namespace UnofficialNeetPatches.Patches;

[HarmonyPatch]
[HarmonyPatchCategory(LostCharactersFix.FeatureId)]
internal static class CharacterRegistryPatches
{
    // Before the game's check "is this name taken?", so a respawned character takes the place of a dead one.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(CharacterStories), "Start")]
    private static void BeforeRegister(CharacterStories __instance) =>
        CharacterRegistry.ForgetIfGone(CharacterRegistry.KeyOf(__instance.gameObject.name));

    // Both read the map without checking; SwitchSegmentLine also uses the brain before its null check.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Dialogue), nameof(Dialogue.SwitchSegmentLine), typeof(string), typeof(string))]
    private static bool BeforeSwitchSegmentLine(string characterName) =>
        CharacterRegistry.IsThere(characterName, needsBrain: true);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Dialogue), nameof(Dialogue.SetOverrideKnot))]
    private static bool BeforeSetOverrideKnot(string characterName) =>
        CharacterRegistry.IsThere(characterName, needsBrain: false);
}
