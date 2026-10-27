using Coop.World.Objects;
using UnityEngine;

namespace Coop.World.Ownership;

/// <summary>
/// A replica is kinematic, so walking into one would be walking into a wall. Each physics step this
/// looks around the local player's capsule, and around what the player holds or (on a client) threw;
/// a replica they move into is taken over on the spot and becomes pushable one step later. Touching
/// also stops a body from being handed back while leaned on.
/// </summary>
internal static class ContactClaims
{
    private const float Skin = 0.03f;
    private const float MinSpeedSq = 0.2f * 0.2f;
    private const int ClaimsPerSecond = 10;
    private const float ThrownSpeedSq = 1f;

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

        float now = Time.time;
        Touch(count, null, _body.velocity.sqrMagnitude > MinSpeedSq, now);
        CarriedTouches(now);
    }

    // What this game's player holds pushes too, and so does what a client threw (still flying).
    private static void CarriedTouches(float now)
    {
        byte me = WorldSide.LocalSlot;
        bool host = WorldSide.IsHost;
        var moving = NetObjects.Moving;
        for (int i = 0; i < moving.Count; i++)
        {
            var o = moving[i];
            if (o.IsReplica || o.Owner != me || o.Body == null || o.Body.isKinematic || !o.Alive)
                continue;
            bool held = o.Holder == me;
            if (!held && (host || o.Body.velocity.sqrMagnitude < ThrownSpeedSq))
                continue;
            var collider = o.Collider != null ? o.Collider : o.Collider = o.Go.GetComponent<Collider>();
            if (collider == null)
                continue;
            var bounds = collider.bounds;
            int count = Physics.OverlapBoxNonAlloc(bounds.center, bounds.extents + Vector3.one * Skin, Hits, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            Touch(count, o, true, now);
        }
    }

    private static void Touch(int count, NetObject toucher, bool moving, float now)
    {
        for (int i = 0; i < count; i++)
        {
            var o = NetObjects.FindBody(Hits[i].attachedRigidbody);
            Hits[i] = null;
            if (o == null || o == toucher || o.Body == _body)
                continue;
            o.LastTouched = now;
            if (moving && Claimable(o) && UnderLimit(now))
                Owners.LocalTouch(o);
        }
    }

    private static bool Claimable(NetObject o) =>
        o.IsReplica
        && !o.OnPuppet
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
