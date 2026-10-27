using UnityEngine;

namespace Coop.Players;

/// <summary>Where a carried player's body rides: across the carrier's shoulders.</summary>
internal static class CarryPose
{
    // From the carrier's body root (about hip height) up to the shoulders, and a little forward.
    private const float Up = 0.55f;
    private const float Forward = 0.05f;

    public static Vector3 Position(Transform carrier) => carrier.position + carrier.up * Up + carrier.forward * Forward;

    // Lying sideways across the shoulders, face down.
    public static Quaternion Rotation(Transform carrier) => carrier.rotation * Quaternion.Euler(90f, 90f, 0f);
}
