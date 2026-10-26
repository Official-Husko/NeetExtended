using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace OpenSaves.Writing;

/// <summary>Deletes all but the newest saves. The game writes a new file every save and never deletes one.</summary>
internal static class SaveCleanup
{
    public static void Prune()
    {
        var mod = OpenSavesMod.Instance;
        int keep = Math.Max(3, mod?.Config.Value.KeepSaves ?? 25);
        try
        {
            var old = new DirectoryInfo(Application.persistentDataPath).GetFiles("neetSave_*")
                .OrderByDescending(f => f.LastWriteTime)
                .Skip(keep)
                .ToList();
            foreach (var file in old)
                file.Delete();
        }
        catch (IOException e)
        {
            mod?.Log.Warn("Couldn't tidy up old saves: " + e.Message);
        }
    }
}
