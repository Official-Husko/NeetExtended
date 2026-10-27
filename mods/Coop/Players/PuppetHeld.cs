using Coop.World.Objects;
using UnityEngine;

namespace Coop.Players;

/// <summary>
/// Finds what a body's hands hold in this game, so the arms can reach the thing itself instead of
/// the place the player's own game had the hand (the item in the player's hand follows its own
/// stream and is a little off from it). Looked up when the pose names a different item, not every frame.
/// </summary>
internal sealed class PuppetHeld
{
    // A held thing further than this from the hand isn't in it (a stale name, an item that lags far behind).
    private const float MaxGap = 0.9f;

    private readonly byte _slot;
    private NetRef _leftRef, _rightRef;
    private NetObject _left, _right;

    public PuppetHeld(byte slot) => _slot = slot;

    /// <summary>Where the item in a hand is in this game, when the pose names one that's really held by this body's player.</summary>
    public bool TryGet(in PlayerPose pose, bool right, Vector3 hand, out Vector3 position)
    {
        position = default;
        byte flag = right ? PlayerPose.RightHolds : PlayerPose.LeftHolds;
        if ((pose.HandsShown & flag) == 0)
            return false;
        var wanted = right ? pose.HeldRight : pose.HeldLeft;
        ref var cached = ref (right ? ref _rightRef : ref _leftRef);
        ref var item = ref (right ? ref _right : ref _left);
        if (item == null || !item.Alive || !cached.Equals(wanted))
        {
            item = NetObjects.Find(wanted);
            cached = wanted;
        }
        if (item == null || !item.Alive || item.Holder != _slot)
            return false;
        position = item.Tf != null ? item.Tf.position : item.Go.transform.position;
        return (position - hand).sqrMagnitude <= MaxGap * MaxGap;
    }
}
