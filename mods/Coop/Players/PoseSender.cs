using Coop.Net;
using Coop.Session;
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
            Pitch = _camera != null ? _camera.localEulerAngles.x : 0f,
            Bools = _animator != null ? _bools.Read(_animator) : (ushort)0,
            AnimSpeed = _animator != null ? _animator.speed : 1f,
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
        var root = _player.transform;
        var left = _abilities.rigidHands[0];
        var right = _abilities.rigidHands[1];
        if (left != null)
        {
            pose.LeftHand = root.InverseTransformPoint(left.position);
            pose.LeftHandTurn = Quaternion.Inverse(root.rotation) * left.rotation;
        }
        if (right != null)
        {
            pose.RightHand = root.InverseTransformPoint(right.position);
            pose.RightHandTurn = Quaternion.Inverse(root.rotation) * right.rotation;
        }
        var shown = _abilities.rendererHands;
        if (shown != null && shown.Length >= 2)
            pose.HandsShown = (byte)((shown[0] != null && shown[0].enabled ? 1 : 0) | (shown[1] != null && shown[1].enabled ? 2 : 0));
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
