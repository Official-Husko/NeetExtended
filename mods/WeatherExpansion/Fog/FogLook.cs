using NeetModKit.Events;
using UnityEngine;

namespace WeatherExpansion.Fog;

/// <summary>
/// Owns the scene's fog colour and sky tint while a fog look is on, and puts the game's own values back
/// when the last one goes. Two features ask for it: the haze (colour) and the lightning glow (fog and
/// sky follow the flash). They share this so they never fight over the same settings. The fog's
/// thickness is not here: <see cref="StormFogFeature"/> sets it through <c>Sky.FogAmount</c>, which the
/// framework applies to the scene's density. Called once per frame from the mod; nothing here allocates.
/// </summary>
internal static class FogLook
{
    // A dark blue-grey instead of black, so the yard is a silhouette against something.
    private static readonly Color HazeColor = new Color(0.035f, 0.05f, 0.085f);
    // The fog and sky at the height of a flash.
    private static readonly Color FlashFog = new Color(0.40f, 0.47f, 0.66f);
    private static readonly Color FlashSky = new Color(0.50f, 0.58f, 0.78f);

    private static bool _haze;
    private static bool _glow;
    private static bool _captured;
    private static Color _fogColor;
    private static Light _moon;
    private static Material _sky;
    private static bool _skyHasTint;
    private static Color _skyTint;

    public static bool Haze
    {
        get => _haze;
        set { _haze = value; Changed(); }
    }

    public static bool Glow
    {
        get => _glow;
        set { _glow = value; Changed(); }
    }

    private static void Changed()
    {
        if (!_haze && !_glow)
            Restore();
    }

    /// <summary>The scene is going away: forget its fog and put the shared sky material back.</summary>
    public static void SceneGone()
    {
        RestoreSky();
        _captured = false;
        _moon = null;
        _sky = null;
    }

    public static void Tick(WeatherExpansionConfig config)
    {
        if ((!_haze && !_glow) || !GameEvents.IsHouseReady)
            return;
        if (!_captured && !Capture())
            return;

        float flash = _glow ? FlashLevel.Of(_moon) * config.LightningGlow : 0f;
        flash = Mathf.Clamp01(flash);

        Color color = _haze ? HazeColor * config.FogBrightness : _fogColor;
        RenderSettings.fogColor = Color.Lerp(color, FlashFog, flash);

        if (_skyHasTint && _glow)
            _sky.SetColor("_Tint", Color.Lerp(_skyTint, FlashSky, flash));
    }

    private static bool Capture()
    {
        if (!RenderSettings.fog)
            return false;
        _fogColor = RenderSettings.fogColor;
        _moon = RenderSettings.sun;
        _sky = RenderSettings.skybox;
        _skyHasTint = _sky != null && _sky.HasProperty("_Tint");
        if (_skyHasTint)
            _skyTint = _sky.GetColor("_Tint");
        _captured = true;
        return true;
    }

    private static void Restore()
    {
        if (_captured && GameEvents.IsHouseReady)
        {
            RenderSettings.fogColor = _fogColor;
        }
        RestoreSky();
        _captured = false;
    }

    private static void RestoreSky()
    {
        if (_captured && _skyHasTint && _sky != null)
            _sky.SetColor("_Tint", _skyTint);
    }
}
