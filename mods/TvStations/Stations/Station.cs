namespace TvStations.Stations;

/// <summary>A video shown as a TV channel.</summary>
public sealed class Station
{
    /// <summary>Shown in the mod menu.</summary>
    public string Name;

    /// <summary>Full path of the video file.</summary>
    public string File;

    /// <summary>Where it came from ("ModId/tv/file.mp4"), for messages.</summary>
    public string Source;

    /// <summary>0 to 1, on top of the TV's own volume and the mod's setting.</summary>
    public float Volume = 1f;

    /// <summary>The game channel it takes the place of ("Sports" or "5"), or null to add it after the others.</summary>
    public string Replaces;
}
