using Coop.World.Talk;
using HarmonyLib;
using UnityEngine;

namespace Coop.World.Patches;

/// <summary>
/// Conversations and phones run on the host (see <see cref="TalkProtocol"/>). On a client the story
/// never runs: starts, Use, choices and phone buttons become requests. On the host, World's last
/// prefix takes remote players' conversations away from the host's dialogue box.
/// </summary>
[HarmonyPatchCategory(WorldPatches.World)]
[HarmonyPatch]
internal static class TalkPatches
{
    private static bool OnClient => WorldSide.Running && WorldSide.IsClient;

    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    [HarmonyPatch(typeof(Dialogue), nameof(Dialogue.InitCharacterDialogue))]
    private static bool ClientStart(CharacterStories speaker, string flow) => ClientTalk.BeforeInit(speaker, flow);

    // Last, so mods that route conversations (Phone Expansion, Unofficial NEET Patches) have had their say.
    [HarmonyPrefix]
    [HarmonyPriority(Priority.Last)]
    [HarmonyPatch(typeof(Dialogue), nameof(Dialogue.InitCharacterDialogue))]
    private static bool HostStart(CharacterStories speaker, string flow, string charKnot, bool enableCanvasOnly, bool forceChoosePath, bool __runOriginal) =>
        HostTalks.BeforeInit(speaker, flow, charKnot, enableCanvasOnly, forceChoosePath, __runOriginal);

    // The client's pump is ClientTalk.Tick; this game's copy of the story never moves.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Dialogue), "Update")]
    private static bool Pump() => !OnClient;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Dialogue), nameof(Dialogue.MakeChoice))]
    private static bool Choice(int choiceNum) => ClientTalk.BeforeChoice(choiceNum);

    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    [HarmonyPatch(typeof(Dialogue), nameof(Dialogue.CloseDialogue))]
    private static bool Closing()
    {
        if (OnClient)
        {
            ClientTalk.BeforeClose();
            return true;
        }
        // Inside a remote player's step: that conversation ends, the host's box stays as it is.
        var current = TalkRunner.Current;
        if (current == null)
            return true;
        TalkRunner.Close(current);
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Dialogue), nameof(Dialogue.AdvanceStory))]
    private static bool Advance() => !OnClient;

    // A client's copy of the story never takes offerings; the host's bucket may be a remote player's.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Dialogue), nameof(Dialogue.ProcessOffering))]
    private static bool Offering(GameObject offering) => !OnClient && Offerings.BeforeProcess(offering);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Dialogue), nameof(Dialogue.DeniedOffering))]
    private static bool Denied() => !OnClient;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Usable), nameof(Usable.GoToDialogueFunctions))]
    private static bool ItemStory(string fullKnotPath) => Talk.StoryFunctions.Before(fullKnotPath);

    // The host decides when a speaker walks off; clients hear it from TalkSender.Left.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(CharacterStories), nameof(CharacterStories.Leaving))]
    private static bool BeforeLeaving() => !OnClient;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CharacterStories), nameof(CharacterStories.Leaving))]
    private static void AfterLeaving(CharacterStories __instance, bool __runOriginal)
    {
        if (__runOriginal)
            HostTalks.AfterLeaving(__instance);
    }

    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    [HarmonyPatch(typeof(Phone_Interface), nameof(Phone_Interface.ComposeNumber))]
    private static bool Button(Phone_Interface __instance, int number) => PhoneRequests.BeforeCompose(__instance, number);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Phone_Interface), nameof(Phone_Interface.ComposeNumber))]
    private static void Pressed(Phone_Interface __instance, bool __runOriginal)
    {
        if (__runOriginal)
            PhoneRequests.AfterCompose(__instance);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Phone_Interface), nameof(Phone_Interface.ClearScreen))]
    private static void Clearing(Phone_Interface __instance, bool noDistanceCheck, ref bool endPhoneCall) =>
        HostTalks.BeforeClearScreen(__instance, noDistanceCheck, ref endPhoneCall);

    // Walking away from a handset clears it; only for the player who's using it.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Phone_Interface), "OnTriggerExit")]
    private static bool LeftPhone(Phone_Interface __instance, Collider other) => PhoneRequests.BeforeWalkAway(__instance, other);
}
