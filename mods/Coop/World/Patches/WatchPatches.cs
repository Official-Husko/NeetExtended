using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Coop.World.Talk;
using Coop.World.Talk.Watch;
using HarmonyLib;

namespace Coop.World.Patches;

/// <summary>
/// The host's own conversation, mirrored for the players near it (see <see cref="WatchProtocol"/>):
/// each thing the game's dialogue box does for the host's player is told to everyone in the same
/// call. A remote player's conversation never goes through these (its steps run in
/// <see cref="TalkRunner"/>, and <see cref="TalkSender"/> tells the watchers).
/// </summary>
[HarmonyPatchCategory(WorldPatches.World)]
[HarmonyPatch]
internal static class WatchPatches
{
    private static readonly List<string> Choices = new List<string>();

    // The seed PlayVoiceAudio rolled for the line AdvanceStory is showing, told to the watchers with it.
    private static int _seed;

    // The host's own player is talking: not a client, and not inside a remote player's step.
    private static bool HostOwn => WorldSide.Running && WorldSide.IsHost && TalkRunner.Current == null && Dialogue._ != null && Dialogue._.currSpeaker != null;

    // A new line (also a line shown again when a conversation picks up where it stopped).
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Dialogue), nameof(Dialogue.AdvanceStory))]
    private static void Line(Dialogue __instance, bool isChoice, bool __runOriginal)
    {
        if (!__runOriginal || !HostOwn)
            return;
        var d = __instance;
        var speaker = Speakers.Of(d.currSpeaker);
        bool playerVoice = d.currentAudio != null && d.currentAudio == d.GetComponent<UnityEngine.AudioSource>();
        WatchSender.Show(speaker, WorldSide.HostSlot, playerVoice, _seed, d.storyText.text);
    }

    // The host's box blips from the line's seed too, so it sounds like what the watchers hear. AdvanceStory
    // calls PlayVoiceAudio() itself; a patch on that method alone can be skipped when the call is
    // compiled into AdvanceStory, so the call is swapped here.
    [HarmonyTranspiler]
    [HarmonyPatch(typeof(Dialogue), nameof(Dialogue.AdvanceStory))]
    private static IEnumerable<CodeInstruction> VoiceCall(IEnumerable<CodeInstruction> code)
    {
        var original = AccessTools.Method(typeof(Dialogue), nameof(Dialogue.PlayVoiceAudio));
        var replacement = AccessTools.Method(typeof(WatchPatches), nameof(PlayVoice));
        bool found = false;
        foreach (var instruction in code)
        {
            if ((instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt) && instruction.operand as MethodBase == original)
            {
                found = true;
                instruction.opcode = OpCodes.Call;
                instruction.operand = replacement;
            }
            yield return instruction;
        }
        if (!found)
            CoopMod.Log.Warn("World: Dialogue.AdvanceStory doesn't call PlayVoiceAudio the way it did; the host's voice blips won't match what others hear.");
    }

    private static IEnumerator PlayVoice(Dialogue d)
    {
        if (!HostOwn)
            return d.PlayVoiceAudio();
        _seed = LineVoice.NewSeed();
        return HostVoice.Play(d, _seed);
    }

    // Use with nothing more to say: the choices come up.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Dialogue), "UpdateChoices")]
    private static void ChoicesUp(Dialogue __instance, bool __runOriginal)
    {
        if (!__runOriginal || !HostOwn)
            return;
        Choices.Clear();
        foreach (var choice in __instance.masterStory.currentChoices)
            Choices.Add(choice?.text ?? "");
        WatchSender.Choices(Speakers.Of(__instance.currSpeaker), Choices);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Dialogue), nameof(Dialogue.MakeChoice))]
    private static void Picked(int choiceNum)
    {
        if (HostOwn)
            WatchSender.Pick(Speakers.Of(Dialogue._.currSpeaker), choiceNum);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Dialogue), nameof(Dialogue.CloseDialogue))]
    private static void Closing()
    {
        if (HostOwn)
            WatchSender.Close(Speakers.Of(Dialogue._.currSpeaker), keepText: false);
        // The game wipes the box; whatever is being watched is drawn again on the next frame.
        Watching.Invalidate();
    }

    // The speaker is done talking but the last line stays up.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Dialogue), nameof(Dialogue.NPCReset))]
    private static void Reset()
    {
        if (HostOwn)
            WatchSender.Close(Speakers.Of(Dialogue._.currSpeaker), keepText: true);
    }

    // Dialogue.Save stores the box's text as a conversation of the host's; a watched line stays out of it.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Dialogue), nameof(Dialogue.Save))]
    private static void Saving(out bool __state) => __state = Watching.HideForSave();

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(Dialogue), nameof(Dialogue.Save))]
    private static void Saved(bool __state)
    {
        if (__state)
            Watching.ShowAfterSave();
    }
}
