using System.Collections.Generic;
using Coop.Net;
using Coop.Session;
using Coop.World.Objects;
using LiteNetLib.Utils;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Coop.World.Ambience;

/// <summary>
/// Radios play at the host's pace. Which tape is in a radio, its battery and its music switch are
/// shared state already (RadioState); this keeps the sound in step: the host sends what each radio
/// plays (nothing, its tape, or another song, like the Mixtapes station's, by name) and where it is
/// in it, once a second and as soon as that changes. A client plays the same and jumps to the
/// host's time when it's more than 0.15 s off. On a client the Mixtapes station leaves the picking to
/// the host.
///
/// WorldRadio (Stream, host to clients): count, then per radio: item ref, kind (<see cref="Silent"/>,
/// <see cref="Tape"/>, <see cref="Song"/>), and for a song its clip name and length; the time.
/// </summary>
internal static class RadioPlayback
{
    private const byte Silent = 0;
    private const byte Tape = 1;
    private const byte Song = 2;

    private const float SendEvery = 1f;
    private const float Tolerance = 0.15f;
    private const float Settle = 1.5f;
    private const float Stale = 4f;
    private const float SearchAgain = 2f;

    private sealed class Heard
    {
        public NetRef Item;
        public byte Kind;
        public string Clip;
        public float Length;
        public float Time;
        public float At;
        public AudioClip Found;
        public float SearchAt;
        public float SettleUntil;
    }

    private static readonly List<Radio> Radios = new List<Radio>();
    private static readonly Dictionary<Radio, AudioClip> SentClips = new Dictionary<Radio, AudioClip>();
    private static readonly Dictionary<long, Heard> Reports = new Dictionary<long, Heard>();
    private static readonly List<long> Gone = new List<long>();
    private static float _next;

    public static void Init() => Messages.On(MessageIds.WorldRadio, OnRadio);

    /// <summary>A radio started (Radio.Start).</summary>
    public static void Track(Radio radio)
    {
        if (radio != null && !Radios.Contains(radio))
            Radios.Add(radio);
    }

    /// <summary>World sync started (or the mod reloaded): the radios already in the house.</summary>
    public static void Rescan()
    {
        Radios.Clear();
        Radios.AddRange(Object.FindObjectsOfType<Radio>());
    }

    public static void Clear()
    {
        Radios.Clear();
        SentClips.Clear();
        Reports.Clear();
    }

    public static void LateTick(bool host)
    {
        for (int i = Radios.Count - 1; i >= 0; i--)
        {
            if (Radios[i] == null)
                Radios.RemoveAt(i);
        }
        if (host)
            Send();
        else
            Follow();
    }

    /// <summary>For the bridge: what each radio here plays.</summary>
    public static string Describe()
    {
        var text = new System.Text.StringBuilder();
        foreach (var radio in Radios)
        {
            var speaker = radio.musicAudio;
            var o = NetObjects.Find(ItemOf(radio));
            text.Append(o != null ? o.Ref.ToString() : radio.name).Append(": ")
                .Append(speaker != null && speaker.isPlaying ? $"{speaker.clip?.name} at {speaker.time:0.00} of {speaker.clip?.length:0.00} s" : "silent")
                .Append(radio.cassette != null ? ", tape in" : "").Append('\n');
        }
        return text.Length > 0 ? text.ToString() : "No radios.";
    }

    private static void Send()
    {
        bool changed = false;
        foreach (var radio in Radios)
        {
            var playing = Playing(radio);
            if (!SentClips.TryGetValue(radio, out var sent) || sent != playing)
            {
                SentClips[radio] = playing;
                changed = true;
            }
        }
        if (!changed && Time.unscaledTime < _next)
            return;
        _next = Time.unscaledTime + SendEvery;

        var w = Outbox.Begin(MessageIds.WorldRadio);
        int countAt = w.Length;
        w.Put((byte)0);
        byte count = 0;
        foreach (var radio in Radios)
        {
            var o = NetObjects.Find(ItemOf(radio));
            if (o == null || count == byte.MaxValue)
                continue;
            count++;
            o.Ref.Write(w);
            var clip = Playing(radio);
            var speaker = radio.musicAudio;
            if (clip == null)
            {
                w.Put(Silent);
            }
            else if (radio.cassette != null && clip == radio.cassette.musicClip)
            {
                w.Put(Tape);
            }
            else
            {
                w.Put(Song);
                w.Put(clip.name ?? "");
                w.Put(clip.length);
            }
            w.Put(clip != null ? speaker.time : 0f);
        }
        w.Data[countAt] = count;
        Outbox.ToPlayers(w, Channel.Stream);
    }

