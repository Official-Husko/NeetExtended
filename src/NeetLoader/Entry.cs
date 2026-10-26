using System;
using UnityEngine;

namespace NeetLoader;

/// <summary>
/// Startup hook. Unity calls <see cref="Init"/> before the first scene loads because the installer
/// lists it in NEET_Data/RuntimeInitializeOnLoads.json.
/// </summary>
public static class Entry
{
    private static bool _started;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void Init()
    {
        if (_started)
            return;
        _started = true;

        try
        {
            Loader.Start();
        }
        catch (Exception e)
        {
            // Last resort: the logger itself may be what failed.
            Debug.LogError("[NeetLoader] Failed to start: " + e);
        }
    }
}
