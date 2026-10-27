using HarmonyLib;
using NeetModKit.Conversations;

namespace NeetModKit.Patches;

/// <summary>Tells <see cref="InkStory"/> when the game creates its Ink stories.</summary>
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
            InkStory.Dialogue.OnCreated(__instance.masterStory);
    }

    // EmailHandler builds its story in Start. Emails only read it when they're opened.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(EmailHandler), "Start")]
    private static void AfterEmailStart(EmailHandler __instance)
    {
        if (EmailHandler._ == __instance && __instance.masterStory != null)
            InkStory.Email.OnCreated(__instance.masterStory);
    }
}
