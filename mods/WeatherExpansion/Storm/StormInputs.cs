namespace WeatherExpansion.Storm;

/// <summary>
/// What the storm is doing right now, as far as the looks care: multipliers that are 1 until
/// something sets them. This is the one place the framework's Sky API (storm state, fog density,
/// rain strength) plugs in later: set these from its state and every look follows. Nothing here
/// is saved or synced; each game draws its own copy.
/// </summary>
internal static class StormInputs
{
    /// <summary>Multiplies the fog density (drizzle below 1, heavy rain above).</summary>
    public static float FogDensity = 1f;

    /// <summary>Multiplies how wet the windows get and how much mist hangs in the yards.</summary>
    public static float Wetness = 1f;
}
