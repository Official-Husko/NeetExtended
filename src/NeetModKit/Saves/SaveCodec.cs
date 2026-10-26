#nullable disable
using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace NeetModKit.Saves;

/// <summary>
/// The NEET save container (.nsav). Open and simple: a 20-byte header followed by the save's JSON,
/// usually Deflate-compressed. Old saves (plain JSON files) are read as they are.
///
/// Header (little-endian):
///   0  8 bytes  ASCII "NEETSAVE"
///   8  1 byte   format version (1)
///   9  1 byte   codec: 0 = plain UTF-8 JSON, 1 = raw Deflate (RFC 1951) of UTF-8 JSON
///  10  2 bytes  reserved, 0
///  12  4 bytes  length of the JSON in bytes (uncompressed)
///  16  4 bytes  CRC-32 (IEEE) of the uncompressed JSON bytes
///  20  ...      payload
///
/// This file is shared between the game-side framework and the neet command-line tool.
/// </summary>
public static class SaveCodec
{
    public const string Extension = ".nsav";
    public const byte FormatVersion = 1;
    public const int HeaderSize = 20;

    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("NEETSAVE");

    public enum Codec : byte
    {
        Plain = 0,
        Deflate = 1,
    }

    public sealed class Info
    {
        public bool IsContainer;
        public byte Version;
        public Codec Codec;
        public int JsonLength;
        public int FileLength;
        public uint Crc;
    }

    public static bool IsContainer(byte[] data)
    {
        if (data == null || data.Length < HeaderSize)
            return false;
        for (int i = 0; i < Magic.Length; i++)
        {
            if (data[i] != Magic[i])
                return false;
        }
        return true;
    }

    public static byte[] Encode(string json, Codec codec = Codec.Deflate)
    {
        byte[] raw = Encoding.UTF8.GetBytes(json);
        byte[] payload = codec == Codec.Deflate ? Deflate(raw) : raw;

        var output = new byte[HeaderSize + payload.Length];
        Buffer.BlockCopy(Magic, 0, output, 0, Magic.Length);
        output[8] = FormatVersion;
        output[9] = (byte)codec;
        WriteInt(output, 12, raw.Length);
        WriteInt(output, 16, (int)Crc32.Compute(raw));
        Buffer.BlockCopy(payload, 0, output, HeaderSize, payload.Length);
        return output;
    }

    /// <summary>Returns the save's JSON, from a container or from a plain JSON file.</summary>
    public static string Decode(byte[] data)
    {
        if (!IsContainer(data))
            return Encoding.UTF8.GetString(data);

        var info = Inspect(data);
        if (info.Version > FormatVersion)
            throw new InvalidDataException($"Save format {info.Version} is newer than this version understands ({FormatVersion}).");

        byte[] raw = info.Codec switch
        {
            Codec.Plain => Slice(data, HeaderSize, data.Length - HeaderSize),
            Codec.Deflate => Inflate(data, HeaderSize, info.JsonLength),
            _ => throw new InvalidDataException($"Unknown save codec {(byte)info.Codec}."),
        };

        if (raw.Length != info.JsonLength || Crc32.Compute(raw) != info.Crc)
            throw new InvalidDataException("The save file is damaged (length or checksum mismatch).");
        return Encoding.UTF8.GetString(raw);
    }

    public static Info Inspect(byte[] data)
    {
        if (!IsContainer(data))
            return new Info { IsContainer = false, JsonLength = data.Length, FileLength = data.Length };
        return new Info
        {
            IsContainer = true,
            Version = data[8],
            Codec = (Codec)data[9],
            JsonLength = ReadInt(data, 12),
            Crc = (uint)ReadInt(data, 16),
            FileLength = data.Length,
        };
    }

    private static byte[] Deflate(byte[] raw)
    {
        using var output = new MemoryStream(raw.Length / 8 + 64);
        using (var deflate = new DeflateStream(output, CompressionLevel.Optimal, leaveOpen: true))
            deflate.Write(raw, 0, raw.Length);
        return output.ToArray();
    }

    private static byte[] Inflate(byte[] data, int offset, int expectedLength)
    {
        using var input = new MemoryStream(data, offset, data.Length - offset);
        using var deflate = new DeflateStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream(expectedLength);
        deflate.CopyTo(output);
        return output.ToArray();
    }

    private static byte[] Slice(byte[] data, int offset, int length)
    {
        var slice = new byte[length];
        Buffer.BlockCopy(data, offset, slice, 0, length);
        return slice;
    }

    private static void WriteInt(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)value;
        buffer[offset + 1] = (byte)(value >> 8);
        buffer[offset + 2] = (byte)(value >> 16);
        buffer[offset + 3] = (byte)(value >> 24);
    }

    private static int ReadInt(byte[] buffer, int offset) =>
        buffer[offset] | buffer[offset + 1] << 8 | buffer[offset + 2] << 16 | buffer[offset + 3] << 24;

    /// <summary>Standard CRC-32 (IEEE 802.3, same as zip/PNG).</summary>
    private static class Crc32
    {
        private static readonly uint[] Table = BuildTable();

        public static uint Compute(byte[] data)
        {
            uint crc = 0xFFFFFFFFu;
            for (int i = 0; i < data.Length; i++)
                crc = Table[(crc ^ data[i]) & 0xFF] ^ (crc >> 8);
            return ~crc;
        }

        private static uint[] BuildTable()
        {
            var table = new uint[256];
            for (uint n = 0; n < 256; n++)
            {
                uint c = n;
                for (int k = 0; k < 8; k++)
                    c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                table[n] = c;
            }
            return table;
        }
    }
}
