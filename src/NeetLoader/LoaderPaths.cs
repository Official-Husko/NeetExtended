using System.IO;
using UnityEngine;

namespace NeetLoader;

/// <summary>Folder layout used by the loader. All paths are absolute.</summary>
public static class LoaderPaths
{
    /// <summary>The game's install folder (the one containing NEET.exe).</summary>
    public static string GameDir { get; private set; }

    /// <summary>&lt;game&gt;/Mods, one sub-folder per mod.</summary>
    public static string ModsDir { get; private set; }

    /// <summary>&lt;game&gt;/NeetLoader, loader settings and logs.</summary>
    public static string DataDir { get; private set; }

    public static string LogsDir { get; private set; }

    internal static void Init()
    {
        GameDir = Path.GetDirectoryName(Application.dataPath);
        ModsDir = Path.Combine(GameDir, "Mods");
        DataDir = Path.Combine(GameDir, "NeetLoader");
        LogsDir = Path.Combine(DataDir, "logs");

        Directory.CreateDirectory(ModsDir);
        Directory.CreateDirectory(LogsDir);
    }
}
