using HarmonyLib;
using NeetModKit.Events;

namespace Coop.Session;

/// <summary>
/// Keeps everyone in the house while a co-op game runs. The game leaves the house for the dream
/// (sleeping with your eyes shut), for a few story scenes and for some story deaths; any of those
/// would take one player out of the shared world, or the whole world away from everyone when the host
/// goes. In co-op you can rest but not dream, and the story's scene jumps wait. Going back to the main
/// menu leaves the game.
/// </summary>
[HarmonyPatch]
internal static class ScenePolicy
{
    // The game switches to the dream once eyes have been shut for 20 seconds.
    private const float DreamAt = 20f;
    private const float HoldAt = DreamAt - 0.5f;

    public static void Init(CoopMod mod) =>
        GameEvents.SceneLoaded.Subscribe(mod, scene =>
        {
            if (scene == "MainMenu" && CoopSession.InWorld)
                CoopSession.Leave("You went back to the main menu.");
        });

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Player_Movement), "Update")]
    private static void NoDreams(Player_Movement __instance)
    {
        if (__instance.dreamTimer > HoldAt && CoopSession.InWorld)
            __instance.dreamTimer = HoldAt;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(LoadSceneOnDeath), "SwitchScene")]
    private static bool NoDeathScene() => Allow("a story death");

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Dialogue), nameof(Dialogue.SceneSwitchInstant))]
    private static bool NoStoryScene(string scene) => Allow("the story going to " + scene);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Dialogue), nameof(Dialogue.SceneSwitchKnockout))]
    private static bool NoStoryKnockout(string scene) => Allow("the story going to " + scene);

    private static bool Allow(string what)
    {
        if (!CoopSession.InWorld)
            return true;
        CoopMod.Log.Info($"Stayed in the house instead of {what} (co-op).");
        return false;
    }
}