    private static AudioClip Playing(Radio radio)
    {
        var speaker = radio.musicAudio;
        return speaker != null && speaker.isPlaying ? speaker.clip : null;
    }

    private static void Follow()
    {
        float now = Time.unscaledTime;
        Gone.Clear();
        foreach (var pair in Reports)
        {
            var heard = pair.Value;
            if (now - heard.At > Stale)
            {
                Gone.Add(pair.Key);
                continue;
            }
            var o = NetObjects.Find(heard.Item);
            var radio = o != null && o.Alive ? o.Go.GetComponentInChildren<Radio>(true) : null;
            if (radio != null && radio.musicAudio != null)
                Apply(radio, radio.musicAudio, heard, now);
        }
        foreach (long key in Gone)
            Reports.Remove(key);
    }

    private static void Apply(Radio radio, AudioSource speaker, Heard heard, float now)
    {
        AudioClip clip;
        bool loop;
        switch (heard.Kind)
        {
            case Tape:
                clip = radio.cassette != null ? radio.cassette.musicClip : null;
                loop = true;
                break;
            case Song:
                clip = Find(heard, now);
                loop = false;
                break;
            default:
                if (speaker.isPlaying)
                    speaker.Stop();
                return;
        }
        if (clip == null)
            return;
        if (speaker.clip != clip || !speaker.isPlaying)
        {
            speaker.clip = clip;
            speaker.loop = loop;
            speaker.Play();
            heard.SettleUntil = 0f;
        }
        if (now < heard.SettleUntil)
            return;

        float length = clip.length;
        float target = heard.Time + (now - heard.At) + Latency();
        if (length > 0f)
        {
            if (loop)
                target %= length;
            else if (target >= length)
                return;
        }
        float off = speaker.time - target;
        if (loop && length > 0f)
        {
            if (off > length / 2f)
                off -= length;
            else if (off < -length / 2f)
                off += length;
        }
        if (Mathf.Abs(off) <= Tolerance)
            return;
        speaker.time = Mathf.Clamp(target, 0f, Mathf.Max(0f, length - 0.05f));
        heard.SettleUntil = now + Settle;
    }

    // A song by name and length among the clips this game has loaded (Mixtapes names them the same
    // in every game). Looked for once, then again every couple of seconds while it isn't here.
    private static AudioClip Find(Heard heard, float now)
    {
        if (heard.Found != null)
            return heard.Found;
        if (now < heard.SearchAt)
            return null;
        heard.SearchAt = now + SearchAgain;
        foreach (var clip in Resources.FindObjectsOfTypeAll<AudioClip>())
        {
            if (clip.name == heard.Clip && Mathf.Abs(clip.length - heard.Length) < 0.1f)
            {
                heard.Found = clip;
                return clip;
            }
        }
        return null;
    }

    private static float Latency()
    {
        int rtt = CoopSession.Transport != null ? CoopSession.Transport.PingOf(CoopSession.HostPeer) : -1;
        return rtt > 0 ? rtt / 2000f : 0f;
    }

    private static GameObject ItemOf(Radio radio)
    {
        var usable = radio.GetComponentInParent<Usable>();
        return usable != null ? usable.gameObject : radio.gameObject;
    }

    private static void OnRadio(in Sender from, NetDataReader reader)
    {
        if (!WorldSide.Running || WorldSide.IsHost)
            return;
        float now = Time.unscaledTime;
        int count = reader.GetByte();
        for (int i = 0; i < count; i++)
        {
            var item = NetRef.Read(reader);
            byte kind = reader.GetByte();
            string clip = null;
            float length = 0f;
            if (kind == Song)
            {
                clip = reader.GetString(256);
                length = reader.GetFloat();
            }
            float time = reader.GetFloat();
            if (!Reports.TryGetValue(item.Key, out var heard))
                Reports[item.Key] = heard = new Heard { Item = item };
            if (heard.Kind != kind || heard.Clip != clip)
            {
                heard.Found = null;
                heard.SearchAt = 0f;
                heard.SettleUntil = 0f;
            }
            heard.Kind = kind;
            heard.Clip = clip;
            heard.Length = length;
            heard.Time = time;
            heard.At = now;
        }
    }
}
