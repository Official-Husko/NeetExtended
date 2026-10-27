using System;
using System.IO;
using System.Runtime.InteropServices;

namespace CoopSteam.Native;

/// <summary>
/// Loads steam_api.dll from the mod folder by its full path. Steamworks.NET asks for "steam_api" by
/// name; once the DLL is loaded, that lookup finds it, so the game folder stays untouched.
/// </summary>
internal static class SteamLibrary
{
    public const string FileName = "steam_api.dll";

    private static IntPtr _handle;

    [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadLibraryW(string path);

    /// <summary>Null when the library is loaded, otherwise what to tell the player.</summary>
    public static string Load(string modDirectory)
    {
        if (_handle != IntPtr.Zero)
            return null;
        string path = Path.Combine(modDirectory ?? "", FileName);
        if (!File.Exists(path))
            return "Steam support isn't installed. Close the game and run: neet steam";
        _handle = LoadLibraryW(path);
        if (_handle == IntPtr.Zero)
            return $"Couldn't load {FileName} (Windows error {Marshal.GetLastWin32Error()}). Run 'neet steam' again to replace it.";
        return null;
    }
}
