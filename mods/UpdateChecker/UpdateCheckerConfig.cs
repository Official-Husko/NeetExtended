using System.ComponentModel;
using NeetModKit.Settings;

namespace UpdateChecker;

/// <summary>Stored in Mods/UpdateChecker/config.json and shown in the mod menu.</summary>
public sealed class UpdateCheckerConfig
{
    [Label("Check for updates")]
    [Description("Asks GitHub for the newest release once when the game starts and then every hour. It sends nothing about you. Off: the mod never goes online.")]
    public bool Check = true;

    [Label("Tell me in the game")]
    [Description("While an update is out, the game sends one email and rings the phone up to twice per save, about once an hour of play. Off: only the note in the corner.")]
    public bool TellInGame = true;
}
