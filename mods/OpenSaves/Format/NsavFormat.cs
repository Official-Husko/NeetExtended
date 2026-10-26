using NeetModKit.Saves;

namespace OpenSaves.Format;

/// <summary>Teaches the game's loader to read .nsav files.</summary>
internal sealed class NsavFormat : ISaveFormat
{
    public string Name => "Open Saves (.nsav)";

    public bool CanRead(byte[] data) => NsavCodec.IsContainer(data);

    public string Read(byte[] data) => NsavCodec.Decode(data);
}
