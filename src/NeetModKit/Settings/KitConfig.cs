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

    /// <summary>Newest changelog version the player has seen. Internal bookkeeping, not shown.</summary>
    [Hidden]
    public string LastSeenVersion = "";
}
