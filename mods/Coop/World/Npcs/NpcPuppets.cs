using System.Collections.Generic;
using Coop.World.Objects;
using UnityEngine;
using UnityEngine.AI;

namespace Coop.World.Npcs;

/// <summary>
/// Client: NPCs only think on the host. Here every brain is switched off and its body becomes a
/// puppet the host's stream moves: no brain, no walking or hand logic, no needs, no damage, kinematic
/// bodies. Everything is plain component state, so doing it again (after a hot reload) is harmless.
/// The parts that need the components' own Start to have run wait a frame.
/// </summary>
internal static class NpcPuppets
{
    private static readonly List<NetObject> Later = new List<NetObject>();
    private static readonly Dictionary<Stat, Stat.EffectManager> Damage = new Dictionary<Stat, Stat.EffectManager>();
    private static readonly Stat.EffectManager NoDamage = (pos, initVal, potency, type, force, source) => 0f;

    public static void Puppetize(NetObject o)
    {
        var brain = o.Brain;
        if (brain == null)
            return;
        brain.enabled = false;
        brain.StopAllCoroutines();
        o.IsReplica = true;
        o.Owner = WorldSide.HostSlot;
        if (brain.TryGetComponent(out NavMeshAgent nav))
            nav.enabled = false;
        if (o.Body != null)
        {
            o.Body.isKinematic = true;
            o.Body.useGravity = false;
            o.Body.interpolation = RigidbodyInterpolation.Interpolate;
        }
        if (!Later.Contains(o))
            Later.Add(o);
    }

    /// <summary>A freshly made NPC must not dress itself in new clothes here: the host's clothes come as items.</summary>
    public static void BeforeStart(GameObject go)
    {
        if (!go.TryGetComponent(out Player_Abilities abilities) || abilities.clothings == null)
            return;
        for (int i = 0; i < abilities.clothings.Length; i++)
        {
            var piece = abilities.clothings[i];
            if (piece != null && !piece.scene.IsValid())
                abilities.clothings[i] = null;
        }
    }

    /// <summary>The rest of the recipe, once every component of the new NPCs has started.</summary>
    public static void ProcessLater()
    {
        for (int i = 0; i < Later.Count; i++)
        {
            var o = Later[i];
            if (o.Alive && o.Index >= 0)
                Finish(o);
        }
        Later.Clear();
    }

    private static void Finish(NetObject o)
    {
        var go = o.Go;
        if (go.TryGetComponent(out Player_Movement movement))
            movement.enabled = false;
        if (go.TryGetComponent(out Player_Abilities abilities))
        {
            abilities.enabled = false;
            // The hands and fingers are physics bodies the abilities drive; without them they'd flop.
            foreach (var body in go.GetComponentsInChildren<Rigidbody>(true))
                body.isKinematic = true;
        }
        if (go.TryGetComponent(out PlayerStats stats))
        {
            // Only once Awake has run: PlayerStats zeroes health if it starts out disabled.
            stats.pausedNeeds = true;
            stats.invincible = true;
            stats.enabled = false;
        }
        if (go.TryGetComponent(out StatusHandler status))
        {
            status.enabled = false;
            status.StopAllCoroutines();
        }
        if (go.TryGetComponent(out CatStats cat))
            cat.enabled = false;
        if (go.TryGetComponent(out ActorStats actor))
            BlockDamage(actor);
        o.Brain.enabled = false;
    }

    // Knives, traps, fire and punches here must not kill the host's NPC on this screen only.
    private static void BlockDamage(ActorStats actor)
    {
        if (actor.stats == null || !actor.stats.TryGetValue(typeof(Health), out var health) || health == null)
            return;
        if (health.manageEffect == NoDamage)
            return;
        Damage[health] = health.manageEffect;
        health.manageEffect = NoDamage;
    }

    /// <summary>Puts the NPCs' own damage handling back (the mod unloads; the next copy blocks it again).</summary>
    public static void RestoreDamage()
    {
        foreach (var pair in Damage)
        {
            if (pair.Key != null && pair.Key.manageEffect == NoDamage)
                pair.Key.manageEffect = pair.Value;
        }
        Damage.Clear();
        Later.Clear();
    }

    /// <summary>The scene went away with its NPCs.</summary>
    public static void Forget()
    {
        Damage.Clear();
        Later.Clear();
    }
}
