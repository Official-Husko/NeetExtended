using System.IO;
using OpenSaves.Format;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OpenSaves.Writing;

/// <summary>The replacements SaveAll calls while compressed saves are on.</summary>
public static class SaveWriter
{
    /// <summary>Replaces JToken.ToString(formatting, converters): indentation is wasted space here.</summary>
    public static string Serialize(JToken token, Formatting formatting, JsonConverter[] converters) =>
        token.ToString(Formatting.None, converters);

    /// <summary>Replaces File.WriteAllText. Keeps the game's "neetSave_" name so the game still finds it.</summary>
    public static void WriteSave(string path, string json) =>
        File.WriteAllBytes(Path.ChangeExtension(path, NsavCodec.Extension), NsavCodec.Encode(json));
}
