using UnityEngine;

namespace Coop.Players;

/// <summary>
/// Reaches a body's arms to where its player's hands really are. The animator poses the arms the way
/// it always does; after it, each raised hand (holding something, reaching for something, punching)
/// gets a two-bone solve on the body's shoulder-elbow-hand bones towards the hand's streamed place,
/// blended in and out so an arm goes back to the animation when its hand is let go. The elbow keeps
/// the side the animation had it on, a little out and down. The first-person arms the hands come from
/// are longer than the body's, so an arm that can't reach stretches along its bones (up to
/// <see cref="MaxStretch"/>) and then simply points at the hand. Hands the pose doesn't show are left alone.
/// </summary>
internal sealed class PuppetArms
{
    // Full strength in about 0.15 s.
    private const float BlendSpeed = 6.5f;
    private const float ElbowOut = 0.35f;
    private const float MaxStretch = 1.4f;
    // Stops a fully stretched arm from locking straight.
    private const float Slack = 0.002f;

    private readonly Arm _left, _right;

    private PuppetArms(Arm left, Arm right)
    {
        _left = left;
        _right = right;
    }

    /// <summary>Finds the bones of both arms in the copied model; null when it doesn't have them.</summary>
    public static PuppetArms Create(Transform model)
    {
        var left = Arm.Find(model, "left", -1f);
        var right = Arm.Find(model, "right", 1f);
        return left != null && right != null ? new PuppetArms(left, right) : null;
    }

    /// <summary>Call after the animator has posed the body. <paramref name="active"/> is false while the body is down or carried.</summary>
    public void Apply(Transform root, in PlayerPose pose, PuppetHeld held, bool active, float deltaTime)
    {
        _left.Apply(Target(root, pose, held, false), active && pose.IsShown(PlayerPose.LeftShown), deltaTime);
        _right.Apply(Target(root, pose, held, true), active && pose.IsShown(PlayerPose.RightShown), deltaTime);
    }

    // The thing in the hand when this game has it, else where the player's own game had the hand.
    private static Vector3 Target(Transform root, in PlayerPose pose, PuppetHeld held, bool right)
    {
        Vector3 hand = root.TransformPoint(right ? pose.RightHand : pose.LeftHand);
        return held != null && held.TryGet(pose, right, hand, out var item) ? item : hand;
    }

    private sealed class Arm
    {
        private readonly Transform _upper, _fore, _end;
        private readonly float _side;
        private readonly Vector3 _upperAxis, _foreAxis;
        private float _weight;
        private bool _stretched;

        private Arm(Transform upper, Transform fore, Transform end, float side)
        {
            _upper = upper;
            _fore = fore;
            _end = end;
            _side = side;
            _upperAxis = LongestAxis(fore.localPosition);
            _foreAxis = LongestAxis(end.localPosition);
        }

        // The local axis a bone's child lies along: stretching the arm scales the bone along it.
        private static Vector3 LongestAxis(Vector3 direction)
        {
            float x = Mathf.Abs(direction.x), y = Mathf.Abs(direction.y), z = Mathf.Abs(direction.z);
            return x >= y && x >= z ? Vector3.right : y >= z ? Vector3.up : Vector3.forward;
        }

        private void Stretch(float amount)
        {
            _upper.localScale = Vector3.one + _upperAxis * (amount - 1f);
            _fore.localScale = Vector3.one + _foreAxis * (amount - 1f);
            _stretched = amount != 1f;
        }

        public static Arm Find(Transform model, string side, float sign)
        {
            var upper = PuppetBuilder.FindDeep(model, side + "upperarm");
            var fore = PuppetBuilder.FindDeep(model, side + "forearm");
            var end = PuppetBuilder.FindDeep(model, side + "forearm_end");
            return upper != null && fore != null && end != null ? new Arm(upper, fore, end, sign) : null;
        }

        public void Apply(Vector3 target, bool reach, float deltaTime)
        {
            _weight = Mathf.MoveTowards(_weight, reach ? 1f : 0f, BlendSpeed * deltaTime);
            if (_weight <= 0f)
            {
                if (_stretched)
                    Stretch(1f);
                return;
            }
            float blend = _weight * _weight * (3f - 2f * _weight);

            var animatedUpper = _upper.localRotation;
            var animatedFore = _fore.localRotation;
            // Scaled in proportion to the blend so the stretch comes and goes with the reach.
            Stretch(1f);
            float stretch = Mathf.Clamp(Vector3.Distance(_upper.position, target) / FullLength(), 1f, MaxStretch);
            Stretch(Mathf.Lerp(1f, stretch, blend));
            if (!Solve(target))
                return;
            _upper.localRotation = Quaternion.Slerp(animatedUpper, _upper.localRotation, blend);
            _fore.localRotation = Quaternion.Slerp(animatedFore, _fore.localRotation, blend);
        }

        private float FullLength() =>
            (_fore.position - _upper.position).magnitude + (_end.position - _fore.position).magnitude;

        private bool Solve(Vector3 target)
        {
            Vector3 shoulder = _upper.position;
            Vector3 elbow = _fore.position;
            float upperLength = (elbow - shoulder).magnitude;
            float foreLength = (_end.position - elbow).magnitude;
            Vector3 toTarget = target - shoulder;
            float distance = toTarget.magnitude;
            if (distance < 1e-4f || upperLength < 1e-4f || foreLength < 1e-4f)
                return false;
            Vector3 direction = toTarget / distance;

            float reach = Mathf.Clamp(distance, Mathf.Abs(upperLength - foreLength) + Slack, upperLength + foreLength - Slack);
            float along = (upperLength * upperLength - foreLength * foreLength + reach * reach) / (2f * reach);
            float height = Mathf.Sqrt(Mathf.Max(0f, upperLength * upperLength - along * along));

            // Bend towards where the animation has the elbow, nudged out and down.
            Vector3 bend = elbow - shoulder;
            bend -= direction * Vector3.Dot(bend, direction);
            bend += (_upper.root.right * _side * ElbowOut - Vector3.up * ElbowOut) * upperLength;
            bend -= direction * Vector3.Dot(bend, direction);
            if (bend.sqrMagnitude < 1e-8f)
                bend = Vector3.down;
            Vector3 newElbow = shoulder + direction * along + bend.normalized * height;

            _upper.rotation = Quaternion.FromToRotation(elbow - shoulder, newElbow - shoulder) * _upper.rotation;
            Vector3 hit = shoulder + direction * reach;
            _fore.rotation = Quaternion.FromToRotation(_end.position - _fore.position, hit - _fore.position) * _fore.rotation;
            return true;
        }
    }
}
