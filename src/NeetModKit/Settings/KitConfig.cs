using System.ComponentModel;

namespace NeetModKit.Settings;

/// <summary>Framework settings, stored in Mods/NeetModKit/config.json and editable in the mod menu.</summary>
public sealed class KitConfig
{
    [Description("Key that opens the mod menu (Unity key name, e.g. F1, F2, Insert).")]
    public string MenuKey = "F1";

    [Description("Pause the game while the mod menu is open.")]
    public bool PauseWhileOpen = true;

    [Description("Size of the mod menu.")]
    [Range(0.6f, 2f)]
    public float MenuScale = 1f;

    [Label("Show What's New after updates")]
    [Description("Open What's New by itself after mods update.")]
    public bool ShowWhatsNewOnUpdate = true;

    [Description("Write saves as compressed .nsav files (about 16x smaller). Off writes plain JSON like the original game. Both kinds always load.")]
    public bool CompressSaves = true;

    [Label("Saves to keep")]
    [Description("The game writes a new save file every time it saves; older ones beyond this count are deleted.")]
    [Range(3, 100)]
    public int KeepSaves = 25;

    /// <summary>Newest changelog version the player has seen. Internal bookkeeping, not shown.</summary>
    [Hidden]
    public string LastSeenVersion = "";
}
