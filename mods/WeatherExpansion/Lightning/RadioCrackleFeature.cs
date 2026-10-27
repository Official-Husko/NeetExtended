using System.Collections.Generic;
using UnityEngine;

namespace WeatherExpansion.Lightning;

/// <summary>
/// A strike makes every radio that is playing crackle: a burst of static from the radio and the music
/// cutting in and out for a moment, then coming back. The radio's own music source keeps playing (so a
/// tape keeps its place and co-op's radio sync isn't disturbed); only its volume is pushed down and put
/// back to what it was. Local audio in every game.
/// </summary>
internal sealed class RadioCrackleFeature : StrikeFeature
{
    public const string FeatureId = "RadioCrackle";
    private const float Chunk = 0.07f;
    private const float Quiet = 0.08f;

    private sealed class Dip
    {
        public Radio Radio;
        public float Volume;
        public float Start;
        public float Length;
        public float Depth;
    }

    private readonly List<Dip> _dips = new List<Dip>();

    public override string Id => FeatureId;
    public override string Name => "Lightning crackle on the radio";
    public override string Description =>
        "Experimental. A lightning strike makes a radio that's playing crackle with static and cuts the " +
        "music in and out for a moment. How strong is in Settings.";

    protected override void End()
    {
        for (int i = _dips.Count - 1; i >= 0; i--)
            Restore(_dips[i]);
        _dips.Clear();
    }

    protected override void Strike(float power)
    {
        float amount = Mathf.Clamp01(WeatherExpansionMod.Config.Value.RadioCrackle);
        if (amount <= 0f)
            return;
        float now = Time.unscaledTime;
        foreach (var radio in Object.FindObjectsOfType<Radio>())
        {
            if (radio.musicAudio == null || !radio.musicAudio.isPlaying)
                continue;
            var dip = Find(radio);
            if (dip == null)
            {
                dip = new Dip { Radio = radio, Volume = radio.musicAudio.volume };
                _dips.Add(dip);
            }
            dip.Start = now;
            dip.Length = 1.1f * Mathf.Clamp(power, 0.6f, 1.6f);
            dip.Depth = amount;
            if (radio.clickAudio != null)
                radio.clickAudio.PlayOneShot(CrackleClip.Get(), Mathf.Clamp01(0.7f * amount * Mathf.Min(power, 1.4f)));
        }
    }

    public override void OnUpdate()
    {
        if (_dips.Count == 0)
            return;
        float now = Time.unscaledTime;
        for (int i = _dips.Count - 1; i >= 0; i--)
        {
            var dip = _dips[i];
            if (dip.Radio == null || dip.Radio.musicAudio == null)
            {
                _dips.RemoveAt(i);
                continue;
            }
            float t = now - dip.Start;
            if (t >= dip.Length)
            {
                Restore(dip);
                _dips.RemoveAt(i);
                continue;
            }
            // Cut out in random chunks, fewer of them as it dies down.
            float k = t / dip.Length;
            bool cut = Hash((int)(t / Chunk)) < (1f - k) * 0.9f;
            float level = cut ? Quiet : Mathf.Lerp(0.45f, 1f, k);
            dip.Radio.musicAudio.volume = dip.Volume * Mathf.Lerp(1f, level, dip.Depth);
        }
    }

    private Dip Find(Radio radio)
    {
        for (int i = 0; i < _dips.Count; i++)
        {
            if (_dips[i].Radio == radio)
                return _dips[i];
        }
        return null;
    }

    private static void Restore(Dip dip)
    {
        if (dip.Radio != null && dip.Radio.musicAudio != null)
            dip.Radio.musicAudio.volume = dip.Volume;
    }

    // 0 to 1, the same for the same number.
    private static float Hash(int n)
    {
        uint x = (uint)n * 2654435761u;
        x ^= x >> 15;
        x *= 2246822519u;
        x ^= x >> 13;
        return (x & 0xFFFF) / 65535f;
    }
}
