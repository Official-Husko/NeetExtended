using UnityEngine;

namespace Coop.Players;

/// <summary>
/// Experimental (setting "Physics hands on other players"): another player's first-person arms, as
/// they see them. Each arm is a chain of jointed bones from the shoulder to the hand, skinned to the
/// arm mesh; the game pulls the hand end with forces. On the copy the hand end follows where that
/// player's hand really is (sent in the pose) and the joints bend the arm the way the game's physics
/// does. The arms have no colliders here, so they can't push anything in the shared house.
/// </summary>
internal sealed class PuppetHands
{
    private const string LeftArm = "NeetHandL";
    private const string RightArm = "NeetHandR";
    private const string HandEnd = "SmallFinger_L.004_end";
    private const string ArmMesh = "NeetHand";

    private readonly Rigidbody _left, _right;
    private readonly Renderer _leftMesh, _rightMesh;
    private SkinnedMeshRenderer _body;

    private PuppetHands(Rigidbody left, Rigidbody right, Renderer leftMesh, Renderer rightMesh)
    {
        _left = left;
        _right = right;
        _leftMesh = leftMesh;
        _rightMesh = rightMesh;
    }

    /// <summary>Sets up the copied model's arms; null when the model doesn't have them.</summary>
    public static PuppetHands Create(GameObject model, Rigidbody body)
    {
        foreach (var collider in model.GetComponentsInChildren<Collider>(true))
            Object.DestroyImmediate(collider);

        // The chains were joined to the player's own body, which the copy doesn't have.
        foreach (var joint in model.GetComponentsInChildren<Joint>(true))
        {
            if (joint.connectedBody == null || !joint.connectedBody.transform.IsChildOf(model.transform))
                joint.connectedBody = body;
        }

        var left = End(model.transform, LeftArm);
        var right = End(model.transform, RightArm);
        if (left == null || right == null)
            return null;
        return new PuppetHands(left, right, Mesh(model.transform, LeftArm), Mesh(model.transform, RightArm));
    }

    /// <summary>The body mesh, whose own arm shrinks away (blend shapes 0 and 1, as in the game) while the physics arm shows.</summary>
    public void UseBody(SkinnedMeshRenderer body) => _body = body;

    /// <summary>Moves the hand ends to where the pose has them, relative to the body.</summary>
    public void Follow(Transform root, in PlayerPose pose)
    {
        // The pose only carries the hands that are up; a hand that isn't stays where it was, out of sight.
        if (pose.IsShown(PlayerPose.LeftShown))
        {
            _left.MovePosition(root.TransformPoint(pose.LeftHand));
            _left.MoveRotation(root.rotation * pose.LeftHandTurn);
        }
        if (pose.IsShown(PlayerPose.RightShown))
        {
            _right.MovePosition(root.TransformPoint(pose.RightHand));
            _right.MoveRotation(root.rotation * pose.RightHandTurn);
        }
        if (_leftMesh != null)
            _leftMesh.enabled = pose.IsShown(PlayerPose.LeftShown);
        if (_rightMesh != null)
            _rightMesh.enabled = pose.IsShown(PlayerPose.RightShown);
        if (_body != null && _body.sharedMesh != null && _body.sharedMesh.blendShapeCount >= 2)
        {
            _body.SetBlendShapeWeight(0, pose.IsShown(PlayerPose.LeftShown) ? 100f : 0f);
            _body.SetBlendShapeWeight(1, pose.IsShown(PlayerPose.RightShown) ? 100f : 0f);
        }
    }

    private static Rigidbody End(Transform model, string arm)
    {
        var side = Find(model, arm);
        var end = side != null ? Find(side, HandEnd) : null;
        if (end == null || !end.TryGetComponent(out Rigidbody body))
            return null;
        body.isKinematic = true;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        return body;
    }

    private static Renderer Mesh(Transform model, string arm)
    {
        var side = Find(model, arm);
        var mesh = side != null ? side.Find(ArmMesh) : null;
        return mesh != null ? mesh.GetComponent<Renderer>() : null;
    }

    private static Transform Find(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name)
                return child;
            var found = Find(child, name);
            if (found != null)
                return found;
        }
        return null;
    }
}
