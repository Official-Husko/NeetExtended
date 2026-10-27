using HarmonyLib;
using UnityEngine;

namespace Coop.Players;

/// <summary>
/// The host's save keeps the masks a player who left had on, parked under the house
/// (<see cref="MaskParking"/>), for when they come back. The game's load gives every item back a
/// falling body (it never applies the saved kinematic flag), so after a load those masks are parked
/// again: any mask not on a head and below the house stays kinematic at the parking spot. Runs
/// whenever Co-op is installed, in or out of a session, since a save from a co-op game can be loaded
/// alone.
/// </summary>
[HarmonyPatch(typeof(SaveLoadHandler), nameof(SaveLoadHandler.LoadAll))]
internal static class MaskParkingPatch
{
    // Well below any floor, well above where a parked mask that fell for a while would be.
    private const float ParkedBelow = -400f;

    private static void Postfix()
    {
        foreach (var mask in Object.FindObjectsOfType<MaskFace>())
        {
            if (mask.onHead || mask.transform.parent != null || mask.transform.position.y > ParkedBelow
                || !mask.TryGetComponent(out Rigidbody body) || body.isKinematic)
                continue;
            body.isKinematic = true;
            body.velocity = Vector3.zero;
            mask.transform.position = MaskParking.Spot;
        }
    }
}
