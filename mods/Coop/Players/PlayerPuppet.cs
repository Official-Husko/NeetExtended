using System.Collections.Generic;
using Coop.Net;
using UnityEngine;

namespace Coop.Players;

/// <summary>
/// Another player's body in this game: a copy of the player's model with none of the game's player
/// scripts, moved by their poses, played back slightly behind (<see cref="RemoteClock"/>) so
/// movement stays smooth.
/// </summary>
internal sealed class PlayerPuppet : MonoBehaviour
{
    // Further than this from where it should be (a teleport, a respawn) it jumps instead of sliding.
    private const float SnapDistance = 3f;

    private readonly PoseBuffer _poses = new PoseBuffer();
    private Rigidbody _body;
    private Animator _animator;
    private AnimatorBools _bools;
    private Transform _head;
    private SkinnedMeshRenderer _skin;
    private SkinnedMeshRenderer[] _hands;

    // Meshes copied for this body alone; they go with it.
    internal readonly List<Mesh> OwnedMeshes = new List<Mesh>();

    public byte Slot { get; private set; }

    internal Nameplate Plate { get; set; }

    public void Init(byte slot, Animator animator, Transform head, SkinnedMeshRenderer skin, SkinnedMeshRenderer[] hands)
    {
        Slot = slot;
        _body = GetComponent<Rigidbody>();
        _animator = animator;
        _bools = animator != null ? new AnimatorBools(animator) : null;
        _head = head;
        _skin = skin;
        _hands = hands;
    }

    public void Dress(in Appearance look) => PuppetDresser.Apply(_skin, _hands, look);

    public void SetDowned(bool down) => Plate?.SetDowned(down);

    public void Push(in PlayerPose pose)
    {
        bool first = _poses.IsEmpty;
        _poses.Add(pose);
        if (first)
            Place(pose, snap: true);
    }

    private void FixedUpdate()
    {
        if (!_poses.IsEmpty)
            Place(_poses.Sample(RemoteClock.PlaybackTime(Slot)), snap: false);
    }

    private void LateUpdate()
    {
        if (_poses.IsEmpty)
            return;
        var pose = _poses.Sample(RemoteClock.PlaybackTime(Slot));
        if (_animator != null)
        {
            _bools.Apply(_animator, pose.Bools);
            _animator.speed = pose.AnimSpeed > 0f ? pose.AnimSpeed : 1f;
        }
        // Looking up and down tilts the head after the animator has posed it.
        if (_head != null)
            _head.localRotation *= Quaternion.Euler(Mathf.Clamp(Mathf.DeltaAngle(0f, pose.Pitch), -60f, 60f) * 0.5f, 0f, 0f);
    }

    private void OnDestroy()
    {
        foreach (var mesh in OwnedMeshes)
        {
            if (mesh != null)
                Destroy(mesh);
        }
    }

    private void Place(in PlayerPose pose, bool snap)
    {
        var rotation = pose.Rotation;
        if (snap || (transform.position - pose.Position).sqrMagnitude > SnapDistance * SnapDistance)
        {
            transform.SetPositionAndRotation(pose.Position, rotation);
            return;
        }
        _body.MovePosition(pose.Position);
        _body.MoveRotation(rotation);
    }
}
