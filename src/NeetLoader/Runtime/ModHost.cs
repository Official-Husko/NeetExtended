using System;
using NeetLoader.Logging;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NeetLoader.Runtime;

/// <summary>Hidden, persistent GameObject that forwards Unity callbacks to mods.</summary>
internal sealed class ModHost : MonoBehaviour
{
    private const float LogFlushInterval = 1f;

    private static readonly Action<NeetMod> UpdateCall = m => m.OnUpdate();
    private static readonly Action<NeetMod> LateUpdateCall = m => m.OnLateUpdate();
    private static readonly Action<NeetMod> FixedUpdateCall = m => m.OnFixedUpdate();
    private static readonly Action<NeetMod> GuiCall = m => m.OnGUI();
    private static readonly Action<NeetMod> QuitCall = m => m.OnQuit();

    private readonly ModCallbacks _update = new ModCallbacks(nameof(NeetMod.OnUpdate));
    private readonly ModCallbacks _lateUpdate = new ModCallbacks(nameof(NeetMod.OnLateUpdate));
    private readonly ModCallbacks _fixedUpdate = new ModCallbacks(nameof(NeetMod.OnFixedUpdate));
    private readonly ModCallbacks _gui = new ModCallbacks(nameof(NeetMod.OnGUI));
    private readonly ModCallbacks _sceneLoaded = new ModCallbacks(nameof(NeetMod.OnSceneLoaded));
    private readonly ModCallbacks _quit = new ModCallbacks(nameof(NeetMod.OnQuit));

    private ModLogger _log;
    private float _nextFlush;

    /// <summary>The single host instance. Also runs coroutines on behalf of mods.</summary>
    internal static ModHost Instance { get; private set; }

    public static ModHost Create(ModLogger log)
    {
        var go = new GameObject("NeetLoader");
        go.hideFlags = HideFlags.HideAndDontSave;
        DontDestroyOnLoad(go);
        var host = go.AddComponent<ModHost>();
        host._log = log;
        Instance = host;
        SceneManager.sceneLoaded += host.OnSceneLoaded;
        return host;
    }

    public void Register(NeetMod mod)
    {
        _update.AddIfOverridden(mod);
        _lateUpdate.AddIfOverridden(mod);
        _fixedUpdate.AddIfOverridden(mod);
        _gui.AddIfOverridden(mod);
        _sceneLoaded.AddIfOverridden(mod);
        _quit.AddIfOverridden(mod);
    }

    public void Unregister(NeetMod mod)
    {
        _update.Remove(mod);
        _lateUpdate.Remove(mod);
        _fixedUpdate.Remove(mod);
        _gui.Remove(mod);
        _sceneLoaded.Remove(mod);
        _quit.Remove(mod);
    }

    private void Awake() => MainThread.CaptureThread();

    private void Update()
    {
        MainThread.Drain(_log);
        Mods.ModRegistry.Watcher.Tick();
        _update.Invoke(UpdateCall, _log);

        if (Time.unscaledTime >= _nextFlush)
        {
            _nextFlush = Time.unscaledTime + LogFlushInterval;
            LogHub.Flush();
        }
    }

    private void LateUpdate() => _lateUpdate.Invoke(LateUpdateCall, _log);
    private void FixedUpdate() => _fixedUpdate.Invoke(FixedUpdateCall, _log);

    private void OnGUI()
    {
        if (_gui.Count > 0)
            _gui.Invoke(GuiCall, _log);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        _log.Info($"Scene loaded: {scene.name}");
        _sceneLoaded.Invoke(m => m.OnSceneLoaded(scene, mode), _log);
    }

    private void OnApplicationQuit()
    {
        _quit.Invoke(QuitCall, _log);
        _log.Info("Game closing");
        CleanExit.Arm(_log);
        LogHub.Shutdown();
    }
}
