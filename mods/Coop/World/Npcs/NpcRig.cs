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
    private static readonly int DeadHash = Animator.StringToHash("isDead");
    private static readonly int CrouchHash = Animator.StringToHash("isCrouching");
    private static readonly int KickHash = Animator.StringToHash("Kick");

    // The body collider's shape while standing and while crouching, sitting or lying down (the
    // numbers Player_Movement.Update sets).
    private const float StandHeight = 1.67f;
    private const float CrouchHeight = 0.55f;
    private static readonly Vector3 CrouchCenter = new Vector3(0f, -0.6f, 0f);

    public Animator Animator;
    public SkinnedMeshRenderer Skin;
    public SkinnedMeshRenderer[] Hands;
    public Player_Movement Movement;
    public Player_Abilities Abilities;
    public CapsuleCollider Capsule;

    // The sources the rig's own scripts play its sounds on: the body's, then the feet's (same order in every game).
    public AudioSource[] Voices;

    private AnimatorBools _bools;
    private bool _hasDancing;
    private bool _readParameters;

    // Host: last pose and clothes sent.
    public NpcPose Sent;
    public bool HasSent;
    public int DressSignature;
    public bool DressSent;
    public byte KickCount;
    public bool[] VoiceOn;
    public bool[] VoiceNew;
    public AudioClip[] VoiceClip;
    public bool VoicesKnown;
    public int MaskSignature;
    public bool MasksSent;

    // Client: poses from the host.
    public readonly TimedRing<NpcPose> Poses = new TimedRing<NpcPose>(6);
    public bool Driving;
    public NpcPose Shown;
    public readonly System.Collections.Generic.List<GameObject> Masked = new System.Collections.Generic.List<GameObject>(2);
    private bool _hasShown;
    private int _shape = -1;

    public static NpcRig TryCreate(Brain brain)
    {
        var movement = brain.GetComponent<Player_Movement>();
        var abilities = brain.GetComponent<Player_Abilities>();
        if (movement == null || abilities == null || movement.neetAC == null)
            return null;
        var rig = new NpcRig
        {
            Animator = movement.neetAC,
            Skin = abilities.neetRend,
            Hands = abilities.rendererHands,
            Movement = movement,
            Abilities = abilities,
            Capsule = brain.GetComponent<CapsuleCollider>(),
        };
        rig.Voices = FindVoices(abilities, movement);
        rig.VoiceOn = new bool[rig.Voices.Length];
        rig.VoiceNew = new bool[rig.Voices.Length];
        rig.VoiceClip = new AudioClip[rig.Voices.Length];
        return rig;
    }

    private static AudioSource[] FindVoices(Player_Abilities abilities, Player_Movement movement)
    {
        var body = abilities.GetComponents<AudioSource>();
        var feet = movement.feet != null ? movement.feet.GetComponents<AudioSource>() : System.Array.Empty<AudioSource>();
        var all = new AudioSource[body.Length + feet.Length];
        body.CopyTo(all, 0);
        feet.CopyTo(all, body.Length);
        return all;
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
            Kicks = KickCount,
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
            ShapeCollider(Animator.GetBool(CrouchHash));
            if (_hasShown && pose.Kicks != Shown.Kicks)
                Animator.SetTrigger(KickHash);
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
        _hasShown = true;
        DieIfDead();
    }

    // The movement script that does this on the host is switched off here. The first call always
    // sets the shape: the prefab's own height isn't the standing one.
    private void ShapeCollider(bool crouching)
    {
        int shape = crouching ? 1 : 0;
        if (shape == _shape || Capsule == null)
            return;
        _shape = shape;
        Capsule.center = crouching ? CrouchCenter : Vector3.zero;
        Capsule.height = crouching ? CrouchHeight : StandHeight;
    }

    // The host's NPC died: this copy becomes a corpse the game's way (it can be picked up, it bleeds
    // when carried), without the death's own effects; those happen on the host.
    private void DieIfDead()
    {
        if (Abilities == null || Abilities.dead || !Ready() || !Animator.GetBool(DeadHash))
            return;
        try
        {
            Abilities.DieAccessor(noEffects: true);
        }
        catch (System.Exception e)
        {
            CoopMod.Log.Debug($"World: making {Abilities.name} a corpse here failed: {e.Message}");
        }
    }
}
