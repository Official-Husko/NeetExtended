using Coop.Net;
using LiteNetLib.Utils;
using UnityEngine;

namespace Coop.World.Talk;

/// <summary>
/// What each handset shows and plays, from the host where the phones run: the screen (digits,
/// "gud fone", a caller's name), the ring and the busy tone. Checked four times a second, and sent
/// when something changed (or straight after a button press).
///
/// WorldPhones (host to all): count, then per handset: screen text, ringing, busy tone.
/// </summary>
internal static class PhoneScreens
{
    private const float Interval = 0.25f;
    private const int Ring = 2;
    private const int Busy = 3;

    private static string[] _text = new string[0];
    private static bool[] _ring = new bool[0];
    private static bool[] _busy = new bool[0];
    private static AudioSource[][] _sources = new AudioSource[0][];
    private static float _next;
    private static bool _sendAgain = true;

    public static void Init() => Messages.On(MessageIds.WorldPhones, OnPhones);

    public static void Clear()
    {
        _text = new string[0];
        _sources = new AudioSource[0][];
        _sendAgain = true;
    }

    /// <summary>Everything goes out on the next check (a player joined or resynced).</summary>
    public static void SendAgain() => _sendAgain = true;

    /// <summary>Check on the next frame instead of waiting for the interval.</summary>
    public static void SendSoon() => _next = 0f;

    /// <summary>Host, late every frame.</summary>
    public static void HostTick()
    {
        float now = Time.time;
        if (now < _next)
            return;
        _next = now + Interval;
        var phones = Speakers.Dispatch()?.phones;
        if (phones == null)
            return;
        Fit(phones);
        bool changed = _sendAgain;
        for (int i = 0; i < phones.Length; i++)
        {
            var phone = phones[i];
            if (phone == null || phone.screenText == null)
                continue;
            string text = phone.screenText.text;
            bool ring = Playing(i, Ring);
            bool busy = Playing(i, Busy);
            if (text != _text[i] || ring != _ring[i] || busy != _busy[i])
            {
                changed = true;
                _text[i] = text;
                _ring[i] = ring;
                _busy[i] = busy;
            }
        }
        if (!changed)
            return;
        _sendAgain = false;
        var w = Outbox.Begin(MessageIds.WorldPhones);
        w.Put((byte)phones.Length);
        for (int i = 0; i < phones.Length; i++)
        {
            w.Put(_text[i] ?? "");
            w.Put(_ring[i]);
            w.Put(_busy[i]);
        }
        Outbox.ToPlayers(w, Channel.Events);
    }

    private static void OnPhones(in Sender from, NetDataReader reader)
    {
        int count = reader.GetByte();
        var phones = WorldSide.Running && WorldSide.IsClient ? Speakers.Dispatch()?.phones : null;
        if (phones != null)
            Fit(phones);
        for (int i = 0; i < count; i++)
        {
            string text = reader.GetString();
            bool ring = reader.GetBool();
            bool busy = reader.GetBool();
            if (phones == null || i >= phones.Length || phones[i] == null)
                continue;
            var phone = phones[i];
            if (phone.screenText != null)
                phone.screenText.text = text;
            phone.ringing = ring;
            Sound(i, Ring, ring);
            Sound(i, Busy, busy);
        }
    }

    private static void Fit(Phone_Interface[] phones)
    {
        if (_sources.Length == phones.Length)
            return;
        _text = new string[phones.Length];
        _ring = new bool[phones.Length];
        _busy = new bool[phones.Length];
        _sources = new AudioSource[phones.Length][];
        for (int i = 0; i < phones.Length; i++)
            _sources[i] = phones[i] != null ? phones[i].GetComponents<AudioSource>() : new AudioSource[0];
    }

    private static bool Playing(int phone, int source)
    {
        var sources = _sources[phone];
        return source < sources.Length && sources[source] != null && sources[source].isPlaying;
    }

    private static void Sound(int phone, int source, bool on)
    {
        var sources = _sources[phone];
        if (source >= sources.Length || sources[source] == null)
            return;
        var audio = sources[source];
        if (on && !audio.isPlaying)
            audio.Play();
        else if (!on && audio.isPlaying)
            audio.Stop();
    }
}
