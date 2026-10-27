using Coop.World.Npcs;
using Coop.World.Objects;
using HarmonyLib;
using UnityEngine;

namespace Coop.World.Patches;

/// <summary>
/// Host: what an NPC rig does that the animator doesn't carry. A kick bumps the rig's counter, which
/// travels with its pose (<see cref="NpcPose"/>). The rigs run the player's own scripts, so these are the
/// player's methods, filtered to the ones that belong to a shared NPC.
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
}
