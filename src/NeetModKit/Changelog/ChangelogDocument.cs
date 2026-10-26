using System.Collections.Generic;
using Newtonsoft.Json;

namespace NeetModKit.Changelog;

/// <summary>The changelog.json format produced by "neet changelog".</summary>
public sealed class ChangelogDocument
{
    [JsonProperty("releases")] public List<ChangelogRelease> Releases = new List<ChangelogRelease>();

    public string NewestVersion => Releases.Count > 0 ? Releases[0].Version : null;
}

public sealed class ChangelogRelease
{
    [JsonProperty("version")] public string Version;
    [JsonProperty("date")] public string Date;
    [JsonProperty("released")] public bool Released;
    [JsonProperty("added")] public List<ChangelogItem> Added = new List<ChangelogItem>();
    [JsonProperty("changed")] public List<ChangelogItem> Changed = new List<ChangelogItem>();
    [JsonProperty("fixed")] public List<ChangelogItem> Fixed = new List<ChangelogItem>();
    [JsonProperty("removed")] public List<ChangelogItem> Removed = new List<ChangelogItem>();

    private string _heading;

    /// <summary>Rich-text heading for the menu, built once.</summary>
    [JsonIgnore]
    public string Heading => _heading ??=
        $"Version {Version}  <size=12><color=#999999>{Date}</color></size>" +
        (Released ? "" : "  <size=11><color=#e8c45a>in progress</color></size>");
}

public sealed class ChangelogItem
{
    [JsonProperty("text")] public string Text;
    [JsonProperty("area")] public string Area;

    private string _line;

    /// <summary>Rich-text bullet line for the menu, built once.</summary>
    [JsonIgnore]
    public string Line => _line ??= "•  " + (string.IsNullOrEmpty(Area) ? "" : $"<color=#999999>{Area}:</color> ") + Text;
}
