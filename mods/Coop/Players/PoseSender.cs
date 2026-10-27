using Coop.Net;
using Coop.Session;
using Coop.World.Objects;
using UnityEngine;

namespace Coop.Players;

/// <summary>Sends this game's player's pose about 30 times a second while in a shared world.</summary>
internal static class PoseSender
{
    private const float Interval = 1f / 30f;

    private static float _next;
    private static GameObject _player;
    private static Transform _camera;
    private static Animator _animator;
    private static AnimatorBools _bools;
    private static Player_Abilities _abilities;

    public static void Init() => CoopLoop.LateUpdate += Tick;

    private static void Tick()
    {
        if (!CoopSession.InWorld || Time.time < _next || !Find())
            return;
        _next = Time.time + Interval;

        var pose = new PlayerPose
        {
            Time = Time.time,
            Position = _player.transform.position,
            Rotation = _player.transform.rotation,
            Pitch = _camera != null ? Mathf.DeltaAngle(0f, _camera.localEulerAngles.x) : 0f,
            Bools = _animator != null ? _bools.Read(_animator) : (ushort)0,
            AnimSpeed = _animator != null ? _animator.speed : 1f,
            Kicks = KickSignal.Count,
            LeftHandTurn = Quaternion.identity,
            RightHandTurn = Quaternion.identity,
        };
        ReadHands(ref pose);
        var message = Outbox.Begin(MessageIds.Pose);
        pose.Write(message, CoopSession.Local.Slot);
        Outbox.ToPlayers(message, Channel.Stream);
    }

    private static void ReadHands(ref PlayerPose pose)
    {
        if (_abilities == null || _abilities.rigidHands == null || _abilities.rigidHands.Length < 2)
            return;
        var shown = _abilities.rendererHands;
        if (shown == null || shown.Length < 2)
            return;
        var root = _player.transform;
        var left = _abilities.rigidHands[0];
        var right = _abilities.rigidHands[1];
        if (left != null && shown[0] != null && shown[0].enabled)
        {
            pose.HandsShown |= PlayerPose.LeftShown;
            pose.LeftHand = root.InverseTransformPoint(left.position);
            pose.LeftHandTurn = Quaternion.Inverse(root.rotation) * left.rotation;
            Holds(ref pose, 0);
        }
        if (right != null && shown[1] != null && shown[1].enabled)
        {
            pose.HandsShown |= PlayerPose.RightShown;
            pose.RightHand = root.InverseTransformPoint(right.position);
            pose.RightHandTurn = Quaternion.Inverse(root.rotation) * right.rotation;
            Holds(ref pose, 1);
        }
    }

    // A hand holding something the other games have too says which, so their body's hand meets it.
    private static void Holds(ref PlayerPose pose, int hand)
    {
        var item = _abilities.pickups != null && _abilities.pickups.Length > hand ? _abilities.pickups[hand] : null;
        var shared = item != null ? NetObjects.Find(item) : null;
        if (shared == null || !shared.Ref.IsValid)
            return;
        if (hand == 0)
        {
            pose.HandsShown |= PlayerPose.LeftHolds;
            pose.HeldLeft = shared.Ref;
        }
        else
        {
            pose.HandsShown |= PlayerPose.RightHolds;
            pose.HeldRight = shared.Ref;
        }
    }

    // The player object changes on respawn and on every scene load.
    private static bool Find()
    {
        var player = HouseManager._ != null ? HouseManager._.player : null;
        if (player == null)
            return false;
        if (player != _player)
        {
            _player = player;
            _camera = HouseManager._.mainCamera != null ? HouseManager._.mainCamera.transform : null;
            _animator = player.TryGetComponent(out Player_Movement movement) ? movement.neetAC : null;
            _bools = _animator != null ? new AnimatorBools(_animator) : null;
            _abilities = player.GetComponent<Player_Abilities>();
        }
        return true;
    }
}
