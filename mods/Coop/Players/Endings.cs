using HarmonyLib;
using UnityEngine;

namespace Coop.Players;

/// <summary>How a downed player dies when nobody gets to them. Worse deaths have bigger numbers.</summary>
internal enum Ending : byte
{
    /// <summary>The plain death (<c>DieAccessor</c>): health ran out, a story or the kill timer.</summary>
    Death,
    /// <summary>Skeletonized: a skeleton rises out of the body (Contact_Skeletonizer, the story's SkeletonizePlayer).</summary>
    Skeleton,
    /// <summary>Skeletonized without the skeleton.</summary>
    Hollow,
    /// <summary>Gibbed (<c>Obliterate</c>): an explosion, a booby-trapped wig, the kill timer.</summary>
    Gibbed,
}

/// <summary>
/// Runs the game's own death for an <see cref="Ending"/>, on the body that was lying down, so the effects,
/// loot, story hooks (<c>OnPreDeath</c>) and the new body all come from the game and not from Co-op.
/// </summary>
internal static class Endings
{
    private const string DeadPose = "isDead";
    private const string SlippedPose = "slipped";
    // A lying body is about this far below where a standing one is.
    private const float StandLift = 0.5f;

    private static readonly AccessTools.FieldRef<Player_Movement, Quaternion> PreSlipRotation =
        AccessTools.FieldRefAccess<Player_Movement, Quaternion>("cachedPreSlipRot");
    private static readonly AccessTools.FieldRef<Player_Movement, RigidbodyConstraints> PreSlipConstraints =
        AccessTools.FieldRefAccess<Player_Movement, RigidbodyConstraints>("cachedPreSlipConst");

    public static void Run(Player_Abilities rig, Ending ending)
    {
        switch (ending)
        {
            case Ending.Skeleton:
                StandUpFirst(rig);
                rig.SkeletonizeAccessor(false);
                break;
            case Ending.Hollow:
                StandUpFirst(rig);
                rig.SkeletonizeAccessor(true);
                break;
            case Ending.Gibbed:
                rig.Obliterate();
                break;
            default:
                rig.DieAccessor(false);
                break;
        }
    }

    // The skeleton comes out of a standing body (it copies the body's rotation), so the fall is undone
    // on the spot; dying and gibbing look fine lying down.
    private static void StandUpFirst(Player_Abilities rig)
    {
        if (!rig.TryGetComponent(out Player_Movement movement))
            return;
        movement.neetAC.SetBool(DeadPose, false);
        if (!movement.slipped)
            return;
        var body = rig.GetComponent<Rigidbody>();
        rig.transform.rotation = PreSlipRotation(movement);
        rig.transform.position += Vector3.up * StandLift;
        if (body != null)
        {
            body.constraints = PreSlipConstraints(movement);
            body.velocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
        movement.neetAC.SetBool(SlippedPose, false);
        movement.slipped = false;
    }
}
