using System;
using Coop.Join;
using Coop.Menu;
using Coop.Net;
using Coop.Session;
using NeetLoader;
using NeetLoader.Config;
using NeetLoader.Logging;
using NeetLoader.Runtime;
using NeetModKit.Events;
using NeetModKit.Menu;
using NeetModKit.Settings;

namespace Coop;

/// <summary>
/// Co-op: the host's house shared over the network. This class wires the parts together and keeps a
/// running session alive across hot reloads (the connection is parked and picked up again).
/// </summary>
public sealed class CoopMod : NeetMod
{
    internal static CoopMod Instance { get; private set; }
    internal static new ModLogger Log { get; private set; }
    internal static ConfigFile<CoopConfig> Config { get; private set; }

    internal static string Version => Instance?.Info.Manifest.Version ?? "0.0.0";

    internal static string PlayerName
    {
        get
        {
            string name = Config?.Value.PlayerName?.Trim();
            return string.IsNullOrEmpty(name) ? "Player" : name;
        }
    }

    public override void OnLoad()
    {
        Instance = this;
        Log = base.Log;
        Config = LoadConfig<CoopConfig>();
        if (string.IsNullOrEmpty(Config.Value.PlayerId))
            Config.Value.PlayerId = Guid.NewGuid().ToString("N");
        Config.Save();
        ModSettings.Register(this, Config, "Co-op");

        Transports.Register(this, new TransportOption
        {
            Id = Transports.DirectId,
            Name = "Direct",
            Create = LiteTransport.Resume,
            Handles = _ => true,
        });
        // First, so the parts below see a carried-over session as they start.
        SessionResume.Begin(this);

        RemoteClock.Init();
        Handshake.Init();
        ScenePolicy.Init(this);
        JoinLoader.Init(this);
        SnapshotSender.Init();
        ModMenu.AddPage(this, new CoopPage());
    }

    // After every part (and mods that add transports, like Steam) has started and is listening.
    public override void OnAllLoaded() => SessionResume.Finish();

    public override void OnUpdate()
    {
        CoopSession.Transport?.Poll();
        CoopLoop.RunUpdate();
    }

    public override void OnLateUpdate() => CoopLoop.RunLateUpdate();

    public override void OnFixedUpdate() => CoopLoop.RunFixedUpdate();

    public override void OnUnload()
    {
        CoopLoop.RunUnloading();
        var transport = CoopSession.Transport;
        if (CoopSession.IsActive && transport != null && transport.IsRunning)
        {
            SessionMemory.Park();
            // A client left in the host's house with no co-op running can't stay there.
            transport.Park(CoopSession.IsClient ? BackToMenu : null);
        }
        else
        {
            transport?.Stop();
        }
        Instance = null;
    }

    public override void OnQuit() => CoopSession.Leave("The game was closed.");

    // Runs on a timer thread, after this copy of the mod is gone.
    private static void BackToMenu() => MainThread.Post(() =>
    {
        SceneLoadingData.sceneNameToLoad = "MainMenu";
        UnityEngine.SceneManagement.SceneManager.LoadScene("Loading");
    });
}
