using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Steamworks;

namespace CoopSteam.Native;

/// <summary>
/// Loads Valve's Steam DLL from the mod folder by its full path: steam_api64.dll on the 64-bit player,
/// steam_api.dll on the 32-bit one. Steamworks.NET asks for it by name; once it's loaded, that lookup
/// finds it, so the game folder stays untouched.
/// </summary>
internal static class SteamLibrary
{
    public static readonly int Bits = IntPtr.Size * 8;
    public static readonly string FileName = IntPtr.Size == 8 ? "steam_api64.dll" : "steam_api.dll";

    // ERROR_BAD_EXE_FORMAT: the DLL is built for the other bitness.
    private const int BadExeFormat = 193;

    private static IntPtr _handle;

    [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadLibraryW(string path);

    /// <summary>Null when the library is loaded, otherwise what to tell the player.</summary>
    public static string Load(string modDirectory)
    {
        if (_handle != IntPtr.Zero)
            return null;
        string mismatch = CheckBuild();
        if (mismatch != null)
            return mismatch;
        string path = Path.Combine(modDirectory ?? "", FileName);
        if (!File.Exists(path))
            return $"Steam support isn't installed ({FileName} is missing). Close the game and run: neet steam";
        _handle = LoadLibraryW(path);
        if (_handle != IntPtr.Zero)
            return null;
        int error = Marshal.GetLastWin32Error();
        return error == BadExeFormat
            ? $"{FileName} doesn't fit this {Bits}-bit game. Close the game and run 'neet steam' again."
            : $"Couldn't load {FileName} (Windows error {error}). Run 'neet steam' again to replace it.";
    }

    /// <summary>
    /// The Steam DLL the Steamworks.NET in use calls: "steam_api64" for its 64-bit build (in the mod
    /// folder), "steam_api" for the 32-bit one (in x86/, which the loader picks on the 32-bit player).
    /// </summary>
    public static string Calls()
    {
        try
        {
            var field = typeof(SteamAPI).Assembly.GetType("Steamworks.NativeMethods")
                ?.GetField("NativeLibraryName", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            return field?.GetRawConstantValue() as string;
        }
        catch (Exception e)
        {
            CoopSteamMod.Log?.Warn("Couldn't tell which Steam DLL Steamworks.NET calls: " + e.Message);
            return null;
        }
    }

    // An older loader doesn't know about x86/ and hands the 32-bit game the 64-bit Steamworks.NET.
    private static string CheckBuild()
    {
        string wanted = Path.GetFileNameWithoutExtension(FileName);
        string calls = Calls();
        if (calls == null || string.Equals(calls, wanted, StringComparison.OrdinalIgnoreCase))
            return null;
        CoopSteamMod.Log?.Warn($"Steamworks.NET from {typeof(SteamAPI).Assembly.Location} calls {calls}; this {Bits}-bit game needs {wanted}.");
        return $"Co-op over Steam got the {(Bits == 64 ? 32 : 64)}-bit Steamworks.NET in this {Bits}-bit game. Install the mods again (that updates the mod loader too).";
    }
}
