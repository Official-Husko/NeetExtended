using System.Collections.Generic;
using Coop.World.Objects;
using UnityEngine;

namespace Coop.World.Ownership;

/// <summary>
/// Client: a rat in this game's player's hand. Its brain only runs on the host (here it's a puppet),
/// and the host doesn't see anyone holding it, so this game does BrainRat.Update's part for its
/// holder: the rat wriggles, and bites every 3-5 s for 4-7.
/// </summary>
internal static class HeldAnimals
{
    private struct Held
    {
        public NetObject Object;
        public float NextBite;
    }

    private static readonly List<Held> Rats = new List<Held>();

    /// <summary>Client, when this game's player takes a holdable animal.</summary>
    public static void Taken(NetObject o)
    {
        if (!WorldSide.IsClient || !(o.Brain is BrainRat rat))
            return;
        for (int i = 0; i < Rats.Count; i++)
        {
            if (Rats[i].Object == o)
                return;
        }
        // The rat's own countdown carries over (it starts at 0: a fresh rat bites at once).
        Rats.Add(new Held { Object = o, NextBite = Time.time + Mathf.Max(0f, rat.heldBitetimer) });
    }

    public static void Clear() => Rats.Clear();

    /// <summary>Client, every frame (nothing to do unless this game's player holds a rat).</summary>
    public static void Tick()
    {
        if (Rats.Count == 0)
            return;
        var player = WorldSide.LocalPlayer;
        float now = Time.time;
        float step = Time.deltaTime;
        for (int i = Rats.Count - 1; i >= 0; i--)
        {
            var held = Rats[i];
            var o = held.Object;
            var usable = o.Usable;
            if (!o.Alive || usable == null || player == null || usable.player != player)
            {
                Rats.RemoveAt(i);
                continue;
            }
            var body = o.Body;
            if (body != null && !body.isKinematic)
            {
                body.AddForce((Random.insideUnitSphere + Vector3.one * 0.8f) * 40f * step, ForceMode.Impulse);
                body.AddTorque(Random.insideUnitSphere * 50f);
            }
            if (now < held.NextBite)
                continue;
            if (player.TryGetComponent(out PlayerStats stats) && stats.health != null)
                stats.health.Affect(o.Tf.position, -Random.Range(4f, 7f), EffectType.Cut, Vector3.down * 2f);
            held.NextBite = now + Random.Range(3, 6);
            Rats[i] = held;
        }
    }
}
