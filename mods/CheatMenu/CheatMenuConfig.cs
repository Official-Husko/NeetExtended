using System.ComponentModel;
using NeetModKit.Settings;

namespace CheatMenu;

public sealed class CheatMenuConfig
{
    [Label("Key")]
    [Description("Key that opens the cheat menu (Unity key name, e.g. Insert, Home, F2).")]
    public string Key = "Insert";

    [Description("Pause the game while the cheat menu is open. Off lets you watch changes happen.")]
    public bool PauseWhileOpen;
}
