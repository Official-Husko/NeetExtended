using System;
using LiteNetLib.Utils;

namespace Coop.Assets;

/// <summary>
/// A file by its content: SHA-256 of the bytes, size and type. The same bytes are the same file in
/// every game, wherever they're kept, so a file is sent once and found again by its hash.
/// </summary>
internal readonly struct AssetId : IEquatable<AssetId>
{
    public const int HashBytes = 32;

    /// <summary>64 lower-case hex digits, or null for "no file".</summary>
    public readonly string Hash;

    /// <summary>Bytes, or -1 when only the content is known (a shared path doesn't say how big the file is).</summary>
    public readonly long Size;

    /// <summary>Lower-case extension with the dot, one of <see cref="FileTypes"/>.</summary>
    public readonly string Ext;

    public AssetId(string hash, long size, string ext)
    {
        Hash = hash;
        Size = size;
        Ext = ext;
    }

    public bool IsValid => Hash != null;

    public bool SizeKnown => Size >= 0;

    /// <summary>The id a shared path names: content and type, size unknown.</summary>
    public static AssetId OfPath(string hash, string ext) => new AssetId(hash, -1, ext);

    /// <summary>Its name in a cache: "hash.ext".</summary>
    public string FileName => Hash + Ext;

    public override string ToString() => !IsValid ? "(none)" : SizeKnown ? $"{Hash.Substring(0, 12)}{Ext} ({FileSizes.Describe(Size)})" : Hash.Substring(0, 12) + Ext;

    /// <summary>Same content and type (the size follows from the content).</summary>
    public bool Equals(AssetId other) => Hash == other.Hash && Ext == other.Ext;
    public override bool Equals(object obj) => obj is AssetId other && Equals(other);
    public override int GetHashCode() => Hash?.GetHashCode() ?? 0;

    public void Write(NetDataWriter writer)
    {
        writer.Put(IsValid);
        if (!IsValid)
            return;
        for (int i = 0; i < HashBytes; i++)
            writer.Put((byte)((Digit(Hash[i * 2]) << 4) | Digit(Hash[i * 2 + 1])));
        writer.Put(Size);
        writer.Put(Ext);
    }

    /// <summary>What another game sent. A type this game doesn't take or a bad size reads as no file.</summary>
    public static AssetId Read(NetDataReader reader)
    {
        if (!reader.GetBool())
            return default;
        var hex = new char[HashBytes * 2];
        for (int i = 0; i < HashBytes; i++)
        {
            byte b = reader.GetByte();
            hex[i * 2] = HexDigit(b >> 4);
            hex[i * 2 + 1] = HexDigit(b & 15);
        }
        long size = reader.GetLong();
        string ext = (reader.GetString(16) ?? "").ToLowerInvariant();
        if (size < -1 || !FileTypes.IsKnown(ext))
            return default;
        return new AssetId(new string(hex), size, ext);
    }

    public static string ToHex(byte[] hash)
    {
        var hex = new char[hash.Length * 2];
        for (int i = 0; i < hash.Length; i++)
        {
            hex[i * 2] = HexDigit(hash[i] >> 4);
            hex[i * 2 + 1] = HexDigit(hash[i] & 15);
        }
        return new string(hex);
    }

    public static bool IsHash(string text)
    {
        if (text == null || text.Length != HashBytes * 2)
            return false;
        foreach (char c in text)
        {
            if (!(c >= '0' && c <= '9' || c >= 'a' && c <= 'f'))
                return false;
        }
        return true;
    }

    private static char HexDigit(int value) => (char)(value < 10 ? '0' + value : 'a' + value - 10);

    private static int Digit(char c) => c <= '9' ? c - '0' : c - 'a' + 10;
}
