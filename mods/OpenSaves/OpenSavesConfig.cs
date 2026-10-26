using System.ComponentModel;
using NeetModKit.Settings;

namespace OpenSaves;

/// <summary>Stored in Mods/OpenSaves/config.json.</summary>
public sealed class OpenSavesConfig
{
    [Label("Saves to keep")]
    [Description("Used by Save cleanup: older save files beyond this count are deleted after each save.")]
    [Range(3, 100)]
    public int KeepSaves = 25;
}
