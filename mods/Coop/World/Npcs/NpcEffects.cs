using System.Collections.Generic;
using Coop.Net;
using Coop.World.Objects;
using LiteNetLib.Utils;
using UnityEngine;

namespace Coop.World.Npcs;

/// <summary>
/// What an NPC's death throws up when it is skeletonized (a wind of blood, the skeleton flushing red as
/// it forms) or obliterated (a burst of blood, a short groan). The host's game makes these in the dying
/// NPC's own script. The NPC itself, the gibs, the skeleton and the blood on the walls are shared
/// already, but a client's NPC just disappears, so the host says where and in which blood and the
/// client does the same there.
///
/// The red flush belongs to the skeleton, which spawns a moment later as a shared NPC of its own: the
/// client remembers the wind it was told about and, when a skeleton appears at that spot, fades its
/// colour from red back to normal over the same four seconds the game does, counted from the skeleton.
///
/// WorldNpcEffect: kind (1 wind, 2 burst), blood colour, rig ref, position, rotation.
/// </summary>
internal static class NpcEffects
{
    public const byte Wind = 1;
    public const byte Burst = 2;

    private const float WindLife = 4f;
    private const float BurstLife = 6f;
    private const float FlushTime = 4f;
    private const float FlushReach = 1.5f;
    private const int GroanSound = 11;
    private const int VoiceSource = 2;
    private static readonly Quaternion Flip = Quaternion.Euler(0f, 180f, 0f);
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private struct Wait
    {
        public Vector3 Position;
        public float Time;
    }

    private struct Flush
    {
        public Material Skin;
        public Color Normal;
        public float End;
    }

    private static readonly List<Wait> Waiting = new List<Wait>(4);
    private static readonly List<Flush> Flushing = new List<Flush>(4);

    public static void Init() => Messages.On(MessageIds.WorldNpcEffect, OnEffect);

    public static void Clear()
    {
        Waiting.Clear();
        for (int i = 0; i < Flushing.Count; i++)
            Finish(Flushing[i]);
        Flushing.Clear();
    }

    /// <summary>Host: an NPC rig is dying that way. Where the particles go is where the game puts them.</summary>
    public static void Send(byte kind, NetObject o, Player_Abilities abilities)
    {
        var tf = abilities.transform;
        var w = Outbox.Begin(MessageIds.WorldNpcEffect);
        w.Put(kind);
        w.Put((byte)abilities.bloodColor);
        o.Ref.Write(w);
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
        var npc = NetObjects.Find(NetRef.Read(reader));
        var position = new Vector3(reader.GetFloat(), reader.GetFloat(), reader.GetFloat());
        var rotation = new Quaternion(reader.GetFloat(), reader.GetFloat(), reader.GetFloat(), reader.GetFloat());
        var prefab = Resources.Load<GameObject>(kind == Wind ? "Particle_BloodWind" : "Particle_BloodBurst");
        if (prefab != null)
        {
            var particles = Object.Instantiate(prefab, position, rotation);
            if (particles.TryGetComponent(out ParticleSystem system) && Water.liquidColors != null && blood < Water.liquidColors.Length)
            {
                var main = system.main;
                main.startColor = Water.liquidColors[blood];
            }
            Object.Destroy(particles, kind == Wind ? WindLife : BurstLife);
        }
        if (kind == Wind)
            Waiting.Add(new Wait { Position = position, Time = Time.time });
        else
            Groan(npc);
    }

    // The obliterate groan is a one-shot on the NPC's voice source, which the NPC's destruction cuts
    // short: the same happens to the puppet, when the host's destroy arrives.
    private static void Groan(NetObject npc)
    {
        var sounds = npc?.Rig?.Abilities?.needSounds;
        if (sounds != null && sounds.Length > GroanSound)
            NpcVoices.PlayOneShot(npc, VoiceSource, sounds[GroanSound]);
    }

    /// <summary>Client: a new NPC appeared; a skeleton forming where a wind blew starts flushing red.</summary>
    public static void Appeared(NetObject o)
    {
        if (Waiting.Count == 0 || o.Go == null || !o.Go.name.Contains("Skeleton"))
            return;
        float now = Time.time;
        for (int i = Waiting.Count - 1; i >= 0; i--)
        {
            var wait = Waiting[i];
            if (now - wait.Time > FlushTime)
            {
                Waiting.RemoveAt(i);
                continue;
            }
            if ((o.Go.transform.position - wait.Position).sqrMagnitude > FlushReach * FlushReach)
                continue;
            Waiting.RemoveAt(i);
            var renderer = o.Go.GetComponentInChildren<SkinnedMeshRenderer>();
            if (renderer != null)
            {
                var skin = renderer.material;
                Flushing.Add(new Flush { Skin = skin, Normal = skin.GetColor(ColorId), End = now + FlushTime });
            }
            return;
        }
    }

    /// <summary>Client, every frame while a skeleton is flushing: the game's red-to-normal fade.</summary>
    public static void LateTick()
    {
        float now = Time.time;
        for (int i = Flushing.Count - 1; i >= 0; i--)
        {
            var flush = Flushing[i];
            float left = flush.End - now;
            if (flush.Skin == null || left <= 0f)
            {
                Finish(flush);
                Flushing.RemoveAt(i);
                continue;
            }
            var n = flush.Normal;
            flush.Skin.SetColor(ColorId, new Color(n.r, n.g - left, n.b - left, 1f));
        }
    }

    private static void Finish(Flush flush)
    {
        if (flush.Skin != null)
            flush.Skin.SetColor(ColorId, flush.Normal);
    }
}
