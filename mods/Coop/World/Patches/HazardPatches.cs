using Coop.World.Forwarding;
using HarmonyLib;
using UnityEngine;

namespace Coop.World.Patches;

/// <summary>
/// Notes when this game's own kill planes and teleporters reach this game's player, so a move another
/// game sends for the same touch isn't done twice (see <see cref="BodyMoves"/>).
/// </summary>
[HarmonyPatchCategory(WorldPatches.World)]
[HarmonyPatch]
internal static class HazardPatches
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(KillPlane), "OnCollisionEnter")]
    private static void AfterKillPlane(Collision other) => BodyMoves.OwnBodyMoved(other.gameObject);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Teleporter), "OnTriggerEnter")]
    private static void AfterTeleporter(Collider other) => BodyMoves.OwnBodyMoved(other.gameObject);
}
