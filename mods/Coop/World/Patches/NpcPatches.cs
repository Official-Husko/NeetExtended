using Coop.World.Npcs;
using Coop.World.Objects;
using HarmonyLib;
using UnityEngine;

namespace Coop.World.Patches;

/// <summary>
/// What an NPC rig does that the animator doesn't carry, caught on the host (one hook is a client's:
/// stripping a corpse goes to the host's NPC). A kick bumps the rig's counter, which travels with its
/// pose (<see cref="NpcPose"/>); dressing and undressing go out as the rig's clothes
/// (<see cref="NpcClothes"/>). The rigs run the player's own scripts, so these are the player's methods,
/// filtered to the ones that belong to a shared NPC.
/// </summary>
[HarmonyPatchCategory(WorldPatches.World)]
[HarmonyPatch]
internal static class NpcPatches
{
    private static readonly AccessTools.FieldRef<Player_Abilities, Coroutine> KickRoutine =
        AccessTools.FieldRefAccess<Player_Abilities, Coroutine>("kickCorout");

    private static bool Hosting => WorldSide.Running && WorldSide.IsHost;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Player_Abilities), nameof(Player_Abilities.Kick))]
    private static void BeforeKick(Player_Abilities __instance, out bool __state) =>
        __state = KickRoutine(__instance) == null;

    // The game starts the kick's impact only when something is in reach and no kick is still landing.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Player_Abilities), nameof(Player_Abilities.Kick))]
    private static void Kicked(Player_Abilities __instance, bool __state)
    {
        if (!Hosting)
            return;
        var rig = NetObjects.Find(__instance.gameObject)?.Rig;
        if (rig == null)
            return;
        rig.KickCount++;
        rig.KickHit = __state && KickRoutine(__instance) != null;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Player_Abilities), nameof(Player_Abilities.PutOnClothes))]
    private static void DressedUp(Player_Abilities __instance)
    {
        if (Hosting)
            NpcClothes.Changed(__instance);
    }

    // A player stripping a corpse in a client's game: the host's NPC is the one that undresses.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(ClothRemover), nameof(ClothRemover.Use))]
    private static bool StripOnHost(ClothRemover __instance)
    {
        if (!WorldSide.Running || WorldSide.IsHost || !__instance.TryGetComponent(out Player_Abilities abilities))
            return true;
        if (!NpcClothes.RequestUndress(abilities))
            return true;
        if (__instance.TryGetComponent(out Usable usable))
            usable.PreventOtherUse();
        return false;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Player_Abilities), nameof(Player_Abilities.RemoveClothing))]
    private static void Undressed(Player_Abilities __instance)
    {
        if (Hosting)
            NpcClothes.Changed(__instance);
    }
}
