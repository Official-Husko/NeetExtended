using System;
using Coop.Net;
using Coop.Session;
using LiteNetLib.Utils;
using NeetModKit.Events;
using NeetModKit.Game;
using NeetModKit.Saves;
using UnityEngine.SceneManagement;

namespace Coop.Join;

/// <summary>
/// Client: loads the house the host sent, straight from memory. The player's own saves are never
/// read or written: saving is locked for as long as we're in someone else's house, the framework is
/// told this game shows another game's world, and leaving goes back to the main menu.
/// </summary>
internal static class JoinLoader
{
    private const string House = "NEET_House";

    private static CoopMod _mod;
    private static IDisposable _saveLock;
    private static IDisposable _otherWorld;
    private static bool _loading;

    public static void Init(CoopMod mod)
    {
        _mod = mod;
        Messages.On(MessageIds.Snapshot, OnSnapshot);
        SaveSource.Loaded += OnLoaded;
        CoopLoop.Unloading += () => SaveSource.Loaded -= OnLoaded;
        GameEvents.HouseReady.Subscribe(mod, OnHouseReady);
        CoopSession.Ended += OnEnded;

        // A reload of the mod mid-session drops these with the old copy; take them again.
        if (CoopSession.IsClient)
            ActAsGuest();
        CoopSession.Resumed += () =>
        {
            if (CoopSession.IsClient)
                ActAsGuest();
        };
    }

    private static void OnSnapshot(in Sender from, NetDataReader reader)
    {
        if (CoopSession.State != SessionState.Joining)
            return;
        var data = new byte[reader.GetInt()];
        reader.GetBytes(data, data.Length);
        var snapshot = SaveSnapshot.Decode(data);
        CoopMod.Log.Info($"Got the host's house ({snapshot.Paint.Count} painted surfaces). Loading it.");

        ActAsGuest();
        SaveSource.Use(snapshot);
        _loading = true;
        SceneLoadingData.sceneNameToLoad = House;
        SceneManager.LoadScene("Loading");
    }

    private static void OnLoaded(Exception error)
    {
        if (!_loading)
            return;
        if (error == null)
            return; // Ready once the house has started up (HouseReady).
        _loading = false;
        CoopMod.Log.Error($"Loading the host's house failed: {error}");
        CoopSession.Leave("Loading the host's house failed. Your log has the details.");
    }

    private static void OnHouseReady()
    {
        if (!_loading)
            return;
        _loading = false;
        if (CoopSession.State == SessionState.Joining)
            Handshake.SendReady();
    }

    private static void OnEnded(string reason)
    {
        bool wasInHostHouse = _saveLock != null;
        _loading = false;
        SaveSource.Clear();
        _saveLock?.Dispose();
        _saveLock = null;
        _otherWorld?.Dispose();
        _otherWorld = null;
        // The house here is the host's; it can't be played or saved on its own.
        if (wasInHostHouse && SceneManager.GetActiveScene().name != "MainMenu")
        {
            SceneLoadingData.sceneNameToLoad = "MainMenu";
            SceneManager.LoadScene("Loading");
        }
    }

    // No saving, and nothing that happens on its own (the host's game does that for everyone).
    private static void ActAsGuest()
    {
        _saveLock ??= SaveLock.Acquire(_mod, "in someone else's co-op game");
        _otherWorld ??= WorldRole.ShowOtherWorld(_mod);
    }
}
