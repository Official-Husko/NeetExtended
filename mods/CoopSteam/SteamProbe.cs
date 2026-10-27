using CoopSteam.Native;
using Steamworks;

namespace CoopSteam;

/// <summary>A one-line check of the Steam side (bitness and Steamworks build included), for the dev bridge: <c>CoopSteam.SteamProbe.Run()</c>.</summary>
public static class SteamProbe
{
    public static string Run()
    {
        string build = $"{SteamLibrary.Bits}-bit game, Steamworks.NET calls {SteamLibrary.Calls() ?? "(unknown)"}";
        string why = SteamRuntime.Ensure();
        if (why != null)
            return "Steam is not usable (" + build + "): " + why;
        SteamRuntime.Pump();
        var relay = SteamRuntime.RelayStatus(out string detail);
        return $"Steam init OK | {build} | SteamID {SteamUser.GetSteamID().m_SteamID} | persona \"{SteamFriends.GetPersonaName()}\""
            + $" | app {SteamUtils.GetAppID().m_AppId} | overlay {(SteamUtils.IsOverlayEnabled() ? "on" : "off")}"
            + $" | relay {SteamRuntime.Describe(relay)}{(string.IsNullOrEmpty(detail) ? "" : " (" + detail + ")")}";
    }
}
