using System.ComponentModel;
using NeetModKit.Settings;

namespace Coop;

/// <summary>Stored in Mods/Coop/config.json and shown in the mod menu.</summary>
public sealed class CoopConfig
{
    [Label("Your name")]
    [Description("What the other players see above your head.")]
    public string PlayerName = "";

    [Label("Port")]
    [Description("The UDP port a hosted game listens on. Whoever hosts has to let it through their router and firewall.")]
    [Range(1024, 65535)]
    public int Port = 8889;

    [Label("Last address")]
    [Description("The address you joined last, filled in for next time.")]
    public string LastAddress = "";

    [Label("Max players")]
    [Description("How many people a hosted game takes, you included.")]
    [Range(2, 4)]
    public int MaxPlayers = 4;

    /// <summary>Identifies this install to hosts, so a player who comes back gets their own body back.</summary>
    [Hidden]
    public string PlayerId = "";
}
