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
}

public sealed class ChangelogItem
{
    [JsonProperty("text")] public string Text;
    [JsonProperty("area")] public string Area;
}
