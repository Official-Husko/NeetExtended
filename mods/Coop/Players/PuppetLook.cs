using UnityEngine;

namespace Coop.Players;

/// <summary>
/// Makes a body look where its player looks: the head, the neck and the top of the spine share the
/// up and down tilt (the animator only turns the body). The tilt is capped so the head can't fold into
/// the chest, and each bone turns around the body's own right-hand axis so it doesn't matter how the
/// bones are laid out.
/// </summary>
internal sealed class PuppetLook
{
    private const float MaxUp = 70f;
    private const float MaxDown = 55f;
    private const float ChestShare = 0.15f;
    private const float NeckShare = 0.3f;
    private const float HeadShare = 0.45f;

    private readonly Transform _chest, _neck, _head;

    private PuppetLook(Transform chest, Transform neck, Transform head)
    {
        _chest = chest;
        _neck = neck;
        _head = head;
    }

    public static PuppetLook Create(Transform model)
    {
        var head = PuppetBuilder.FindDeep(model, "headbone");
        if (head == null)
            return null;
        return new PuppetLook(PuppetBuilder.FindDeep(model, "chestbone"), PuppetBuilder.FindDeep(model, "neckbone"), head);
    }

    /// <summary>Call after the animator has posed the body; <paramref name="pitch"/> is degrees, positive looks down.</summary>
    public void Apply(Transform root, float pitch)
    {
        pitch = Mathf.Clamp(pitch, -MaxUp, MaxDown);
        Vector3 axis = root.right;
        Tilt(_chest, axis, pitch * ChestShare);
        Tilt(_neck, axis, pitch * NeckShare);
        Tilt(_head, axis, pitch * HeadShare);
    }

    private static void Tilt(Transform bone, Vector3 axis, float degrees)
    {
        if (bone != null)
            bone.rotation = Quaternion.AngleAxis(degrees, axis) * bone.rotation;
    }
}
