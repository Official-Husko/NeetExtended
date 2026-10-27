using NeetModKit.Features;
using NeetModKit.Game;
using UnityEngine;

namespace WeatherExpansion.Soaking;

/// <summary>
/// Standing in the rain soaks you. While the player is in the front or the back yard (the shed counts as
/// indoors) with nothing over their head, <c>PlayerStats.wetness</c> climbs at a speed that depends on the
/// weather, up to a limit that depends on it too. The game dries the player on its own everywhere
/// (1.5 a second), so a quick dash outside barely shows and a long stand in a storm ends up dripping.
///
/// What the game does with wetness: over 60 the player drips, over 50 burns are halved (and the wetness
/// drops), and over 50 a Sparker shock hurts three times as much (75 instead of 25). Limits stay under 80
/// so the player is never at the very top, which the game gives to people who jump in water.
///
/// Runs in every game for that game's own player, so each co-op player gets wet on their own body, from
/// the weather the host sets (<see cref="Sky.Weather"/>). Reads the weather, doesn't set it.
/// </summary>
internal sealed class RainWetnessFeature : ModFeature
{
    public const string FeatureId = "RainWetness";

    // Wetness gained per second before the game's drying, and the most the weather soaks you to.
    private const float DrizzleRate = 1.9f, DrizzleLimit = 25f;
    private const float RainRate = 2.5f, RainLimit = 45f;
    private const float StormRate = 3.5f, StormLimit = 75f;
    private const float Ceiling = 80f;

    private const float Step = 0.4f;
    private const float HeadHeight = 1.0f;

    private float _next;

    public override string Id => FeatureId;
    public override string Name => "Rain makes you wet";
    public override string Description =>
        "Experimental. Standing in the rain outside, with nothing over your head, soaks you: slowly in a drizzle, fast in a storm. " +
        "You dry off indoors. Wet people drip, take half the damage from fire and get shocked harder. Speed is in Settings.";

    protected override void OnEnable() => _next = 0f;

    public override void OnUpdate()
    {
        if (Time.time < _next)
            return;
        _next = Time.time + Step;

        var config = WeatherExpansionMod.Config.Value;
        if (!config.RainMakesYouWet)
            return;
        var stats = GameRefs.PlayerStats;
        var brain = GameRefs.PlayerBrain;
        if (stats == null || brain == null || !IsOutside(brain.currentLocation))
            return;
        var weather = Sky.Weather;
        Of(weather, out float rate, out float limit);
        rate *= config.SoakSpeed;
        float room = limit - stats.wetness.GetVal();
        if (rate <= 0f || room <= 0f || RainCover.IsCovered(stats.transform, HeadHeight))
            return;
        stats.wetness.AddVal(Mathf.Min(rate * Step, room));
    }

    private static bool IsOutside(Location location) => location == Location.FrontYard || location == Location.Backyard;

    private static void Of(WeatherState weather, out float rate, out float limit)
    {
        float intensity = Mathf.Clamp(weather.Intensity, 0.5f, 1.5f);
        switch (weather.Kind)
        {
            case WeatherKind.Drizzle:
                rate = DrizzleRate;
                limit = DrizzleLimit;
                break;
            case WeatherKind.Storm:
                rate = StormRate;
                limit = StormLimit;
                break;
            default:
                rate = RainRate;
                limit = RainLimit;
                break;
        }
        rate *= intensity;
        limit = Mathf.Min(Ceiling, limit * Mathf.Clamp(intensity, 0.8f, 1.2f));
    }
}
