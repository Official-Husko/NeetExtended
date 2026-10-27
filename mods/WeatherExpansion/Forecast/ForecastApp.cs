using NeetModKit.Desktop;

namespace WeatherExpansion.Forecast;

/// <summary>
/// A website on the in-game computer with the weather now, the forecast and the next lightning.
/// Registered when the mod loads; the page is built again for every house.
/// </summary>
internal static class ForecastApp
{
    public const string AppId = "WeatherForecast";

    private static ForecastPage _page;

    public static void Register(WeatherExpansionMod owner)
    {
        _page = new ForecastPage();
        ComputerApps.Register(owner, new ComputerAppDefinition
        {
            Id = AppId,
            Title = "Wetballs Weather",
            Description = "The weather now, what's coming and when the next lightning is.",
            Url = "wetballs",
            Height = 120f,
            StartMenuLabel = "Weather",
            Build = _page.Build,
        });
    }

    /// <summary>Every frame from the mod's update; the page does nothing unless it is on screen.</summary>
    public static void Tick() => _page?.Tick();

    public static void Release() => _page = null;
}
