using NeetModKit.Game;
using UnityEngine;

namespace NeetModKit.Menu;

/// <summary>
/// While the menu is open: the player's controls are switched off, the cursor is free and,
/// if the player wants, the game is paused. Everything is restored on close.
/// </summary>
internal static class MenuInput
{
    private static float _savedTimeScale = 1f;
    private static bool _paused;
    private static bool _inputDisabled;

    public static bool CursorFree { get; private set; }

    public static void Capture(bool pause)
    {
        CursorFree = true;
        ApplyCursor();

        var input = GameRefs.House != null ? GameRefs.House.inputAsset : null;
        if (input != null && input.enabled)
        {
            input.Disable();
            _inputDisabled = true;
        }

        if (pause && !_paused)
        {
            _savedTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            _paused = true;
        }
    }

    public static void Release()
    {
        CursorFree = false;

        if (_inputDisabled)
        {
            var input = GameRefs.House != null ? GameRefs.House.inputAsset : null;
            input?.Enable();
            _inputDisabled = false;
        }

        if (_paused)
        {
            Time.timeScale = _savedTimeScale;
            _paused = false;
        }
    }

    /// <summary>Called after the game's camera script decides on the cursor, so the menu wins.</summary>
    public static void ApplyCursor()
    {
        if (!CursorFree)
            return;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }
}
