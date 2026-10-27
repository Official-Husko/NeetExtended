using UnityEngine;

namespace Coop.Players;

/// <summary>
/// Who the local player means when they try to help a downed player: the one they carry, or the one
/// they look at (a wide aim, since the body lies on the floor).
/// </summary>
internal static class DownedAim
{
    public const float Reach = 2.5f;
    private const float AimRadius = 0.35f;
    private const byte None = byte.MaxValue;

    private static int _playerMask = -1;

    /// <summary>The other player's body in front of the camera, or null.</summary>
    public static PlayerPuppet Looked(float reach = Reach)
    {
        var camera = HouseManager._ != null ? HouseManager._.mainCamera : null;
        if (camera == null)
            return null;
        if (_playerMask < 0)
            _playerMask = LayerMask.GetMask("Player");
        var eye = camera.transform;
        if (!Physics.SphereCast(eye.position, AimRadius, eye.forward, out var hit, reach, _playerMask, QueryTriggerInteraction.Ignore))
            return null;
        return hit.collider.GetComponentInParent<PlayerPuppet>();
    }

    /// <summary>The slot of the downed player to help (carried first, else looked at), or 255.</summary>
    public static byte Patient(float reach = Reach)
    {
        byte carried = Carrying.Carried;
        if (carried != None && Downed.IsDown(carried))
            return carried;
        var looked = Looked(reach);
        return looked != null && Downed.IsDown(looked.Slot) ? looked.Slot : None;
    }
}
