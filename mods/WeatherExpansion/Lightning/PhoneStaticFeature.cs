using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace WeatherExpansion.Lightning;

/// <summary>
/// A strike garbles the handset screens for a moment: the digits jitter and some turn into noise. The
/// screen text itself is never touched (the phone reads it to dial and to know it is idle): a copy of the
/// text object is laid over it showing the noise while the real one is hidden, and everything is put back
/// after. With the "Static in phone calls" setting on, a call in progress also gets a burst of static from
/// the handset; the conversation itself is left alone.
/// </summary>
internal sealed class PhoneStaticFeature : StrikeFeature
{
    public const string FeatureId = "PhoneStatic";
    private const string Glyphs = "#%&$@*?!/\\|<>=+~0123456789";
    private const float Step = 0.06f;

    private sealed class Garble
    {
        public Phone_Interface Phone;
        public Text Screen;
        public Text Copy;
        public float Until;
    }

    private readonly List<Garble> _active = new List<Garble>();
    private readonly System.Text.StringBuilder _text = new System.Text.StringBuilder(16);
    private float _nextStep;

    public override string Id => FeatureId;
    public override string Name => "Lightning on the phones";
    public override string Description =>
        "Experimental. A lightning strike garbles the phone screens for a moment and, if set, puts static " +
        "on a call in progress. The call itself carries on.";

    protected override void End()
    {
        for (int i = _active.Count - 1; i >= 0; i--)
            Finish(_active[i]);
        _active.Clear();
    }

    protected override void Strike(float power)
    {
        var config = WeatherExpansionMod.Config.Value;
        float now = Time.unscaledTime;
        foreach (var phone in Object.FindObjectsOfType<Phone_Interface>())
        {
            if (phone.screenText == null)
                continue;
            Begin(phone, now + 0.9f * Mathf.Clamp(power, 0.6f, 1.5f));
            if (config.PhoneCallStatic && phone.talking)
                Burst(phone, power);
        }
    }

    public override void OnUpdate()
    {
        if (_active.Count == 0)
            return;
        float now = Time.unscaledTime;
        bool step = now >= _nextStep;
        if (step)
            _nextStep = now + Step;
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            var g = _active[i];
            if (g.Screen == null || g.Copy == null || now >= g.Until)
            {
                Finish(g);
                _active.RemoveAt(i);
            }
            else if (step)
            {
                Scramble(g);
            }
        }
    }

    private void Begin(Phone_Interface phone, float until)
    {
        foreach (var g in _active)
        {
            if (g.Phone == phone)
            {
                g.Until = until;
                return;
            }
        }
        var screen = phone.screenText;
        // A copy keeps the font, size and place; it is a child of the same parent, so it moves with the handset.
        var copy = Object.Instantiate(screen.gameObject, screen.transform.parent).GetComponent<Text>();
        copy.gameObject.name = "WeatherExpansion_Garble";
        copy.raycastTarget = false;
        screen.canvasRenderer.SetAlpha(0f);
        _active.Add(new Garble { Phone = phone, Screen = screen, Copy = copy, Until = until });
        Scramble(_active[_active.Count - 1]);
    }

    private static void Burst(Phone_Interface phone, float power)
    {
        var sources = phone.GetComponents<AudioSource>();
        if (sources.Length > 1)
            sources[1].PlayOneShot(CrackleClip.Get(), Mathf.Clamp01(0.6f * Mathf.Min(power, 1.5f)));
    }

    // Some of the real characters stay, the rest turn to noise, and now and then the whole screen blinks.
    private void Scramble(Garble g)
    {
        string real = g.Screen.text;
        int length = Mathf.Max(real.Length, 5);
        _text.Length = 0;
        for (int i = 0; i < length; i++)
        {
            bool keep = i < real.Length && Random.value < 0.45f;
            _text.Append(keep ? real[i] : Glyphs[Random.Range(0, Glyphs.Length)]);
        }
        g.Copy.text = _text.ToString();
        g.Copy.canvasRenderer.SetAlpha(Random.value < 0.18f ? 0f : 1f);
    }

    private static void Finish(Garble g)
    {
        if (g.Copy != null)
            Object.Destroy(g.Copy.gameObject);
        if (g.Screen != null)
            g.Screen.canvasRenderer.SetAlpha(1f);
    }
}
