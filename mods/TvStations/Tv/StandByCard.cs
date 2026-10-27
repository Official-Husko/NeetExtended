using NeetModKit.Ui.Toolkit;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace TvStations.Tv;

/// <summary>
/// The old "please stand by" card a station shows while its video isn't running: the file hasn't
/// arrived yet, it's still loading, co-op is holding it until everyone has it, or it won't play.
/// A sepia test pattern with a rolling picture band, a flickering title and a line underneath.
/// Shows after a moment without the video playing, so switching channels doesn't flash it.
/// </summary>
internal sealed class StandByCard : MonoBehaviour
{
    private const float ShowAfter = 0.3f;
    private const float RollSeconds = 3.5f;
    private const int PatternWidth = 320;
    private const int PatternHeight = 180;

    private static readonly string[] Lines =
    {
        "WE ARE EXPERIENCING TECHNICAL DIFFICULTIES",
        "SOME DUMBASS IS STILL DOWNLOADING THE TAPE",
        "HOLD YOUR DICK. ITS LOADING",
        "THE PROJECTIONIST IS TAKING A SHIT",
        "DONT TOUCH THAT DIAL, BITCH",
    };

    private static readonly Color Cream = new Color(0.98f, 0.93f, 0.8f);
    private static Texture2D _pattern;

    private VideoPlayer _player;
    private GameObject _card;
    private RawImage _background;
    private RectTransform _band;
    private Text _title;
    private Text _line;
    private float _stoppedSince = -1f;
    private bool _shown;
    private float _nextGrain;

    public static StandByCard Add(GameObject channel, VideoPlayer player)
    {
        var card = channel.AddComponent<StandByCard>();
        card._player = player;
        card.Build();
        return card;
    }

    /// <summary>The pattern is made once and kept; this lets it go (the mod unloads).</summary>
    public static void ReleasePattern()
    {
        if (_pattern != null)
            Destroy(_pattern);
        _pattern = null;
    }

    private void OnEnable()
    {
        _stoppedSince = Time.unscaledTime;
        SetShown(false);
    }

    private void Update()
    {
        if (_player != null && _player.isPlaying)
        {
            _stoppedSince = -1f;
            if (_shown)
                SetShown(false);
            return;
        }
        float now = Time.unscaledTime;
        if (_stoppedSince < 0f)
            _stoppedSince = now;
        if (!_shown && now - _stoppedSince >= ShowAfter)
            SetShown(true);
        if (_shown)
            Animate(now);
    }

    private void SetShown(bool shown)
    {
        _shown = shown;
        if (_card == null)
            return;
        if (shown)
            _line.text = Lines[Random.Range(0, Lines.Length)];
        _card.SetActive(shown);
    }

    private void Animate(float now)
    {
        // The picture rolls like a TV whose vertical hold is off.
        float roll = now % RollSeconds / RollSeconds;
        _band.anchorMin = new Vector2(0f, 1f - roll - 0.12f);
        _band.anchorMax = new Vector2(1f, 1f - roll);

        float flicker = 0.82f + 0.18f * Mathf.PerlinNoise(now * 6f, 0.3f);
        _title.color = new Color(Cream.r, Cream.g, Cream.b, flicker);

        if (now >= _nextGrain)
        {
            _nextGrain = now + 0.06f;
            _background.uvRect = new Rect(Random.Range(-0.004f, 0.004f), Random.Range(-0.004f, 0.004f), 1f, 1f);
        }
    }

