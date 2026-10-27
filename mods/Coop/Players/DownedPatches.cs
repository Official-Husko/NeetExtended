using Coop.Session;
using HarmonyLib;
using UnityEngine;

namespace Coop.Players;

/// <summary>
/// The two hooks downed players need: dying becomes going down (for this game's own player, in a
/// co-op game), and using food while looking at a downed player feeds it to them instead of you.
/// </summary>
[HarmonyPatch]
internal static class DownedPatches
{
    private const float ReviveReach = 3f;
    // Generous, since the body is lying on the floor.
    private const float ReviveAimRadius = 0.35f;
    private static int _playerMask = -1;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Player_Abilities), nameof(Player_Abilities.DieAccessor), typeof(bool))]
    private static bool GoDownInstead(Player_Abilities __instance, bool noEffects, bool __runOriginal) =>
        // Something else (god mode) already stopped this death.
        __runOriginal && (noEffects || !Downed.TryGoDown(__instance));

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Food), nameof(Food.Use))]
    private static bool FeedDownedPlayer(Food __instance)
    {
        if (!CoopSession.InWorld || HouseManager._ == null || HouseManager._.mainCamera == null)
            return true;
        if (!__instance.TryGetComponent(out Usable usable) || usable.player != HouseManager._.player)
            return true;

        if (_playerMask < 0)
            _playerMask = LayerMask.GetMask("Player");
        var eye = HouseManager._.mainCamera.transform;
        if (!Physics.SphereCast(eye.position, ReviveAimRadius, eye.forward, out var hit, ReviveReach, _playerMask, QueryTriggerInteraction.Ignore))
            return true;
        var puppet = hit.collider.GetComponentInParent<PlayerPuppet>();
        if (puppet == null || !Downed.IsDown(puppet.Slot))
            return true;

        Object.Destroy(__instance.gameObject);
        Downed.Revive(puppet.Slot);
        return false;
    }
}
