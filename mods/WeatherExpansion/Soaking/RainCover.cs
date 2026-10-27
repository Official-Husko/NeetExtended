using UnityEngine;

namespace WeatherExpansion.Soaking;

/// <summary>
/// Whether something is over the player's head: a porch roof, a tree, an awning. A short ray straight up
/// from the head against the world's solid colliders. Triggers, the player's own body and loose things
/// (items, other people, anything with a moving rigidbody) don't count as cover.
/// </summary>
internal static class RainCover
{
    private const float Reach = 12f;
    private static readonly RaycastHit[] Hits = new RaycastHit[8];

    public static bool IsCovered(Transform player, float headHeight)
    {
        var from = player.position + Vector3.up * headHeight;
        int count = Physics.RaycastNonAlloc(from, Vector3.up, Hits, Reach, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            var collider = Hits[i].collider;
            if (collider == null || collider.transform.IsChildOf(player))
                continue;
            var body = collider.attachedRigidbody;
            if (body != null && !body.isKinematic)
                continue;
            return true;
        }
        return false;
    }
}
