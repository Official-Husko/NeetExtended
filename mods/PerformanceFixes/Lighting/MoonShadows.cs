using UnityEngine;

namespace PerformanceFixes.Lighting;

/// <summary>
/// Cuts what the moon (the house's only directional light, HouseManager.mainDirectionalLight) costs,
/// according to <see cref="MoonShadowMode"/>. Its shadows are a big part of a frame (see
/// docs/research/performance.md), for a light at intensity 0.2 that nobody can see. Indoors the moon
/// reaches nothing anyway (the shadows keep it out), so without them the light is switched off there:
/// a shadowless moon would shine through the walls. A lightning flash (the game pushes the moon's
/// intensity to 14-22 for a moment) gets the real thing, shadows and all, or the whole house would
/// flash white. Only the light's own switches change, never its intensity, so the lightning keeps
/// working. <see cref="ShadowControl"/> leaves directional lights alone, so the two don't meet. The
/// player's brain says where they are (Brain.OnLocationChange, what the game uses for the rain sound).
/// </summary>
internal static class MoonShadows
{
    /// <summary>Above this the moon is mid-flash. Its resting intensity is 0.2.</summary>
    private const float FlashIntensity = 1f;

    private static Light _moon;
    private static LightShadows _shadows = LightShadows.Soft;
    private static BrainPlayer _brain;
    private static MoonShadowMode _mode = MoonShadowMode.On;
    private static bool _outdoors;
    private static bool _flashing;

    /// <summary>Applies a mode to the current house. Run when the house is ready and when the setting changes.</summary>
    public static void Apply(MoonShadowMode mode)
    {
        Unhook();
        var moon = HouseManager._ != null ? HouseManager._.mainDirectionalLight : null;
        if (_moon != null && _moon != moon)
            Show(true, _shadows);
        if (moon != _moon)
        {
            _moon = moon;
            if (moon != null && moon.shadows != LightShadows.None)
                _shadows = moon.shadows;
        }
        _mode = mode;
        if (moon == null)
            return;

        _brain = HouseManager._.player != null ? HouseManager._.player.GetComponent<BrainPlayer>() : null;
        _outdoors = true;
        if (_brain != null)
        {
            _outdoors = IsOutdoors(_brain.currentLocation);
            if (mode != MoonShadowMode.On)
                _brain.OnLocationChange += OnLocation;
        }
        _flashing = false;
        Update();
    }

    /// <summary>Gives the moon its light and shadows back.</summary>
    public static void Restore()
    {
        Unhook();
        Show(true, _shadows);
        _moon = null;
    }

    /// <summary>The scene's objects are gone; forget them without touching anything.</summary>
    public static void Forget()
    {
        _brain = null;
        _moon = null;
    }

    /// <summary>The front and back yard. The shed counts as indoors: it has a roof and walls.</summary>
    public static bool IsOutdoors(Location location) => location == Location.FrontYard || location == Location.Backyard;

    /// <summary>Every frame while the mode isn't On: spots a lightning flash starting and ending (one float read).</summary>
    public static void Tick()
    {
        if (_moon == null || _mode == MoonShadowMode.On)
            return;
        var flashing = _moon.intensity > FlashIntensity;
        if (flashing == _flashing)
            return;
        _flashing = flashing;
        Update();
    }

    private static void OnLocation(Location location)
    {
        _outdoors = IsOutdoors(location);
        Update();
    }

    private static void Update()
    {
        if (_mode == MoonShadowMode.On || _flashing)
            Show(true, _shadows);
        else if (_outdoors)
            Show(true, _mode == MoonShadowMode.Off ? LightShadows.None : _shadows);
        else
            Show(false, LightShadows.None);
    }

    private static void Unhook()
    {
        if (_brain != null)
            _brain.OnLocationChange -= OnLocation;
        _brain = null;
    }

    private static void Show(bool on, LightShadows shadows)
    {
        if (_moon == null)
            return;
        if (_moon.enabled != on)
            _moon.enabled = on;
        if (on && _moon.shadows != shadows)
            _moon.shadows = shadows;
    }
}
