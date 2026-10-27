using UnityEngine;

namespace Coop.World.Breakables;

/// <summary>
/// What a break looks like in a game that didn't decide it. The deciding game's BreakOnImpact has
/// already run (and its shards, gibs and bones reach the others as ordinary spawns), so what's left
/// is the part that spawns nothing shared: the thud, the break sound, the dust and shard particles
/// that aren't items, and the pane going away. Done from the component's own settings, so every
/// breakable sounds and looks like the game's.
/// </summary>
internal static class BreakApply
{
    /// <summary>A hit that didn't break it: the thud, and the hit count the deciding game has.</summary>
    public static void Hit(BreakOnImpact b, int count)
    {
        b.breakCollisionAmounts = count;
        PlayAny(b.collisionSounds, b.transform.position);
    }

    /// <summary>The break itself, with its sound and effects; then the pane and what goes with it are gone.</summary>
    public static void Break(BreakOnImpact b)
    {
        if (b.broken)
            return;
        Effects(b);
        Remove(b);
    }

    /// <summary>The effects only, for an item that broke where it was simulated (the item itself goes with its own destroy).</summary>
    public static void Effects(BreakOnImpact b)
    {
        PlayAny(b.breakSounds, b.transform.position);
        var effects = b.breakEffectSpawn;
        if (effects == null || effects.Length == 0)
            return;
        var t = b.spawnAtParent && b.transform.parent != null ? b.transform.parent : b.transform;
        int count = b.amtToSpawn;
        for (int i = 0; i < count; i++)
        {
            var prefab = effects[i % effects.Length];
            // Pieces that are items (shards, gibs, bones) arrive from the game that spawned them.
            if (prefab == null || prefab.GetComponentInChildren<Usable>(true) != null)
                continue;
            var at = t.position + Random.insideUnitSphere * b.randomSphereOffset
                + t.right * b.spawnOffset.x + t.up * b.spawnOffset.y + t.forward * b.spawnOffset.z;
            var rotation = b.spawnRotOffset == Vector3.zero ? Random.rotation : t.rotation.RotAxisEuler(t, b.spawnRotOffset);
            Object.Instantiate(prefab, at, rotation);
        }
    }

    /// <summary>Joining: what the host already lost, without the noise.</summary>
    public static void Quiet(BreakOnImpact b, int count, bool broken)
    {
        b.breakCollisionAmounts = count;
        if (broken && !b.broken)
            Remove(b);
    }

    // The tail of BreakOnImpact.Impact: the panes and the object itself go (or are only switched off).
    private static void Remove(BreakOnImpact b)
    {
        b.broken = true;
        var also = b.objToAlsoDestroy;
        if (also != null)
        {
            for (int i = 0; i < also.Length; i++)
                Gone(b, also[i]);
        }
        if (!b.dontDestroySelf)
            Gone(b, b.gameObject);
    }

    private static void Gone(BreakOnImpact b, GameObject go)
    {
        if (go == null)
            return;
        if (b.disableMode)
            go.SetActive(false);
        else
            Object.Destroy(go);
    }

    private static void PlayAny(AudioClip[] clips, Vector3 at)
    {
        if (clips == null || clips.Length == 0)
            return;
        var clip = clips[Random.Range(0, clips.Length)];
        if (clip != null)
            Commons.PlayClipAt(clip, at);
    }
}
