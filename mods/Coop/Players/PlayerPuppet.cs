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
    private static readonly int KickTrigger = Animator.StringToHash("Kick");

    // Further than this from where it should be (a teleport, a respawn) it jumps instead of sliding.
    private const float SnapDistance = 3f;

    private readonly PoseBuffer _poses = new PoseBuffer();
    private Rigidbody _body;
    private CapsuleCollider _capsule;
    private Animator _animator;
    private AnimatorBools _bools;
    private PuppetLook _look;
    private PuppetArms _arms;
    private PuppetHeld _held;
    private bool _downed;
    private byte _kicksSeen;
    private bool _kicksKnown;
    private SkinnedMeshRenderer _skin;
    private SkinnedMeshRenderer[] _hands;

    // Meshes copied for this body alone; they go with it.
    internal readonly List<Mesh> OwnedMeshes = new List<Mesh>();

    public byte Slot { get; private set; }

    internal Nameplate Plate { get; set; }

    /// <summary>The experimental physics arms, or null for the body's own arms.</summary>
    internal PuppetHands Hands { get; set; }

    /// <summary>
    /// This game's player carrying this body (<see cref="Carrying"/>): it rides on their shoulder here,
    /// ahead of its own stream, and its colliders are off so it can't push the carrier.
    /// </summary>
    internal Transform Shoulder { get; private set; }

    /// <summary>Where the body is, for checks against the house's hazards (kill planes, teleporters).</summary>
    internal Bounds Bounds => _capsule != null ? _capsule.bounds : new Bounds(transform.position, Vector3.one);

    internal void Carry(Transform carrier)
    {
        Shoulder = carrier;
        foreach (var collider in GetComponentsInChildren<Collider>())
            collider.enabled = carrier == null;
    }

    public void Init(byte slot, Animator animator, SkinnedMeshRenderer skin, SkinnedMeshRenderer[] hands)
    {
        Slot = slot;
        _body = GetComponent<Rigidbody>();
        _capsule = GetComponent<CapsuleCollider>();
        _animator = animator;
        _bools = animator != null ? new AnimatorBools(animator) : null;
        _look = animator != null ? PuppetLook.Create(animator.transform) : null;
        _arms = animator != null ? PuppetArms.Create(animator.transform) : null;
        _held = new PuppetHeld(slot);
        _skin = skin;
        _hands = hands;
    }

    public void Dress(in Appearance look) => PuppetDresser.Apply(_skin, _hands, look);

    public void SetDowned(bool down)
    {
        _downed = down;
        Plate?.SetDowned(down);
    }

    public void Push(in PlayerPose pose)
    {
        bool first = _poses.IsEmpty;
        _poses.Add(pose);
        if (first)
            Place(pose, snap: true);
    }

    private void FixedUpdate()
    {
        if (Shoulder != null)
        {
            _body.MovePosition(CarryPose.Position(Shoulder));
            _body.MoveRotation(CarryPose.Rotation(Shoulder));
            return;
        }
        if (_poses.IsEmpty)
            return;
        var pose = _poses.Sample(RemoteClock.PlaybackTime(Slot));
        Place(pose, snap: false);
        Hands?.Follow(transform, pose);
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
            PlayKick(pose.Kicks);
        }
        // The animator has posed the body; the head and the arms follow where the player looks and reaches.
        // A body that is down or carried keeps the pose it was given.
        bool upright = Shoulder == null && !_downed && Vector3.Dot(transform.up, Vector3.up) > 0.7f;
        if (upright)
            _look?.Apply(transform, pose.Pitch);
        if (Hands == null)
            _arms?.Apply(transform, pose, _held, upright, Time.deltaTime);
    }

    // The first pose only sets the count; a kick played before this body existed isn't replayed.
    private void PlayKick(byte kicks)
    {
        if (_kicksKnown && KickSignal.IsNew(_kicksSeen, kicks))
            _animator.SetTrigger(KickTrigger);
        _kicksSeen = kicks;
        _kicksKnown = true;
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
