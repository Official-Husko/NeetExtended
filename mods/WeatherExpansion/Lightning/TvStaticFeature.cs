using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace WeatherExpansion.Lightning;

/// <summary>
/// A strike flicks the TV to static for about a second and then it is what it was. The static is a copy
/// of the game's own static channel (its noise picture) laid over the screen, shuffled a few times a
/// second, so the channel underneath keeps running (a video keeps its place) and nothing about the TV's
/// state changes: every game shows it by itself at the strike, which co-op already has in step.
/// </summary>
internal sealed class TvStaticFeature : StrikeFeature
{
    public const string FeatureId = "TvStatic";
    private const float Step = 0.045f;

    private GameObject _overlay;
    private RectTransform _rect;
    private Image _image;
    private float _until;
    private float _nextStep;

    public override string Id => FeatureId;
    public override string Name => "Lightning static on the TV";
    public override string Description =>
        "Experimental. A lightning strike flicks the TV to static for a moment while it's on, then it shows " +
        "what it showed. How long is in Settings.";

    protected override void Begin() => SceneManager.sceneUnloaded += OnSceneUnloaded;

    protected override void End()
    {
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        Drop();
    }

    protected override void Strike(float power)
    {
        var tv = HouseManager._ != null ? HouseManager._.tv : null;
        if (tv == null || !tv.isOn || !Build(tv))
            return;
        float seconds = WeatherExpansionMod.Config.Value.TvStaticSeconds * Mathf.Clamp(power, 0.6f, 1.5f);
        if (seconds <= 0.05f)
            return;
        _overlay.transform.SetAsLastSibling(); // A station added after the first strike would sit on top.
        _until = Time.unscaledTime + seconds;
        _nextStep = 0f;
        _overlay.SetActive(true);
        if (tv.audioTv != null)
            tv.audioTv.PlayOneShot(CrackleClip.Get(), Mathf.Clamp01(0.4f * power));
    }

    public override void OnUpdate()
    {
        if (_overlay == null || !_overlay.activeSelf)
            return;
        var tv = HouseManager._ != null ? HouseManager._.tv : null;
        float now = Time.unscaledTime;
        if (now >= _until || tv == null || !tv.isOn)
        {
            _overlay.SetActive(false);
            return;
        }
        if (now < _nextStep)
            return;
        _nextStep = now + Step;
        // Flipping and tinting the one noise picture is what makes it crawl.
        _rect.localScale = new Vector3(Random.value < 0.5f ? 1f : -1f, Random.value < 0.5f ? 1f : -1f, 1f);
        float grey = Random.Range(0.7f, 1f);
        _image.color = new Color(grey, grey, grey, 1f);
    }

    // Makes the overlay the first time; false when the static channel isn't there.
    private bool Build(TV tv)
    {
        if (_overlay != null)
            return true;
        var source = tv.transform.Find("Channel1_Static");
        if (source == null || source.GetComponent<Image>() == null)
        {
            WeatherExpansionMod.Instance.Log.Warn("Lightning static: the TV's static channel wasn't found.");
            return false;
        }
        _overlay = Object.Instantiate(source.gameObject, tv.transform);
        _overlay.name = "WeatherExpansion_Static";
        var module = _overlay.GetComponent<ScreenModule>();
        if (module != null)
            Object.Destroy(module); // A channel the TV doesn't know about; only the picture is wanted.
        _rect = (RectTransform)_overlay.transform;
        _image = _overlay.GetComponent<Image>();
        _image.raycastTarget = false;
        _overlay.SetActive(false);
        return true;
    }

    private void OnSceneUnloaded(Scene scene) => Drop();

    private void Drop()
    {
        if (_overlay != null)
            Object.Destroy(_overlay);
        _overlay = null;
        _rect = null;
        _image = null;
    }
}
