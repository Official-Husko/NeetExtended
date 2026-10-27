using System;
using System.Collections.Generic;
using Steamworks;

namespace CoopSteam.Native;

/// <summary>
/// When Steamworks.NET can't set up its interfaces, finds which Steam interface versions the Steam bridge
/// (Proton's lsteamclient) doesn't offer. The versions are the ones Steamworks.NET 2024.8.0 asks for.
/// </summary>
internal static class InterfaceCheck
{
    /// <summary>The versions that came back empty, or an empty list if none did (or nothing could be asked).</summary>
    public static List<string> FindMissing()
    {
        var missing = new List<string>();
        try
        {
            var user = SteamAPI.GetHSteamUser();
            var pipe = SteamAPI.GetHSteamPipe();
            Ask(missing, "SteamUser023", SteamClient.GetISteamUser(user, pipe, "SteamUser023"));
            Ask(missing, "SteamFriends017", SteamClient.GetISteamFriends(user, pipe, "SteamFriends017"));
            Ask(missing, "SteamUtils010", SteamClient.GetISteamUtils(pipe, "SteamUtils010"));
            Ask(missing, "SteamMatchMaking009", SteamClient.GetISteamMatchmaking(user, pipe, "SteamMatchMaking009"));
            Ask(missing, "SteamMatchMakingServers002", SteamClient.GetISteamMatchmakingServers(user, pipe, "SteamMatchMakingServers002"));
            Ask(missing, "STEAMUSERSTATS_INTERFACE_VERSION012", SteamClient.GetISteamUserStats(user, pipe, "STEAMUSERSTATS_INTERFACE_VERSION012"));
            Ask(missing, "STEAMAPPS_INTERFACE_VERSION008", SteamClient.GetISteamApps(user, pipe, "STEAMAPPS_INTERFACE_VERSION008"));
            Ask(missing, "SteamNetworking006", SteamClient.GetISteamNetworking(user, pipe, "SteamNetworking006"));
            Ask(missing, "STEAMREMOTESTORAGE_INTERFACE_VERSION016", SteamClient.GetISteamRemoteStorage(user, pipe, "STEAMREMOTESTORAGE_INTERFACE_VERSION016"));
            Ask(missing, "STEAMSCREENSHOTS_INTERFACE_VERSION003", SteamClient.GetISteamScreenshots(user, pipe, "STEAMSCREENSHOTS_INTERFACE_VERSION003"));
            Ask(missing, "SteamMatchGameSearch001", SteamClient.GetISteamGameSearch(user, pipe, "SteamMatchGameSearch001"));
            Ask(missing, "STEAMHTTP_INTERFACE_VERSION003", SteamClient.GetISteamHTTP(user, pipe, "STEAMHTTP_INTERFACE_VERSION003"));
            Ask(missing, "STEAMUGC_INTERFACE_VERSION020", SteamClient.GetISteamUGC(user, pipe, "STEAMUGC_INTERFACE_VERSION020"));
            Ask(missing, "STEAMMUSIC_INTERFACE_VERSION001", SteamClient.GetISteamMusic(user, pipe, "STEAMMUSIC_INTERFACE_VERSION001"));
            Ask(missing, "STEAMMUSICREMOTE_INTERFACE_VERSION001", SteamClient.GetISteamMusicRemote(user, pipe, "STEAMMUSICREMOTE_INTERFACE_VERSION001"));
            Ask(missing, "STEAMHTMLSURFACE_INTERFACE_VERSION_005", SteamClient.GetISteamHTMLSurface(user, pipe, "STEAMHTMLSURFACE_INTERFACE_VERSION_005"));
            Ask(missing, "STEAMINVENTORY_INTERFACE_V003", SteamClient.GetISteamInventory(user, pipe, "STEAMINVENTORY_INTERFACE_V003"));
            Ask(missing, "STEAMVIDEO_INTERFACE_V007", SteamClient.GetISteamVideo(user, pipe, "STEAMVIDEO_INTERFACE_V007"));
            Ask(missing, "STEAMPARENTALSETTINGS_INTERFACE_VERSION001", SteamClient.GetISteamParentalSettings(user, pipe, "STEAMPARENTALSETTINGS_INTERFACE_VERSION001"));
            Ask(missing, "SteamInput006", SteamClient.GetISteamInput(user, pipe, "SteamInput006"));
            Ask(missing, "SteamParties002", SteamClient.GetISteamParties(user, pipe, "SteamParties002"));
            Ask(missing, "STEAMREMOTEPLAY_INTERFACE_VERSION002", SteamClient.GetISteamRemotePlay(user, pipe, "STEAMREMOTEPLAY_INTERFACE_VERSION002"));
        }
        catch (Exception e)
        {
            CoopSteamMod.Log?.Warn("Checking Steam's interfaces failed: " + e.Message);
        }
        return missing;
    }

    private static void Ask(List<string> missing, string version, IntPtr pointer)
    {
        if (pointer == IntPtr.Zero)
            missing.Add(version);
    }
}
