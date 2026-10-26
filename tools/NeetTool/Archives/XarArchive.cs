using System.Buffers.Binary;
using System.IO.Compression;
using System.Xml.Linq;

namespace NeetTool.Archives;

/// <summary>
/// Reads files out of a xar archive, the container macOS .pkg installers use: a binary header, a
/// zlib-compressed XML table of contents, then the file data ("heap").
/// </summary>
public sealed class XarArchive : IDisposable
{
    private const uint Magic = 0x78617221; // "xar!"

    private readonly Stream _stream;
    private readonly long _heapStart;
    private readonly XElement _toc;

    public XarArchive(Stream stream)
    {
        _stream = stream;
        Span<byte> header = stackalloc byte[28];
        _stream.ReadExactly(header);
        if (BinaryPrimitives.ReadUInt32BigEndian(header) != Magic)
            throw new InvalidDataException("Not a xar archive.");
        int headerSize = BinaryPrimitives.ReadUInt16BigEndian(header[4..]);
        long tocCompressed = (long)BinaryPrimitives.ReadUInt64BigEndian(header[8..]);

        _stream.Position = headerSize;
        var compressed = new byte[tocCompressed];
        _stream.ReadExactly(compressed);
        using var toc = new ZLibStream(new MemoryStream(compressed), CompressionMode.Decompress);
        _toc = XDocument.Load(toc).Root!.Element("toc")!;
        _heapStart = headerSize + tocCompressed;
    }

    /// <summary>The data of a file by its path inside the archive ("TargetSupport.pkg.tmp/Payload").</summary>
    public Stream Open(string path)
    {
        var entry = Find(_toc, path.Split('/')) ?? throw new FileNotFoundException($"{path} isn't in the archive.");
        var data = entry.Element("data") ?? throw new InvalidDataException($"{path} has no data.");
        long offset = long.Parse(data.Element("offset")!.Value);
        long length = long.Parse(data.Element("length")!.Value);
        string encoding = data.Element("encoding")?.Attribute("style")?.Value ?? "application/octet-stream";

        _stream.Position = _heapStart + offset;
        var slice = new SubStream(_stream, length);
        return encoding switch
        {
            "application/octet-stream" => slice,
            "application/x-gzip" => new ZLibStream(slice, CompressionMode.Decompress),
            _ => throw new NotSupportedException($"{path} is stored as {encoding}, which isn't supported."),
        };
    }

    private static XElement? Find(XElement parent, string[] parts, int index = 0)
    {
        foreach (var file in parent.Elements("file"))
        {
            if (file.Element("name")?.Value != parts[index])
                continue;
            return index == parts.Length - 1 ? file : Find(file, parts, index + 1);
        }
        return null;
    }

    public void Dispose() => _stream.Dispose();
}
