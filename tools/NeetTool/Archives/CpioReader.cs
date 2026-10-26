using System.Text;

namespace NeetTool.Archives;

/// <summary>
/// Reads a cpio archive in the portable ASCII format (magic "070707"), the format of a .pkg's
/// Payload once it's ungzipped. Entries come in order; each one's data has to be read or skipped
/// before moving to the next.
/// </summary>
public sealed class CpioReader
{
    private const int HeaderSize = 76;
    private const string Trailer = "TRAILER!!!";

    private readonly Stream _stream;
    private readonly byte[] _skipBuffer = new byte[81920];
    private long _remaining;

    public CpioReader(Stream stream)
    {
        _stream = stream;
    }

    public sealed record Entry(string Path, int Mode, long Size)
    {
        public bool IsDirectory => (Mode & 0xF000) == 0x4000;
        public bool IsFile => (Mode & 0xF000) == 0x8000;
    }

    /// <summary>The next entry, or null at the end of the archive.</summary>
    public Entry? Next()
    {
        Skip(_remaining);
        var header = new byte[HeaderSize];
        _stream.ReadExactly(header);
        string text = Encoding.ASCII.GetString(header);
        if (!text.StartsWith("070707", StringComparison.Ordinal))
            throw new InvalidDataException("Not a portable ASCII cpio archive.");

        int mode = Convert.ToInt32(text.Substring(18, 6), 8);
        int nameSize = Convert.ToInt32(text.Substring(59, 6), 8);
        long size = Convert.ToInt64(text.Substring(65, 11), 8);

        var name = new byte[nameSize];
        _stream.ReadExactly(name);
        string path = Encoding.UTF8.GetString(name, 0, nameSize - 1); // without the trailing NUL
        if (path == Trailer)
            return null;
        _remaining = size;
        return new Entry(path, mode, size);
    }

    /// <summary>Copies the current entry's data to <paramref name="destination"/>.</summary>
    public void CopyTo(Stream destination)
    {
        while (_remaining > 0)
        {
            int read = _stream.Read(_skipBuffer, 0, (int)Math.Min(_skipBuffer.Length, _remaining));
            if (read == 0)
                throw new EndOfStreamException("The archive ends in the middle of a file.");
            destination.Write(_skipBuffer, 0, read);
            _remaining -= read;
        }
    }

    private void Skip(long count)
    {
        while (count > 0)
        {
            int read = _stream.Read(_skipBuffer, 0, (int)Math.Min(_skipBuffer.Length, count));
            if (read == 0)
                throw new EndOfStreamException("The archive ends in the middle of a file.");
            count -= read;
        }
        _remaining = 0;
    }
}
