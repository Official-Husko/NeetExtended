using System;
using NeetModKit.Features;
using NeetModKit.Game;
using NeetModKit.Ui.Toolkit;
using UnityEngine.UI;

namespace CheatMenu.Pages;

/// <summary>
/// The sky part of the World page: lightning on demand, and (only while Weather Expansion's storm
/// schedule runs) buttons that put the weather on a kind. Weather Expansion carries on from the kind
/// set here. The caller rebuilds the page when <see cref="WeatherButtons"/> changes.
/// </summary>
internal sealed class SkyPanel
{
    // The Weather Expansion feature that moves the weather over the night. Without it nothing
    // changes the scene when the weather is set, so the buttons would do nothing.
    private const string StormFeatureKey = "WeatherExpansion/StormSchedule";

    private Text _now;

    /// <summary>True while the weather buttons are offered.</summary>
    public static bool WeatherButtons
    {
        get
        {
            var features = ModFeatures.Features;
            for (int i = 0; i < features.Count; i++)
            {
                if (features[i].Enabled && ModFeatures.KeyOf(features[i]) == StormFeatureKey)
                    return true;
            }
            return false;
        }
    }

    public void Build(UiBuilder ui, Action<Action, string> run)
    {
        _now = null;
        var sky = ui.Panel();
        sky.Header("SKY");
        sky.Row(8f, 26f).Button("STRIKE LIGHTNING NOW", () => run(Strike, "Boom."), 260f, accent: true);
        if (!WeatherButtons)
            return;

        _now = sky.SubHeader("");
        var kinds = sky.Row(8f, 26f);
        kinds.Button("DRIZZLE", () => run(() => Set(WeatherKind.Drizzle), "Drizzle."), 120f);
        kinds.Button("RAIN", () => run(() => Set(WeatherKind.Rain), "Rain."), 120f);
        kinds.Button("STORM", () => run(() => Set(WeatherKind.Storm), "Storm."), 120f);
        sky.Text("The storm carries on from the kind you pick.");
        Refresh();
    }

    public void Refresh()
    {
        if (_now != null)
            _now.text = "WEATHER: " + Sky.Weather.Name.ToUpperInvariant();
    }

    private static void Strike()
    {
        if (!Sky.Strike())
            throw new InvalidOperationException("there's no sky to strike right now");
    }

    private static void Set(WeatherKind kind) => Sky.SetWeather(CheatMenuMod.Instance, kind);
}
