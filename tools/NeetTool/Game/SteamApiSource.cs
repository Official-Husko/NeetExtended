using System.IO.Compression;
using System.Security.Cryptography;
using NeetTool.Cli;

namespace NeetTool.Game;

/// <summary>
/// Valve's steam_api.dll (32-bit Windows), which the Co-op over Steam mod needs next to itself. It's
/// Valve's file, not ours to ship, so it's downloaded on request from the Steamworks.NET release page
/// (the same release as the Steamworks.NET.dll the mod carries, so the two always match) and checked
/// against pinned hashes. It's kept in the tool's cache folder. The release is 2024.8.0 (Steamworks SDK
/// 1.60) on purpose: the Steam bridge of older Proton builds (GE-Proton 10) doesn't offer the interface
/// versions SDK 1.64 asks for.
/// </summary>
public static class SteamApiSource
{
    public const string Version = "2024.8.0";
    public const string FileName = "steam_api.dll";
    private const string Url =
        "https://github.com/rlabrecque/Steamworks.NET/releases/download/2024.8.0/Steamworks.NET-Standalone_2024.8.0.zip";
    private const long PackageSize = 892_008;
    private const string PackageSha256 = "08094d1372c6d793d6b5b87c59f1362a10fe8580acb1db6bfaf081a511f6758c";
    private const string EntryPath = "Windows-x86/steam_api.dll";
    private const string DllSha256 = "8d36de57cc6436f4e82ee672023f17a7f83a7a55af558582c2c139f83fb33ed0";

    public static string CacheDir => Path.Combine(ToolCache.Dir, $"steamworks-{Version}");

    /// <summary>The cached DLL (the download happens first if it isn't there yet).</summary>
    public static async Task<string> GetAsync(CancellationToken ct = default)
    {
        string target = Path.Combine(CacheDir, FileName);
        if (File.Exists(target) && Hash(File.ReadAllBytes(target)) == DllSha256)
            return target;

        Out.Info($"  Downloading Steamworks.NET {Version} (about {PackageSize / 1024} KB) from github.com...");
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        byte[] package = await http.GetByteArrayAsync(Url, ct);
        if (package.Length != PackageSize || Hash(package) != PackageSha256)
            throw new InvalidOperationException("The downloaded Steamworks.NET package isn't the expected file (checksum mismatch). Nothing was installed; try again.");

        using var zip = new ZipArchive(new MemoryStream(package), ZipArchiveMode.Read);
        var entry = zip.GetEntry(EntryPath) ?? throw new InvalidDataException($"The package has no {EntryPath}.");
        using var stream = entry.Open();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, ct);
        byte[] dll = buffer.ToArray();
        if (Hash(dll) != DllSha256)
            throw new InvalidDataException($"{FileName} in the package isn't the expected file (checksum mismatch).");

        Directory.CreateDirectory(CacheDir);
        string partial = target + ".partial";
        await File.WriteAllBytesAsync(partial, dll, ct);
        File.Move(partial, target, overwrite: true);
        return target;
    }

    private static string Hash(byte[] data) => Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
}
