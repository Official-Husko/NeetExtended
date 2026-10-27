using System;
using NeetLoader;
using NeetLoader.Runtime;
using NeetLoader.Logging;
using NeetModKit.Game;
using UnityEngine.SceneManagement;

namespace NeetModKit.Events;

/// <summary>
/// Game-level moments mods usually care about. All handlers run on the main thread.
/// </summary>
public static class GameEvents
{
    private static ModLogger _log;

    /// <summary>
    /// The house scene finished loading the save (or starting fresh). Player, computer and
    /// registries are all usable. Fires once per house load.
    /// </summary>
    public static readonly GameEvent HouseReady = new GameEvent(nameof(HouseReady));

    /// <summary>The item registry was (re)built. Register modded items here.</summary>
    public static readonly GameEvent ItemsRegistering = new GameEvent(nameof(ItemsRegistering));

    public static readonly GameEvent BeforeSave = new GameEvent(nameof(BeforeSave));
    public static readonly GameEvent AfterSave = new GameEvent(nameof(AfterSave));
    public static readonly GameEvent BeforeLoad = new GameEvent(nameof(BeforeLoad));
    public static readonly GameEvent AfterLoad = new GameEvent(nameof(AfterLoad));

    /// <summary>Raised with the new scene's name after any scene load.</summary>
    public static readonly GameEvent<string> SceneLoaded = new GameEvent<string>(nameof(SceneLoaded));

    /// <summary>
    /// The house is ready after the player slept (the dream scene came before it). Raised right
    /// after <see cref="HouseReady"/>; a new day in <see cref="Rules.ModFlags.Day"/> has begun.
    /// </summary>
    public static readonly GameEvent WokeUp = new GameEvent(nameof(WokeUp));

    /// <summary>The player ordered something from a computer store (Computer.OrderItem went through).</summary>
    public static readonly GameEvent<StoreOrder> ItemOrdered = new GameEvent<StoreOrder>(nameof(ItemOrdered));

    /// <summary>True between <see cref="HouseReady"/> and the next scene change.</summary>
    public static bool IsHouseReady { get; private set; }

    /// <summary>
    /// Runs <paramref name="action"/> every time the house becomes ready, and right away if it
    /// already is. Use this instead of HouseReady when a mod can be (re)loaded mid-game.
    /// </summary>
    public static void WhenHouseReady(ITracker owner, Action action)
    {
        HouseReady.Subscribe(owner, action);
        if (IsHouseReady)
            Run(action);
    }

    internal static void Init(NeetMod owner)
    {
        _log = owner.Log;
        SceneManager.sceneLoaded += OnSceneLoaded;
        owner.Track(NeetLoader.Runtime.Disposable.Create(() => SceneManager.sceneLoaded -= OnSceneLoaded));

        // Reloaded mid-game: the house finished loading long ago, so start out ready.
        IsHouseReady = GameRefs.InHouse && SaveLoadHandler._ != null;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        IsHouseReady = false;
        // Sleeping in the house loads the dream; the next house load is the next morning.
        if (scene.name == DreamScene)
            _slept = true;
        SceneLoaded.Raise(scene.name, _log);
    }

    private const string DreamScene = "DreamZone";
    private static bool _slept;

    internal static void RaiseHouseReady()
    {
        if (!GameRefs.InHouse)
            return;
        IsHouseReady = true;
        PlayerControl.ApplyFlags(); // Re-apply god mode, frozen needs and speed to the new player object.
        _log.Info("House ready");
        HouseReady.Raise(_log);
        if (_slept)
        {
            _slept = false;
            WokeUp.Raise(_log);
        }
    }

    internal static void RaiseOrder(StoreOrder order) => ItemOrdered.Raise(order, _log);

    internal static void Raise(GameEvent e) => e.Raise(_log);

    private static void Run(Action action)
    {
        try { action(); }
        catch (Exception e) { _log.Error("HouseReady handler threw", e); }
    }
}
