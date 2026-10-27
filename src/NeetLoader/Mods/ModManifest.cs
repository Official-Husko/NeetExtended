using System.Collections.Generic;
using Newtonsoft.Json;

namespace NeetLoader.Mods;

/// <summary>Contents of a mod's mod.json.</summary>
public sealed class ModManifest
{
    [JsonProperty("id")] public string Id;
    [JsonProperty("name")] public string Name;
    [JsonProperty("version")] public string Version = "0.0.0";
    [JsonProperty("author")] public string Author;
    [JsonProperty("description")] public string Description;

    /// <summary>DLLs to load, relative to the mod folder. Empty means every *.dll in the folder root.</summary>
    [JsonProperty("assemblies")] public List<string> Assemblies = new List<string>();

    /// <summary>Required mods: id -> minimum version ("*" for any).</summary>
    [JsonProperty("dependencies")] public Dictionary<string, string> Dependencies = new Dictionary<string, string>();

    /// <summary>Mods that should load first if present, but aren't required.</summary>
    [JsonProperty("loadAfter")] public List<string> LoadAfter = new List<string>();

    /// <summary>Apply every [HarmonyPatch] class in the mod's assemblies automatically.</summary>
    [JsonProperty("autoPatch")] public bool AutoPatch = true;

    /// <summary>
    /// Load the DLLs from memory and watch them for changes, so a rebuild reloads the mod
    /// without restarting the game. The mod has to clean up after itself in OnUnload.
    /// </summary>
    [JsonProperty("hotReload")] public bool HotReload;

    /// <summary>
    /// Always on: the player can't switch it off or unload it (reloading still works). For the
    /// framework everything else needs, and for mods that protect something, like No Feedback.
    /// Mods it depends on are kept on too.
    /// </summary>
    [JsonProperty("essential")] public bool Essential;

    /// <summary>
    /// Off: the mod is installed but doesn't load until the player switches it on (the Mods tab's
    /// "Load when the game starts", or LOAD NOW for this session). For mods that change the game a
    /// lot, like the Randomizer.
    /// </summary>
    [JsonProperty("enabledByDefault")] public bool EnabledByDefault = true;

    public string DisplayName => string.IsNullOrEmpty(Name) ? Id : Name;
}
