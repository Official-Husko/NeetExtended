using System;
using System.Collections.Generic;
using NeetModKit.Game;
using UnityEngine;

namespace NeetModKit.Ui.Toolkit;

/// <summary>
/// Takes the game's controls away while a window is open: player input off, cursor free and,
/// if any holder asks for it, the game paused. Several windows can hold it at once (the mod menu
/// and the cheat menu, say); everything comes back when the last one lets go.
/// </summary>
public static class UiInputLock
{
    private sealed class Hold : IDisposable
    {
        public bool Pause;
        public void Dispose() => Release(this);
    }

    private static readonly List<Hold> Holds = new List<Hold>();
    private static float _savedTimeScale = 1f;
    private static bool _paused;
    private static bool _inputDisabled;

    public static bool Active => Holds.Count > 0;

    /// <summary>Locks input until the returned handle is disposed.</summary>
    public static IDisposable Acquire(bool pause)
    {
        var hold = new Hold { Pause = pause };
        Holds.Add(hold);
        Apply();
        return hold;
    }

    /// <summary>The camera script re-locks the cursor every frame; this puts it back while locked.</summary>
    public static void ApplyCursor()
    {
        if (!Active)
            return;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    /// <summary>A new scene brings new input actions; switch those off too while locked.</summary>
    internal static void OnSceneLoaded()
    {
        _inputDisabled = false;
        Apply();
    }

    internal static void ReleaseAll()
    {
        Holds.Clear();
        Apply();
    }

    private static void Release(Hold hold)
    {
        if (Holds.Remove(hold))
            Apply();
    }

    private static void Apply()
    {
        bool active = Holds.Count > 0;
        bool pause = false;
        foreach (var hold in Holds)
            pause |= hold.Pause;

        var input = GameRefs.House != null ? GameRefs.House.inputAsset : null;
        if (active && input != null && input.enabled)
        {
            input.Disable();
            _inputDisabled = true;
        }
        else if (!active && _inputDisabled)
        {
            input?.Enable();
            _inputDisabled = false;
        }

        if (pause && !_paused)
        {
            _savedTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            _paused = true;
        }
        else if (!pause && _paused)
        {
            Time.timeScale = _savedTimeScale;
            _paused = false;
        }

        ApplyCursor();
    }
}
