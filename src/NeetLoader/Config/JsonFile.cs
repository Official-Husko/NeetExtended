using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace NeetLoader.Config;

/// <summary>Small helpers for reading and writing JSON files with the game's Newtonsoft.Json.</summary>
public static class JsonFile
{
    public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
    {
        Formatting = Formatting.Indented,
        NullValueHandling = NullValueHandling.Ignore,
        ObjectCreationHandling = ObjectCreationHandling.Replace,
    };

    public static T Read<T>(string path) where T : new()
    {
        if (!File.Exists(path))
            return new T();
        return JsonConvert.DeserializeObject<T>(File.ReadAllText(path), Settings) ?? new T();
    }

    public static bool TryRead<T>(string path, out T value, out Exception error)
    {
        try
        {
            value = JsonConvert.DeserializeObject<T>(File.ReadAllText(path), Settings);
            error = null;
            return value != null;
        }
        catch (Exception e)
        {
            value = default;
            error = e;
            return false;
        }
    }

    /// <summary>Writes through a temp file so a crash mid-write can't leave a half-written file.</summary>
    public static void Write<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        string tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonConvert.SerializeObject(value, Settings), new UTF8Encoding(false));
        if (File.Exists(path))
            File.Delete(path);
        File.Move(tmp, path);
    }
}
