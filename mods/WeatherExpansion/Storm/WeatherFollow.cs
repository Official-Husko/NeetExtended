using NeetModKit.Game;
using UnityEngine;

namespace WeatherExpansion.Storm;

/// <summary>
/// What the looks take from the weather (<see cref="Sky.Weather"/>): how thick the fog is, how much mist
/// hangs over the yards, how wet the windows get and how fast drops run. Each has a value per kind
/// of weather and follows the weather's intensity; they move toward the new values over a few seconds
/// when the weather changes, so a storm rolls in instead of switching on. Read the properties, call
/// <see cref="Tick"/> once per frame. Nothing allocates.
/// </summary>
internal static class WeatherFollow
{
    private const float Seconds = 3f; // time constant of the approach: most of the way there after about 7 s

    private static bool _started;
    private static float _fog = 1f;
    private static float _wetness = 1f;
    private static float _speed = 1f;

    /// <summary>Multiplies the fog's thickness and the mist (1 in the usual rain).</summary>
    public static float Fog => _fog;

    /// <summary>Multiplies how many drops land on the windows and how fast they grow.</summary>
    public static float Wetness => _wetness;

    /// <summary>Multiplies how fast running drops slide.</summary>
    public static float Speed => _speed;

    public static void Tick(float dt)
    {
        var weather = Sky.Weather;
        float intensity = Mathf.Clamp(weather.Intensity, 0f, 2f);
        float fog, wet, speed;
        switch (weather.Kind)
        {
            case WeatherKind.Drizzle:
                fog = 0.7f; wet = 0.45f; speed = 0.7f;
                break;
            case WeatherKind.Storm:
                fog = 1.35f; wet = 1.6f; speed = 1.5f;
                break;
            default:
                fog = 1f; wet = 1f; speed = 1f;
                break;
        }
        // Intensity 1 is the kind's usual strength; above it the kind gets heavier, below it lighter.
        fog *= 0.8f + 0.2f * intensity;
        wet *= intensity;
        speed *= 0.8f + 0.2f * intensity;

        if (!_started)
        {
            _started = true;
            _fog = fog; _wetness = wet; _speed = speed;
            return;
        }
        float k = 1f - Mathf.Exp(-dt / Seconds);
        _fog = Mathf.Lerp(_fog, fog, k);
        _wetness = Mathf.Lerp(_wetness, wet, k);
        _speed = Mathf.Lerp(_speed, speed, k);
    }
}
