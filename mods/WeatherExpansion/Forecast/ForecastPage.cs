using NeetModKit.Desktop;
using NeetModKit.Game;
using NeetModKit.Ui;
using NeetModKit.Ui.Toolkit;
using UnityEngine;
using UnityEngine.UI;

namespace WeatherExpansion.Forecast;

/// <summary>
/// The page of the forecast site: the weather now, a line about it, what comes next with the time
/// left, and the next lightning. It shows the framework's <see cref="Sky"/> state, so on a co-op
/// client it is the host's weather. <see cref="Tick"/> runs every frame while the page is on screen
/// and only builds new text when a shown number or kind changed (once a second at most).
/// </summary>
internal sealed class ForecastPage
{
    private static readonly Color Muted = new Color(0.75f, 0.75f, 0.8f);

    private GameObject _page;
    private Text _now;
    private Text _mood;
    private Text _next;
    private Text _strike;

    // What the text on the page was made from. -2 means "not drawn yet".
    private WeatherKind _shownKind;
    private WeatherKind? _shownNext;
    private float _shownIntensity;
    private float _shownStartedAt;
    private int _shownIn = -2;
    private int _shownStrike = -2;
    private int _pick;

    public void Build(ComputerAppContext ctx)
    {
        _page = ctx.Panel.gameObject;
        var ui = ctx.Ui;
        ui.Header("WETBALLS WEATHER");
        ui.SubHeader("RIGHT NOW");
        _now = ui.Header("");
        _mood = ui.Text("", Muted);
        ui.SubHeader("COMING UP");
        _next = ui.Text("");
        ui.SubHeader("LIGHTNING");
        _strike = ui.Text("");
        _shownIn = _shownStrike = -2;
        Tick();
    }

    public void Tick()
    {
        if (_page == null || !_page.activeInHierarchy || _now == null)
            return;

        var weather = Sky.Weather;
        int until = weather.HasForecast ? Mathf.CeilToInt(weather.SecondsUntilNext) : -1;
        float strike = Sky.NextStrikeIn;
        int strikeIn = strike >= 0f ? Mathf.CeilToInt(strike) : -1;

        bool sameWeather = _shownIn != -2 && weather.Kind == _shownKind && weather.NextKind == _shownNext &&
                           weather.Intensity == _shownIntensity && weather.StartedAt == _shownStartedAt;
        if (!sameWeather)
            DrawWeather(weather);
        if (!sameWeather || until != _shownIn)
            DrawNext(weather, until);
        if (_shownStrike == -2 || strikeIn != _shownStrike)
            DrawStrike(strikeIn);
    }

    private void DrawWeather(WeatherState weather)
    {
        bool newKind = _shownIn == -2 || weather.Kind != _shownKind;
        _shownKind = weather.Kind;
        _shownNext = weather.NextKind;
        _shownIntensity = weather.Intensity;
        _shownStartedAt = weather.StartedAt;

        if (newKind)
            _pick = Random.Range(0, ForecastWords.PickCount(weather.Kind));
        _now.text = ForecastWords.Now(weather);
        _now.color = ForecastWords.ColorOf(weather.Kind);
        _mood.text = ForecastWords.Mood(weather.Kind, _pick);
    }

    private void DrawNext(WeatherState weather, int until)
    {
        _shownIn = until;
        if (!weather.HasForecast)
        {
            _next.text = ForecastWords.NoForecast(_pick);
            _next.color = Muted;
            return;
        }
        _next.text = weather.NextName.ToUpperInvariant() + " in " + ForecastWords.Clock(until);
        _next.color = ForecastWords.ColorOf(weather.NextKind.Value);
    }

    private void DrawStrike(int seconds)
    {
        _shownStrike = seconds;
        if (seconds < 0)
        {
            _strike.text = "Can't tell. Watch the sky and count your fingers.";
            _strike.color = Muted;
            return;
        }
        _strike.text = "Next one in " + ForecastWords.Clock(seconds) + ". Maybe. Don't sue us.";
        _strike.color = Color.white;
    }
}
