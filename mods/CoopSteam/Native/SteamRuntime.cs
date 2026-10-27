using System;
using Steamworks;
using UnityEngine;

namespace CoopSteam.Native;

/// <summary>
/// Starts the Steam API the first time something needs it and keeps its callbacks running. Steamworks.NET
/// is a library that never reloads, so after a hot reload the API is still up and is picked up as is.
/// </summary>
internal static class SteamRuntime
{
    private const int RetryMs = 5000;

    private static string _directory;
    private static bool _ready;
    private static string _failure;
    private static int _retryAt;
    private static int _pumpedFrame = -1;

    public static void Configure(string modDirectory) => _directory = modDirectory;

    /// <summary>Null when Steam is up, else why not. A failure is remembered for a few seconds.</summary>
    public static string Ensure()
    {
        if (_ready)
            return null;
        if (CallbackDispatcher.IsInitialized)
        {
            _ready = true;
            return null;
        }
        if (_failure != null && unchecked(Environment.TickCount - _retryAt) < 0)
            return _failure;

        _failure = TryStart();
        _retryAt = Environment.TickCount + RetryMs;
        _ready = _failure == null;
        return _failure;
    }

    /// <summary>For the menu: null when a game can be hosted or joined over Steam right now.</summary>
    public static string Unavailable()
    {
        string why = Ensure();
        if (why != null)
            return why;
        Pump();
        var relay = RelayStatus(out string detail);
        if (relay == ESteamNetworkingAvailability.k_ESteamNetworkingAvailability_Current)
            return null;
        return "Steam's relay network isn't ready yet (" + Describe(relay) + (string.IsNullOrEmpty(detail) ? "" : ": " + detail) + "). Try again in a few seconds.";
    }

    /// <summary>Your own SteamID64, for others to join with. Null until Steam is up.</summary>
    public static string OwnId() => _ready ? SteamUser.GetSteamID().m_SteamID.ToString() : null;

    /// <summary>Runs Steam's callbacks, at most once per frame.</summary>
    public static void Pump()
    {
        if (!_ready)
            return;
        int frame = Time.frameCount;
        if (frame == _pumpedFrame)
            return;
        _pumpedFrame = frame;
        SteamAPI.RunCallbacks();
    }

    public static ESteamNetworkingAvailability RelayStatus(out string detail)
    {
        var status = SteamNetworkingUtils.GetRelayNetworkStatus(out SteamRelayNetworkStatus_t info);
        detail = info.m_debugMsg;
        return status;
    }

    public static string Describe(ESteamNetworkingAvailability state) => state switch
    {
        ESteamNetworkingAvailability.k_ESteamNetworkingAvailability_Current => "ready",
        ESteamNetworkingAvailability.k_ESteamNetworkingAvailability_Attempting or ESteamNetworkingAvailability.k_ESteamNetworkingAvailability_Waiting => "starting",
        ESteamNetworkingAvailability.k_ESteamNetworkingAvailability_NeverTried => "not started",
        ESteamNetworkingAvailability.k_ESteamNetworkingAvailability_Retrying => "retrying",
        ESteamNetworkingAvailability.k_ESteamNetworkingAvailability_Failed or ESteamNetworkingAvailability.k_ESteamNetworkingAvailability_CannotTry => "failed",
        _ => state.ToString(),
    };

    private static string TryStart()
    {
        string library = SteamLibrary.Load(_directory);
        if (library != null)
            return library;
        try
        {
            var result = SteamAPI.InitEx(out string message);
            if (result != ESteamAPIInitResult.k_ESteamAPIInitResult_OK)
                return Explain(result, message);
            string layout = Packsize.Test() ? MessageLayout.Verify() : "Steam's struct packing doesn't match this build.";
            if (layout != null)
            {
                SteamAPI.Shutdown();
                return layout;
            }
            SteamNetworkingUtils.InitRelayNetworkAccess();
            CoopSteamMod.Log?.Info("Steam is up.");
            return null;
        }
        catch (DllNotFoundException)
        {
            return "Steam's DLL wouldn't load. Run 'neet steam' again.";
        }
        catch (BadImageFormatException)
        {
            return "steam_api.dll is the wrong kind (this game is 32-bit). Run 'neet steam' again.";
        }
        catch (EntryPointNotFoundException e)
        {
            return "steam_api.dll is too old for this mod (" + e.Message + "). Run 'neet steam' again.";
        }
        catch (Exception e)
        {
            CoopSteamMod.Log?.Error("Starting Steam failed", e);
            return "Starting Steam failed: " + e.Message;
        }
    }

    // Steam started, but one of the interfaces Steamworks.NET asks for isn't offered by the Steam bridge.
    private static string BridgeLacks(System.Collections.Generic.List<string> missing)
    {
        string which = missing.Count == 0
            ? "an interface Steam's Proton bridge doesn't support"
            : "Steam interface" + (missing.Count == 1 ? " " : "s ") + string.Join(", ", missing.ToArray()) + ", which Steam's Proton bridge doesn't support";
        CoopSteamMod.Log?.Warn("Missing from the Steam bridge: " + (missing.Count == 0 ? "(couldn't tell which)" : string.Join(", ", missing.ToArray())));
        return "This game's Proton lacks " + which + ". Try a newer Proton (Proton 11, Experimental or GE-Proton 11).";
    }

    private static string Explain(ESteamAPIInitResult result, string message)
    {
        CoopSteamMod.Log?.Warn($"SteamAPI.InitEx: {result}: {message}");
        if (result == ESteamAPIInitResult.k_ESteamAPIInitResult_FailedGeneric && (message ?? "").Contains("CSteamAPIContext"))
            return BridgeLacks(InterfaceCheck.FindMissing());
        return result switch
        {
            ESteamAPIInitResult.k_ESteamAPIInitResult_NoSteamClient => "Steam isn't running. Start Steam first, then the game.",
            ESteamAPIInitResult.k_ESteamAPIInitResult_VersionMismatch => "Steam is too old or too new for this mod. Update Steam, or run 'neet steam' again.",
            _ => "Steam wouldn't start: " + message,
        };
    }
}
