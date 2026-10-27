using NeetModKit.Features;
using NeetModKit.Game;
using UnityEngine;

namespace WeatherExpansion.Lightning;

/// <summary>
/// A feature that does something to the house at every lightning strike. <c>Sky.OnStrike</c> is raised in
/// every game at the moment of the flash (a co-op client replays the host's strikes), so each game runs
/// its own copy of the effect and they happen together. Nothing runs between strikes: effects that last a
/// moment tick from <see cref="ModFeature.OnUpdate"/> and that returns at once when none is running.
/// </summary>
internal abstract class StrikeFeature : ModFeature
{
    protected override void OnEnable()
    {
        Sky.OnStrike.Subscribe(this, OnStruck);
        Begin();
    }

    protected override void OnDisable() => End();

    /// <summary>The feature was switched on.</summary>
    protected virtual void Begin() { }

    /// <summary>Lightning struck; <paramref name="power"/> is around 1 (0.5 to 1.5).</summary>
    protected abstract void Strike(float power);

    /// <summary>The feature was switched off or the mod unloads: put everything back.</summary>
    protected abstract void End();

    private void OnStruck() => Strike(Power());

    /// <summary>
    /// How hard this strike hits the things in the house: the weather's intensity (kept between 0.5 and
    /// 1.5), so a heavy storm hits harder than a light one. The API doesn't say how far away a strike is,
    /// and a roll of our own would differ between co-op games, while the weather is the same in all of them.
    /// </summary>
    private static float Power() => Mathf.Clamp(Sky.Weather.Intensity, 0.5f, 1.5f);
}
