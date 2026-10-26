using System;
using System.Runtime.InteropServices;
using NeetLoader.Logging;
using UnityEngine;

namespace NeetLoader.Runtime;

/// <summary>
/// The 32-bit player crashes inside Mono's shutdown once Harmony has patched anything, even after
/// every patch is removed again; the unmodded game and the 64-bit player exit cleanly. Everything
/// that matters has run by then (quit handlers, saves), but each exit writes a crash report into
/// Temp/NEET Games/NEET/Crashes. Mono raises AppDomain.ProcessExit just before that point, so on
/// the 32-bit player the process ends there, after saving PlayerPrefs, which Unity would do in the
/// part that's skipped.
/// </summary>
internal static class CleanExit
{
    private static bool _armed;

    private static bool Needed => IntPtr.Size == 4 && Application.platform == RuntimePlatform.WindowsPlayer;

    /// <summary>Call when the game is quitting, after the mods' quit handlers.</summary>
    public static void Arm(ModLogger log)
    {
        if (_armed || !Needed)
            return;
        _armed = true;
        PlayerPrefs.Save();
        AppDomain.CurrentDomain.ProcessExit += (_, _) => TerminateProcess(GetCurrentProcess(), 0);
        log.Info("Will end the process when Mono shuts down (the 32-bit runtime crashes there otherwise)");
    }

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TerminateProcess(IntPtr process, uint exitCode);
}
