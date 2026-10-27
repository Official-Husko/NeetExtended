using System.Collections.Generic;
using Coop.Net;
using Coop.World.Objects;
using LiteNetLib.Utils;
using UnityEngine;

namespace Coop.World.Npcs;

/// <summary>
/// The sounds an NPC rig makes: the groan of dying, hurt and need sounds, punches and kicks landing,
/// clothes rustling, farts, footsteps. The rig's own scripts play them on its audio sources, and those
/// scripts only run on the host, so a client's NPC would be silent. The host watches each rig's sources
/// and sends a sound when one starts (or switches clip); the client plays the same clip on the same
/// source of its puppet, a moment later so it lands with the pose being shown.
///
/// A source is watched, not hooked: the sounds come from a dozen places in the game's scripts, and
/// looking at a few flags a frame is cheaper than patching the engine's Play. A sound started with
/// PlayOneShot has no clip to read, so those few (the obliterate groan) are sent by name elsewhere.
///
/// WorldNpcSound: sender time, rig ref, source index, clip hash, pitch times 100.
/// </summary>
internal static class NpcVoices
{
    private const float MaxDelay = 0.5f;
    private const float RescanEvery = 10f;

    private static readonly Dictionary<int, AudioClip> Clips = new Dictionary<int, AudioClip>();
    private static float _scanned = -RescanEvery;

    /// <summary>The last sound played here for a host NPC, for tests.</summary>
    public static string Last = "";

    public static void Init() => Messages.On(MessageIds.WorldNpcSound, OnSound);

    public static void Clear()
    {
        Clips.Clear();
        _scanned = -RescanEvery;
    }

    /// <summary>Host, every frame: sources of live rigs that started a sound since the last look.</summary>
    public static void LateTick()
    {
        var rigs = NetObjects.Rigs;
        for (int i = 0; i < rigs.Count; i++)
        {
            var o = rigs[i];
            var rig = o.Rig;
            if (rig == null || !o.Alive || !o.PosedByHost || o.Go == null || !o.Go.activeInHierarchy)
                continue;
            bool known = rig.VoicesKnown;
            rig.VoicesKnown = true;
            for (int j = 0; j < rig.Voices.Length; j++)
            {
                var source = rig.Voices[j];
                if (source == null)
                    continue;
                bool on = source.isPlaying;
                var clip = source.clip;
                // A new sound is confirmed a frame later: PlayOneShot also makes the source "play" and
                // leaves its old clip in place, but only a real Play moves the source's time on.
                if (rig.VoiceNew[j])
                {
                    rig.VoiceNew[j] = false;
                    if (on && clip == rig.VoiceClip[j] && source.time > 0f)
                        Send(o, j, source);
                }
                else if (known && on && !source.loop && clip != null && (!rig.VoiceOn[j] || clip != rig.VoiceClip[j]))
                    rig.VoiceNew[j] = true;
                rig.VoiceOn[j] = on;
                rig.VoiceClip[j] = clip;
            }
        }
    }

    private static void Send(NetObject o, int index, AudioSource source)
    {
        var w = Outbox.Begin(MessageIds.WorldNpcSound);
        w.Put(Time.time);
        o.Ref.Write(w);
        w.Put((byte)index);
        w.Put(Hash(source.clip.name));
        w.Put((byte)Mathf.Clamp(Mathf.RoundToInt(source.pitch * 100f), 0, 255));
        Outbox.ToPlayers(w, Channel.Events);
    }

    private static void OnSound(in Sender from, NetDataReader reader)
    {
        if (WorldSide.IsHost || !WorldSide.Running)
            return;
        float time = reader.GetFloat();
        var o = NetObjects.Find(NetRef.Read(reader));
        int index = reader.GetByte();
        int hash = reader.GetInt();
        float pitch = reader.GetByte() / 100f;
        RemoteClock.Observe(WorldSide.HostSlot, time);
        var voices = o?.Rig?.Voices;
        if (voices == null || index >= voices.Length || voices[index] == null || !o.Go.activeInHierarchy)
            return;
        var clip = Resolve(hash);
        if (clip == null)
            return;
        var source = voices[index];
        source.clip = clip;
        source.pitch = pitch;
        source.PlayDelayed(Mathf.Clamp(time - RemoteClock.PlaybackTime(WorldSide.HostSlot), 0f, MaxDelay));
        Last = $"{o.Go.name}: {clip.name}";
    }

    /// <summary>Client: a one-shot sound the host's NPC made on one of its sources (not watchable).</summary>
    public static void PlayOneShot(NetObject o, int index, AudioClip clip)
    {
        var voices = o?.Rig?.Voices;
        if (voices == null || index >= voices.Length || voices[index] == null || clip == null || !o.Go.activeInHierarchy)
            return;
        voices[index].PlayOneShot(clip);
        Last = $"{o.Go.name}: {clip.name} (one shot)";
    }

    // The clips all rigs share are loaded already (the puppets reference them), so one look at what's
    // in memory finds a clip by its name's hash; it looks again, at most every few seconds, for a miss.
    private static AudioClip Resolve(int hash)
    {
        if (Clips.TryGetValue(hash, out var clip) && clip != null)
            return clip;
        if (Time.time - _scanned < RescanEvery)
            return null;
        _scanned = Time.time;
        foreach (var candidate in Resources.FindObjectsOfTypeAll<AudioClip>())
            Clips[Hash(candidate.name)] = candidate;
        return Clips.TryGetValue(hash, out clip) ? clip : null;
    }

    // FNV-1a: the same number in every game, unlike string.GetHashCode.
    private static int Hash(string text)
    {
        unchecked
        {
            uint h = 2166136261;
            foreach (char c in text)
                h = (h ^ c) * 16777619;
            return (int)h;
        }
    }
}
