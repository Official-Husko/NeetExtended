using System;
using System.Collections.Generic;
using System.IO;

namespace Coop.Assets.Transfer;

/// <summary>
/// One file on its way here, written to "hash.ext.part" in the cache in order. A part left by a hot
/// reload or an earlier session is picked up where it stopped.
/// </summary>
internal sealed class Download
{
    public const byte NoSource = byte.MaxValue;

    public readonly List<Action<string>> Waiters = new List<Action<string>>();

    public AssetId Id;
    public string Label;
    public uint Transfer;

    /// <summary>The player it's asked from, or <see cref="NoSource"/> while nobody has it.</summary>
    public byte Source = NoSource;

    /// <summary>From the first chunk (or the id, when it says); -1 until then.</summary>
    public long Total = -1;

    public long Received;
    public long AckedAt;
    public bool Asked;
    public bool Verifying;
    public int Failures;

    /// <summary>Times the source said nobody has it.</summary>
    public int Misses;
    public float LastHeard;
    public float RetryAt;
    public float StartedAt;
    public long StartedFrom;
    public bool Announced;

    private FileStream _stream;

    public string Key => Id.FileName;

    public string PartPath => FileCache.PartOf(Id);

    /// <summary>Opens the part for appending; <see cref="Received"/> is what's already in it.</summary>
    public void Open()
    {
        if (_stream != null)
            return;
        _stream = new FileStream(PartPath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read, 64 * 1024);
        Received = _stream.Length;
        if (Total >= 0 && Received > Total)
            Truncate();
        _stream.Seek(Received, SeekOrigin.Begin);
        AckedAt = Received;
    }

    public void Write(byte[] data, int offset, int count)
    {
        _stream.Write(data, offset, count);
        Received += count;
    }

    /// <summary>Starts the file over (it was longer than the file, or didn't check out).</summary>
    public void Truncate()
    {
        if (_stream == null)
            Open();
        _stream.SetLength(0);
        Received = 0;
        AckedAt = 0;
    }

    public void Close()
    {
        if (_stream == null)
            return;
        try { _stream.Dispose(); }
        catch (IOException) { }
        _stream = null;
    }

    public void DeletePart()
    {
        Close();
        try { File.Delete(PartPath); }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { }
    }

    public string Describe() => Label + " (" + (Total >= 0 ? FileSizes.Describe(Total) : Id.ToString()) + ")";
}
