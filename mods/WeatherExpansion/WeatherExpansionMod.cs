using NeetLoader;

namespace WeatherExpansion;

/// <summary>
/// How the storm looks. The house is one rainy night; this mod adds looks to it, each one a feature
/// that can be switched off on the Mods page. Everything here is drawn by the local game, so in
/// co-op every player sees their own copy.
/// </summary>
public sealed class WeatherExpansionMod : NeetMod
{
    internal static WeatherExpansionMod Instance { get; private set; }

    public override void OnLoad() => Instance = this;

    public override void OnUnload() => Instance = null;
}
