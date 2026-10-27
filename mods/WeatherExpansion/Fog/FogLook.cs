using NeetModKit.Events;
using UnityEngine;
using WeatherExpansion.Storm;

namespace WeatherExpansion.Fog;

/// <summary>
/// Owns the scene's fog colour, fog density and sky tint while a fog look is on, and puts the game's
/// own values back when the last one goes. Two features ask for it: the haze (colour and density) and
/// the lightning glow (fog and sky follow the flash). They share this so they never fight over the
/// same settings. Called once per frame from the mod; nothing here allocates.
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
    private static float _fogDensity;
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

        Color color = _fogColor;
        float density = _fogDensity;
        if (_haze)
        {
            color = HazeColor * config.FogBrightness;
            density = _fogDensity * config.FogThickness * StormInputs.FogDensity;
            density *= Drift(Time.time);
        }
        RenderSettings.fogColor = Color.Lerp(color, FlashFog, flash);
        RenderSettings.fogDensity = density;

        if (_skyHasTint && _glow)
            _sky.SetColor("_Tint", Color.Lerp(_skyTint, FlashSky, flash));
    }

    // The fog thins and thickens slowly, a different rhythm every minute or so.
    private static float Drift(float time) => 1f + 0.16f * Mathf.Sin(time * 0.13f) * Mathf.Sin(time * 0.071f + 1.3f);

    private static bool Capture()
    {
        if (!RenderSettings.fog)
            return false;
        _fogColor = RenderSettings.fogColor;
        _fogDensity = RenderSettings.fogDensity;
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
            RenderSettings.fogDensity = _fogDensity;
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
