using Coop.World.Objects;
using UnityEngine;

namespace Coop.World.Ownership;

/// <summary>
/// A replica is kinematic, so walking into one would be walking into a wall. Each physics step this
/// looks around the local player's capsule; a replica it's moving into is taken over on the spot and
/// becomes pushable one step later. Touching also stops a body from being handed back while leaned on.
/// </summary>
internal static class ContactClaims
{
    private const float Skin = 0.03f;
    private const float MinSpeedSq = 0.2f * 0.2f;
    private const int ClaimsPerSecond = 10;

    private static readonly Collider[] Hits = new Collider[32];
    private static GameObject _player;
    private static CapsuleCollider _capsule;
    private static Rigidbody _body;
    private static float _windowStart;
    private static int _claims;

    public static void Reset()
    {
        _player = null;
        _capsule = null;
        _body = null;
    }

    public static void FixedTick()
    {
        var player = WorldSide.LocalPlayer;
        if (player == null)
            return;
        if (player != _player)
        {
            _player = player;
            _capsule = player.GetComponent<CapsuleCollider>();
            _body = player.GetComponent<Rigidbody>();
        }
        if (_capsule == null || _body == null)
            return;

        var t = _capsule.transform;
        var scale = t.lossyScale;
        float radius = _capsule.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z)) + Skin;
        float half = Mathf.Max(0f, _capsule.height * 0.5f * Mathf.Abs(scale.y) - radius);
        var center = t.TransformPoint(_capsule.center);
        var up = t.up * half;
        int count = Physics.OverlapCapsuleNonAlloc(center + up, center - up, radius, Hits, ~0, QueryTriggerInteraction.Ignore);

        bool moving = _body.velocity.sqrMagnitude > MinSpeedSq;
        float now = Time.time;
        for (int i = 0; i < count; i++)
        {
            var o = NetObjects.FindBody(Hits[i].attachedRigidbody);
            Hits[i] = null;
            if (o == null || o.Body == _body)
                continue;
            o.LastTouched = now;
            if (moving && Claimable(o) && UnderLimit(now))
                Owners.LocalTouch(o);
        }
    }

    private static bool Claimable(NetObject o) =>
        o.IsReplica
        && o.Holder == WorldSide.None
        && !o.GameKinematic
        && o.Brain == null
        && o.Rig == null
        && o.Usable != null
        && o.Usable.player == null
        && !(o.Door != null && o.Door.Locked);

    private static bool UnderLimit(float now)
    {
        if (now - _windowStart > 1f)
        {
            _windowStart = now;
            _claims = 0;
        }
        return _claims++ < ClaimsPerSecond;
    }
}
