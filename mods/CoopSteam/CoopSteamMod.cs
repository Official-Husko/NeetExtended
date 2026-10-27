using Coop.Net;
using CoopSteam.Native;
using CoopSteam.Transport;
using NeetLoader;
using NeetLoader.Logging;

namespace CoopSteam;

/// <summary>
/// Adds "Steam" to Co-op's ways of connecting. Nothing here touches Steam until the player picks it;
/// the Steam API starts the first time the option is checked.
/// </summary>
public sealed class CoopSteamMod : NeetMod
{
    public const string TransportId = "steam";

    internal static new ModLogger Log { get; private set; }

    public override void OnLoad()
    {
        Log = base.Log;
        SteamRuntime.Configure(Directory);
        Transports.Register(this, new TransportOption
        {
            Id = TransportId,
            Name = "Steam",
            Create = SteamTransport.Resume,
            Handles = SteamAddress.Handles,
            HostAddress = SteamRuntime.OwnId,
            Unavailable = SteamRuntime.Unavailable,
        });
    }

    // Steam wants its callbacks run now and then, even before a game is going (relay status, for one).
    public override void OnUpdate() => SteamRuntime.Pump();
}
