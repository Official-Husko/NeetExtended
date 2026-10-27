using System;

namespace CoopSteam.Transport;

/// <summary>Puts the pieces of one big reliable message back together. The buffer is reused.</summary>
internal sealed class ChunkAssembler
{
    // Nobody sends more than a join snapshot; anything bigger is a broken or hostile peer.
    public const int MaxLength = 32 * 1024 * 1024;

    public byte[] Buffer = new byte[16 * 1024];
    public int Length;

    /// <summary>False when the message would grow past <see cref="MaxLength"/>.</summary>
    public bool Append(byte[] source, int offset, int count, bool first)
    {
        if (first)
            Length = 0;
        if (Length + count > MaxLength)
            return false;
        if (Length + count > Buffer.Length)
            Array.Resize(ref Buffer, Math.Max(Buffer.Length * 2, Length + count));
        System.Buffer.BlockCopy(source, offset, Buffer, Length, count);
        Length += count;
        return true;
    }

    public void Reset() => Length = 0;
}
