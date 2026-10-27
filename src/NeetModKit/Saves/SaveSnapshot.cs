using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NeetModKit.Saves;

/// <summary>
/// A whole save held in memory: what the game writes as a neetSave file plus the paint pictures it
/// writes next to it. Used to hand a house from one game to another without touching disk.
/// </summary>
public sealed class SaveSnapshot
{
    private const byte Version = 1;

    private string _json;

    /// <summary>The object SaveLoadHandler.AllSaveDataToJSon returns. Change it before <see cref="Encode"/> or <see cref="SaveSource.Use"/>.</summary>
    public JObject Save;

    /// <summary>PaintTarget.saveId to PNG bytes.</summary>
    public Dictionary<int, byte[]> Paint = new Dictionary<int, byte[]>();

    /// <summary>The save as text, made once (what LoadAll parses).</summary>
    internal string JsonText => _json ??= Save.ToString(Formatting.None);

    /// <summary>One blob: GZip of the minified JSON and the paint pictures.</summary>
    public byte[] Encode()
    {
        byte[] json = Encoding.UTF8.GetBytes(Save.ToString(Formatting.None));
        using (var output = new MemoryStream())
        {
            using (var zip = new GZipStream(output, CompressionMode.Compress, true))
            using (var writer = new BinaryWriter(zip))
            {
                writer.Write(Version);
                writer.Write(json.Length);
                writer.Write(json);
                writer.Write(Paint.Count);
                foreach (var pair in Paint)
                {
                    writer.Write(pair.Key);
                    writer.Write(pair.Value.Length);
                    writer.Write(pair.Value);
                }
            }
            return output.ToArray();
        }
    }

    public static SaveSnapshot Decode(byte[] data)
    {
        using (var input = new MemoryStream(data))
        using (var zip = new GZipStream(input, CompressionMode.Decompress))
        using (var reader = new BinaryReader(zip))
        {
            if (reader.ReadByte() != Version)
                throw new InvalidDataException("This save snapshot is from a different version.");

            string json = Encoding.UTF8.GetString(ReadBlock(reader));
            var snapshot = new SaveSnapshot { Save = JObject.Parse(json) };
            int count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
                snapshot.Paint[reader.ReadInt32()] = ReadBlock(reader);
            return snapshot;
        }
    }

    private static byte[] ReadBlock(BinaryReader reader)
    {
        int length = reader.ReadInt32();
        byte[] bytes = reader.ReadBytes(length);
        if (length < 0 || bytes.Length != length)
            throw new InvalidDataException("The save snapshot is cut short.");
        return bytes;
    }
}
