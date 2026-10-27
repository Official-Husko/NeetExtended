using CoopSteam.Native;
using Steamworks;

namespace CoopSteam;

/// <summary>A one-line check of the Steam side, for the dev bridge: <c>CoopSteam.SteamProbe.Run()</c>.</summary>
public static class SteamProbe
{
    public static string Run()
    {
        string why = SteamRuntime.Ensure();
        if (why != null)
            return "Steam is not usable: " + why;
        SteamRuntime.Pump();
        var relay = SteamRuntime.RelayStatus(out string detail);
        return $"Steam init OK | SteamID {SteamUser.GetSteamID().m_SteamID} | persona \"{SteamFriends.GetPersonaName()}\""
            + $" | app {SteamUtils.GetAppID().m_AppId} | overlay {(SteamUtils.IsOverlayEnabled() ? "on" : "off")}"
            + $" | relay {SteamRuntime.Describe(relay)}{(string.IsNullOrEmpty(detail) ? "" : " (" + detail + ")")}";
    }
}
