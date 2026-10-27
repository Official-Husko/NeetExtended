using System.Collections.Generic;
using System.IO;
using System.Text;
using NeetLoader.Runtime;

namespace NeetModKit.Saves;

/// <summary>A save file format a mod can teach the game to read (the game itself only reads JSON).</summary>
public interface ISaveFormat
{
    string Name { get; }

    /// <summary>True if <paramref name="data"/> is a file in this format.</summary>
    bool CanRead(byte[] data);

    /// <summary>Returns the save's JSON.</summary>
    string Read(byte[] data);
}

/// <summary>
/// The game reads saves through here (a transpiler on LoadAll), so mods can add save formats.
/// Plain JSON always works. A file nobody can read fails with a message naming the problem
/// instead of a JSON parse error deep in LoadAll.
/// </summary>
public static class SaveFormats
{
    private static readonly List<ISaveFormat> Formats = new List<ISaveFormat>();

    public static void Register(ITracker owner, ISaveFormat format)
    {
        Formats.Add(format);
        owner.Track(Disposable.Create(() => Formats.Remove(format)));
    }

    public static string ReadText(string path)
    {
        if (SaveSource.Active)
            return SaveSource.Current.JsonText;

        byte[] data = File.ReadAllBytes(path);
        foreach (var format in Formats)
        {
            if (format.CanRead(data))
                return format.Read(data);
        }

        if (LooksLikeText(data))
            return Encoding.UTF8.GetString(data);
        throw new InvalidDataException(
            $"{Path.GetFileName(path)} isn't a JSON save and no installed mod can read it. If it ends in .nsav, install OpenSaves.");
    }

    private static bool LooksLikeText(byte[] data)
    {
        // JSON saves start with '{' (after an optional BOM or whitespace).
        for (int i = 0; i < data.Length && i < 16; i++)
        {
            byte b = data[i];
            if (b == '{' || b == '[')
                return true;
            if (b != ' ' && b != '\t' && b != '\r' && b != '\n' && b != 0xEF && b != 0xBB && b != 0xBF)
                return false;
        }
        return data.Length == 0;
    }
}
