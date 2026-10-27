using UnityEngine;

namespace UnofficialNeetPatches.Fixes;

/// <summary>
/// SaveLoadHandler.timeToLastLoad is the Time.time of the last load (0 when nothing was loaded), but
/// several checks in the game compare it as if it counted down. These helpers give the checks the
/// answer they were written for: how long ago the load was.
/// </summary>
internal static class LoadClock
{
    /// <summary>Seconds a load has to be over before timers that wait for it start again.</summary>
    public const float SettleSeconds = 2f;

    /// <summary>
    /// What a "no load happened yet" check should see: the real value while a load is still recent,
    /// 0 once <see cref="SettleSeconds"/> have passed (or if there was none).
    /// </summary>
    public static float RecentLoadTime()
    {
        float loaded = SaveLoadHandler.timeToLastLoad;
        return loaded > 0f && Time.time - loaded <= SettleSeconds ? loaded : 0f;
    }

    /// <summary>Time since the load, for the argument order of the game's <c>timeToLastLoad - Time.time</c>.</summary>
    public static float Since(float loaded, float now) => now - loaded;
}
