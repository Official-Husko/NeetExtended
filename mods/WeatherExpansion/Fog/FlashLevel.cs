using UnityEngine;

namespace WeatherExpansion.Fog;

/// <summary>
/// How bright a lightning flash is right now, 0 to 1. The game makes a flash by pushing the moon
/// light from 0.2 up to 14 to 22 and back down (HouseManager.Thunder), so the moon's own intensity
/// is the flash, and following it needs no event and survives house loads.
/// </summary>
internal static class FlashLevel
{
    private const float Floor = 0.3f;
    private const float Full = 14f;

    public static float Of(Light moon)
    {
        if (moon == null)
            return 0f;
        return Mathf.Clamp01((moon.intensity - Floor) / (Full - Floor));
    }
}