    private void Build()
    {
        _card = new GameObject("StandBy", typeof(RectTransform));
        _card.transform.SetParent(transform, false);
        Fill((RectTransform)_card.transform, 0f, 1f);

        _background = _card.AddComponent<RawImage>();
        _background.texture = Pattern();
        _background.raycastTarget = false;

        var bandGo = new GameObject("Band", typeof(RectTransform));
        bandGo.transform.SetParent(_card.transform, false);
        _band = (RectTransform)bandGo.transform;
        _band.offsetMin = _band.offsetMax = Vector2.zero;
        var band = bandGo.AddComponent<Image>();
        band.color = new Color(1f, 0.96f, 0.85f, 0.08f);
        band.raycastTarget = false;

        var banner = new GameObject("Banner", typeof(RectTransform));
        banner.transform.SetParent(_card.transform, false);
        Fill((RectTransform)banner.transform, 0.36f, 0.64f);
        var bannerImage = banner.AddComponent<Image>();
        bannerImage.color = new Color(0.08f, 0.06f, 0.05f, 0.88f);
        bannerImage.raycastTarget = false;

        _title = AddText("Title", 0.38f, 0.62f, "PLEASE STAND BY", Cream);
        _line = AddText("Line", 0.07f, 0.27f, Lines[0], new Color(0.12f, 0.1f, 0.08f));
        _card.SetActive(false);
    }

    private Text AddText(string name, float bottom, float top, string text, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(_card.transform, false);
        var rect = (RectTransform)go.transform;
        Fill(rect, bottom, top);
        rect.anchorMin = new Vector2(0.06f, bottom);
        rect.anchorMax = new Vector2(0.94f, top);

        var label = go.AddComponent<Text>();
        label.font = DisplayFont();
        label.text = text;
        label.color = color;
        label.alignment = TextAnchor.MiddleCenter;
        label.resizeTextForBestFit = true;
        label.resizeTextMinSize = 1;
        label.resizeTextMaxSize = 400;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Truncate;
        label.raycastTarget = false;
        return label;
    }

    // The menus' display font, if the pause menu is loaded; Unity's own otherwise.
    private static Font DisplayFont()
    {
        if (UiThemes.Menu.Ensure() && UiThemes.Menu.Has(UiTemplate.Header))
        {
            var header = UiThemes.Menu.Get(UiTemplate.Header).GetComponent<Text>();
            if (header != null && header.font != null)
                return header.font;
        }
        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    private static void Fill(RectTransform rect, float bottom, float top)
    {
        rect.anchorMin = new Vector2(0f, bottom);
        rect.anchorMax = new Vector2(1f, top);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    // An old test card: rings round the middle, a sunburst outside them, a dark edge and some grain, in sepia.
    private static Texture2D Pattern()
    {
        if (_pattern != null)
            return _pattern;
        var pixels = new Color32[PatternWidth * PatternHeight];
        var random = new System.Random(1962);
        for (int y = 0; y < PatternHeight; y++)
        {
            for (int x = 0; x < PatternWidth; x++)
            {
                float u = (x - PatternWidth * 0.5f) / PatternHeight;
                float v = (y - PatternHeight * 0.5f) / PatternHeight;
                float r = Mathf.Sqrt(u * u + v * v);
                float angle = Mathf.Atan2(v, u);

                float value = 0.55f;
                if (r < 0.44f)
                    value = Mathf.Sin(r * 70f) > 0.55f ? 0.85f : 0.62f;
                else
                    value = Mathf.Sin(angle * 18f) > 0f ? 0.7f : 0.42f;
                if (r > 0.43f && r < 0.46f)
                    value = 0.2f;
                value *= 1f - Mathf.Clamp01((r - 0.35f) * 0.9f);
                value += (float)(random.NextDouble() - 0.5) * 0.08f;
                value = Mathf.Clamp01(value);
                pixels[y * PatternWidth + x] = new Color32(
                    (byte)(255 * value), (byte)(235 * value), (byte)(200 * value), 255);
            }
        }
        _pattern = new Texture2D(PatternWidth, PatternHeight, TextureFormat.RGBA32, false)
        {
            name = "TvStations_StandBy",
            wrapMode = TextureWrapMode.Mirror,
            filterMode = FilterMode.Bilinear,
        };
        _pattern.SetPixels32(pixels);
        _pattern.Apply(false, true);
        return _pattern;
    }
}
