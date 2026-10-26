using HarmonyLib;
using NeetModKit.Conversations;

namespace NeetModKit.Patches;

/// <summary>Tells <see cref="GameStory"/> when the game creates its dialogue story.</summary>
[HarmonyPatch]
internal static class DialoguePatches
{
    // Awake creates the story; Start binds the game's functions. Mod scripts go in between, so
    // they're in place before a save is loaded into the story.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Dialogue), "Awake")]
    private static void AfterAwake(Dialogue __instance)
    {
        if (Dialogue._ == __instance && __instance.masterStory != null)
            GameStory.OnCreated(__instance.masterStory);
    }
}
