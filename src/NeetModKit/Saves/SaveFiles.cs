using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace NeetModKit.Saves;

/// <summary>Finds and reads the save the game is about to load, using the game's own rule (or the snapshot in <see cref="SaveSource"/>).</summary>
public static class SaveFiles
{
    /// <summary>SaveLoadHandler.LoadAll picks the most recently written file starting with "neet".</summary>
    public static FileInfo Newest()
    {
        if (SaveSource.Active)
            return SaveSource.Marker();

        var dir = new DirectoryInfo(Application.persistentDataPath);
        if (!dir.Exists)
            return null;
        return dir.GetFiles("neet*")
            .Where(f => f.Name.Length >= 4)
            .OrderByDescending(f => f.LastWriteTime)
            .FirstOrDefault();
    }

    public static JObject ReadNewest()
    {
        if (SaveSource.Active)
            return SaveSource.Current.Save;

        var file = Newest();
        return file == null ? null : JObject.Parse(SaveFormats.ReadText(file.FullName));
    }
}
