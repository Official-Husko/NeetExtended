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
            Yaw = _player.transform.eulerAngles.y,
            Pitch = _camera != null ? _camera.localEulerAngles.x : 0f,
            Bools = _animator != null ? _bools.Read(_animator) : (ushort)0,
            AnimSpeed = _animator != null ? _animator.speed : 1f,
        };
        var message = Outbox.Begin(MessageIds.Pose);
        pose.Write(message, CoopSession.Local.Slot);
        Outbox.ToPlayers(message, Channel.Stream);
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
        }
        return true;
    }
}
