using UnityEngine;

namespace PerformanceFixes.Logging;

/// <summary>
/// Velocity setters that leave kinematic bodies alone. Unity ignores a velocity set on a
/// kinematic rigidbody anyway, but logs "Setting linear velocity of a kinematic body is not
/// supported." with a stack trace each time. The game does that every physics step while the
/// player or an NPC is dead, a skeleton or hanging: 64,811 warnings in one session.
/// </summary>
internal static class QuietPhysics
{
    public static void SetVelocity(Rigidbody body, Vector3 velocity)
    {
        if (!body.isKinematic)
            body.velocity = velocity;
    }

    public static void SetAngularVelocity(Rigidbody body, Vector3 velocity)
    {
        if (!body.isKinematic)
            body.angularVelocity = velocity;
    }
}
