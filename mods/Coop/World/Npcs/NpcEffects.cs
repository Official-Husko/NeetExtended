using Coop.Net;
using LiteNetLib.Utils;
using UnityEngine;

namespace Coop.World.Npcs;

/// <summary>
/// The blood an NPC's death throws up when it is skeletonized (a wind of blood) or obliterated (a
/// burst). The host's game makes the particles; the NPC itself, the gibs, the skeleton and the blood
/// on the walls are shared already, but a client's NPC just disappears, so the host says where and
/// in which blood and the client plays the same particles there.
///
/// WorldNpcEffect: kind (1 wind, 2 burst), blood colour, position, rotation.
/// </summary>
internal static class NpcEffects
{
    public const byte Wind = 1;
    public const byte Burst = 2;

    private const float WindLife = 4f;
    private const float BurstLife = 6f;
    private static readonly Quaternion Flip = Quaternion.Euler(0f, 180f, 0f);

    public static void Init() => Messages.On(MessageIds.WorldNpcEffect, OnEffect);

    /// <summary>Host: an NPC rig is dying that way. Where the particles go is where the game puts them.</summary>
    public static void Send(byte kind, Player_Abilities abilities)
    {
        var tf = abilities.transform;
        var w = Outbox.Begin(MessageIds.WorldNpcEffect);
        w.Put(kind);
        w.Put((byte)abilities.bloodColor);
        Vector3 position = tf.position - tf.forward * 0.1f;
        Quaternion rotation = tf.rotation * Flip;
        w.Put(position.x);
        w.Put(position.y);
        w.Put(position.z);
        w.Put(rotation.x);
        w.Put(rotation.y);
        w.Put(rotation.z);
        w.Put(rotation.w);
        Outbox.ToPlayers(w, Channel.Events);
    }

    private static void OnEffect(in Sender from, NetDataReader reader)
    {
        if (WorldSide.IsHost || !WorldSide.Running)
            return;
        byte kind = reader.GetByte();
        int blood = reader.GetByte();
        var position = new Vector3(reader.GetFloat(), reader.GetFloat(), reader.GetFloat());
        var rotation = new Quaternion(reader.GetFloat(), reader.GetFloat(), reader.GetFloat(), reader.GetFloat());
        var prefab = Resources.Load<GameObject>(kind == Wind ? "Particle_BloodWind" : "Particle_BloodBurst");
        if (prefab == null)
            return;
        var particles = Object.Instantiate(prefab, position, rotation);
        if (particles.TryGetComponent(out ParticleSystem system) && Water.liquidColors != null && blood < Water.liquidColors.Length)
        {
            var main = system.main;
            main.startColor = Water.liquidColors[blood];
        }
        Object.Destroy(particles, kind == Wind ? WindLife : BurstLife);
    }
}
