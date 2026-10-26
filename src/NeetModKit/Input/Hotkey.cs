using System;
// The game has its own global Key and Keyboard classes, so the Input System ones need aliases.
using InputKey = UnityEngine.InputSystem.Key;
using InputKeyboard = UnityEngine.InputSystem.Keyboard;

namespace NeetModKit.Input;

/// <summary>
/// A keyboard key chosen by name in a settings file ("F1", "Insert", "Home"...). The name is parsed
/// once when set, so checking it every frame is cheap. Unknown names fall back to a default key.
/// </summary>
public sealed class Hotkey
{
    private readonly InputKey _fallback;
    private InputKey _key;
    private string _name;

    public Hotkey(string name, InputKey fallback)
    {
        _fallback = fallback;
        Name = name;
    }

    public string Name
    {
        get => _name;
        set
        {
            _name = value;
            _key = Enum.TryParse(value, true, out InputKey key) && key != InputKey.None ? key : _fallback;
        }
    }

    public bool WasPressed => Pressed(_key);

    public static bool EscapePressed => Pressed(InputKey.Escape);

    private static bool Pressed(InputKey key)
    {
        var keyboard = InputKeyboard.current;
        return keyboard != null && keyboard[key].wasPressedThisFrame;
    }
}
