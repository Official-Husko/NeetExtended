using UnityEngine;

namespace Coop.Players;

/// <summary>
/// Where the wearer's camera is on another player's body: a point on the head bone that sits where the
/// player's camera does when they stand (the game keeps it at <c>CameraControl.initCamPos</c> above the
/// body's origin, facing the way the body does). Masks are placed relative to the camera in the
/// wearer's game, so on a body they are placed relative to this point, and ride the head through
/// every pose (crouching, sitting, lying, looking up). It's scaled to cancel the head bone's tiny
/// scale, so what hangs on it keeps its size.
/// </summary>
internal static class PuppetEyes
{
    // CameraControl.initCamPos, for when the camera script isn't at hand.
    private static readonly Vector3 StandingCamera = new Vector3(0f, 0.5345f, 0f);

    public static Transform Create(Transform root, Animator animator, Transform head)
    {
        if (head == null)
            return null;
        // The head is only where it is standing once the animator has posed the body.
        if (animator != null)
            animator.Update(0f);
        var eyes = new GameObject("Eyes").transform;
        eyes.SetParent(head, false);
        var camera = HouseManager._ != null && HouseManager._.mainCamera != null ? HouseManager._.mainCamera.GetComponent<CameraControl>() : null;
        eyes.SetPositionAndRotation(root.TransformPoint(camera != null ? camera.initCamPos : StandingCamera), root.rotation);
        var scale = head.lossyScale;
        eyes.localScale = new Vector3(Safe(scale.x), Safe(scale.y), Safe(scale.z));
        return eyes;
    }

    private static float Safe(float scale) => Mathf.Abs(scale) > 1e-5f ? 1f / scale : 1f;
}
