using Coop.Players;
using Coop.World.Streaming;
using UnityEngine;

namespace Coop.World.Npcs;

/// <summary>
/// An NPC built on the player rig (Player_NPC, Grandpa, the thief, visitors, skeletons): the parts
/// its pose is read from on the host and written to on a client.
/// </summary>
internal sealed class NpcRig
{
    private static readonly int DancingHash = Animator.StringToHash("Dancing");

    public Animator Animator;
    public SkinnedMeshRenderer Skin;
    public SkinnedMeshRenderer[] Hands;
    public Player_Movement Movement;
    public Player_Abilities Abilities;

    private AnimatorBools _bools;
    private bool _hasDancing;
    private bool _readParameters;

    // Host: last pose and clothes sent.
    public NpcPose Sent;
    public bool HasSent;
    public int DressSignature;
    public bool DressSent;

    // Client: poses from the host.
    public readonly TimedRing<NpcPose> Poses = new TimedRing<NpcPose>(6);
    public bool Driving;
    public NpcPose Shown;

    public static NpcRig TryCreate(Brain brain)
    {
        var movement = brain.GetComponent<Player_Movement>();
        var abilities = brain.GetComponent<Player_Abilities>();
        if (movement == null || abilities == null || movement.neetAC == null)
            return null;
        return new NpcRig
        {
            Animator = movement.neetAC,
            Skin = abilities.neetRend,
            Hands = abilities.rendererHands,
            Movement = movement,
            Abilities = abilities,
        };
    }

    /// <summary>Animator parameters only exist once the animator is active, so they're read late.</summary>
    private bool Ready()
    {
        if (_readParameters)
            return _bools != null;
        if (Animator == null || !Animator.isActiveAndEnabled)
            return false;
        _readParameters = true;
        _bools = new AnimatorBools(Animator);
        foreach (var parameter in Animator.parameters)
        {
            if (parameter.nameHash == DancingHash && parameter.type == AnimatorControllerParameterType.Int)
                _hasDancing = true;
        }
        return true;
    }

    public NpcPose Read(Transform root)
    {
        var pose = new NpcPose
        {
            Position = root.position,
            Rotation = root.rotation,
            Speed = 50,
        };
        if (root.gameObject.activeSelf)
            pose.Flags |= NpcPose.Active;
        if (Ready())
        {
            pose.Bools = _bools.Read(Animator);
            pose.Dancing = _hasDancing ? (byte)Mathf.Clamp(Animator.GetInteger(DancingHash), 0, 255) : (byte)0;
            pose.Speed = (byte)Mathf.Clamp(Mathf.RoundToInt(Animator.speed * 50f), 0, 255);
        }
        if (Skin != null && Skin.sharedMesh != null && Skin.sharedMesh.blendShapeCount >= 2)
        {
            if (Skin.GetBlendShapeWeight(0) > 50f)
                pose.Flags |= NpcPose.LeftHand;
            if (Skin.GetBlendShapeWeight(1) > 50f)
                pose.Flags |= NpcPose.RightHand;
        }
        return pose;
    }

    /// <summary>Everything but the position and turn, which go through the body.</summary>
    public void Show(in NpcPose pose)
    {
        if (Ready())
        {
            _bools.Apply(Animator, pose.Bools);
            if (_hasDancing)
                Animator.SetInteger(DancingHash, pose.Dancing);
            Animator.speed = pose.Speed > 0 ? pose.Speed / 50f : 1f;
        }
        bool left = (pose.Flags & NpcPose.LeftHand) != 0;
        bool right = (pose.Flags & NpcPose.RightHand) != 0;
        if (Skin != null && Skin.sharedMesh != null && Skin.sharedMesh.blendShapeCount >= 2)
        {
            Skin.SetBlendShapeWeight(0, left ? 100f : 0f);
            Skin.SetBlendShapeWeight(1, right ? 100f : 0f);
        }
        if (Hands != null && Hands.Length >= 2)
        {
            if (Hands[0] != null)
                Hands[0].enabled = left;
            if (Hands[1] != null)
                Hands[1].enabled = right;
        }
        Shown = pose;
    }
}
