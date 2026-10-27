using System.Collections.Generic;
using Coop.Net;
using LiteNetLib.Utils;
using UnityEngine;

namespace Coop.World.Ambience;

/// <summary>
/// Toilet handles. A handle is a lever the player grabs and pulls down (Toilet_Flush); the flush
/// itself (what it takes away) is already shared, but the handle only moved for the one pulling it.
/// The game where it's being pulled sends its angle 15 times a second while it's held and while it
/// springs back. The others move their handle to match, kinematic, with the game's own flush check
/// switched off for it (so nothing is flushed twice), and play the flush sound when it flushes.
///
/// WorldToilet: toilet index, the handle's local x angle, flushing.
/// </summary>
internal static class ToiletSync
{
    private const float SendEvery = 1f / 15f;
    private const float SettleTime = 2f;
    private const float RemoteFor = 0.5f;

    private sealed class Toilet
    {
        public Toilet_Flush Flush;
        public Usable Handle;
        public Rigidbody Body;
        public AudioSource Sound;
        public float SendUntil;
        public float NextSend;
        public float RemoteUntil = -1f;
        public bool RemoteFlushing;
        public bool WasKinematic;
    }

    private static readonly List<Toilet> Toilets = new List<Toilet>();
    private static UnityEngine.SceneManagement.Scene _scannedFor;

    public static void Init() => Messages.On(MessageIds.WorldToilet, OnHandle);

    public static void Clear()
    {
        foreach (var toilet in Toilets)
            Release(toilet);
        Toilets.Clear();
        _scannedFor = default;
    }

    /// <summary>For the Toilet_Flush patch: another game is pulling this handle, so its flush check doesn't run here.</summary>
    public static bool IsRemote(Toilet_Flush flush)
    {
        for (int i = 0; i < Toilets.Count; i++)
        {
            if (Toilets[i].Flush == flush)
                return Toilets[i].RemoteUntil > Time.time;
        }
        return false;
    }

    public static void LateTick()
    {
        if (!Scan())
            return;
        float now = Time.time;
        for (int i = 0; i < Toilets.Count; i++)
        {
            var toilet = Toilets[i];
            if (toilet.Flush == null)
                continue;
            if (toilet.RemoteUntil > 0f && toilet.RemoteUntil <= now)
                Release(toilet);
            if (toilet.RemoteUntil > now)
                continue;
            if (toilet.Handle != null && toilet.Handle.player != null)
                toilet.SendUntil = now + SettleTime;
            if (now < toilet.SendUntil && now >= toilet.NextSend)
            {
                toilet.NextSend = now + SendEvery;
                Send(i, toilet);
            }
        }
    }

    private static void Send(int index, Toilet toilet)
    {
        var w = Outbox.Begin(MessageIds.WorldToilet);
        w.Put((byte)index);
        w.Put(toilet.Flush.flush.transform.localEulerAngles.x);
        w.Put(toilet.Flush.flushing);
        if (WorldSide.IsHost)
            Outbox.ToPlayers(w, Channel.Stream);
        else
            Outbox.ToHost(w, Channel.Stream);
    }

    private static void OnHandle(in Sender from, NetDataReader reader)
    {
        if (!WorldSide.Running)
            return;
        int start = reader.Position;
        int length = reader.AvailableBytes;
        int index = reader.GetByte();
        float angle = reader.GetFloat();
        bool flushing = reader.GetBool();
        if (WorldSide.IsHost)
        {
            // Pass a client's handle on to the other clients.
            var relay = Outbox.Begin(MessageIds.WorldToilet);
            relay.Put(reader.RawData, start, length);
            Outbox.ToPlayers(relay, Channel.Stream, from.Slot);
        }
        if (!Scan() || index >= Toilets.Count || Toilets[index].Flush == null)
            return;
        var toilet = Toilets[index];
        if (toilet.Handle != null && toilet.Handle.player != null)
            return; // Pulled here too: this game's pull wins here.
        if (toilet.RemoteUntil < 0f)
        {
            toilet.WasKinematic = toilet.Body != null && toilet.Body.isKinematic;
            toilet.RemoteFlushing = toilet.Flush.flushing;
        }
        toilet.RemoteUntil = Time.time + RemoteFor;
        if (toilet.Body != null)
            toilet.Body.isKinematic = true;
        var handle = toilet.Flush.flush.transform;
        var euler = handle.localEulerAngles;
        handle.localRotation = Quaternion.Euler(angle, euler.y, euler.z);
        if (flushing && !toilet.RemoteFlushing && toilet.Sound != null)
            toilet.Sound.Play();
        toilet.RemoteFlushing = flushing;
        toilet.Flush.flushing = flushing;
    }

    // Back to this game's own physics once the other game stopped sending.
    private static void Release(Toilet toilet)
    {
        if (toilet.RemoteUntil < 0f)
            return;
        toilet.RemoteUntil = -1f;
        if (toilet.Body != null)
            toilet.Body.isKinematic = toilet.WasKinematic;
    }

    // The toilets of this scene, in a stable order (by their place in the hierarchy) on every machine.
    private static bool Scan()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (_scannedFor == scene)
            return Toilets.Count > 0;
        _scannedFor = scene;
        Toilets.Clear();
        var found = new List<Toilet_Flush>(Object.FindObjectsOfType<Toilet_Flush>());
        found.Sort((a, b) => string.CompareOrdinal(PathOf(a.transform), PathOf(b.transform)));
        foreach (var flush in found)
        {
            if (flush.flush == null)
                continue;
            Toilets.Add(new Toilet
            {
                Flush = flush,
                Handle = flush.flush.GetComponent<Usable>(),
                Body = flush.flush.GetComponent<Rigidbody>(),
                Sound = flush.flush.GetComponent<AudioSource>(),
            });
        }
        return Toilets.Count > 0;
    }

    private static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
}
