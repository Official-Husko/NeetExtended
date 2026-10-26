using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace NeetModKit.Saves;

/// <summary>
/// Where the game's save code meets the .nsav format. The game's SaveAll and LoadAll are
/// redirected here (see SaveFormatPatches), so the rest of the game's save logic is unchanged.
/// </summary>
public static class SaveStorage
{
    public static bool Compress => KitMod.Config?.Value.CompressSaves ?? true;

    /// <summary>Replaces JToken.ToString in SaveAll: minified when compressing, the game's own indenting otherwise.</summary>
    public static string Serialize(JToken token, Formatting formatting, JsonConverter[] converters) =>
        token.ToString(Compress ? Formatting.None : formatting, converters);

    /// <summary>Replaces File.WriteAllText in SaveAll.</summary>
    public static void WriteGameSave(string path, string json)
    {
        if (Compress)
        {
            path = Path.ChangeExtension(path, SaveCodec.Extension);
            File.WriteAllBytes(path, SaveCodec.Encode(json));
        }
        else
        {
            File.WriteAllText(path, json);
        }
        Prune(Path.GetDirectoryName(path));
    }

    /// <summary>Replaces File.ReadAllText in LoadAll. Reads both .nsav and plain JSON saves.</summary>
    public static string ReadGameSave(string path) => SaveCodec.Decode(File.ReadAllBytes(path));

    /// <summary>Keeps the newest saves and deletes the rest (the game writes a new file every save).</summary>
    public static void Prune(string folder)
    {
        int keep = Math.Max(3, KitMod.Config?.Value.KeepSaves ?? 25);
        try
        {
            var old = new DirectoryInfo(folder).GetFiles("neetSave_*")
                .OrderByDescending(f => f.LastWriteTime)
                .Skip(keep)
                .ToList();
            foreach (var file in old)
                file.Delete();
        }
        catch (IOException e)
        {
            KitMod.Instance?.Log.Warn("Couldn't tidy up old saves: " + e.Message);
        }
    }

    public static string Folder => Application.persistentDataPath;
}
