using HarmonyLib;

namespace Coop.Session;

/// <summary>
/// What a client can't do in the host's house yet. The people in the house run on the host, and so
/// does the story they're part of; a conversation on a client would move its own copy of the story
/// away from the host's. The phone isn't a person, so calls still work.
/// </summary>
[HarmonyPatch]
internal static class GuestPolicy
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Dialogue), nameof(Dialogue.InitCharacterDialogue))]
    private static bool NoConversations(CharacterStories speaker) =>
        !CoopSession.IsClient || speaker == null || speaker.GetComponentInParent<Brain>() == null;
}
