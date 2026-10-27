using System.IO.Compression;
using System.Security.Cryptography;
using NeetTool.Cli;

namespace NeetTool.Game;

/// <summary>
/// Valve's steam_api.dll (for the 32-bit player) and steam_api64.dll (for the 64-bit one), which the
/// Co-op over Steam mod needs next to itself. Both are fetched, so switching the game's player later
/// doesn't leave Steam without its file. They're Valve's, not ours to ship, so they're downloaded on
/// request from the Steamworks.NET release page (the same release as the Steamworks.NET.dll builds the
/// mod carries, so they always match) and checked against pinned hashes. They're kept in the tool's
/// cache folder. The release is 2024.8.0 (Steamworks SDK 1.60) on purpose: the Steam bridge of older
/// Proton builds (GE-Proton 10) doesn't offer the interface versions SDK 1.64 asks for.
/// </summary>
public static class SteamApiSource
{
    public const string Version = "2024.8.0";
    private const string Url =
        "https://github.com/rlabrecque/Steamworks.NET/releases/download/2024.8.0/Steamworks.NET-Standalone_2024.8.0.zip";
    private const long PackageSize = 892_008;
    private const string PackageSha256 = "08094d1372c6d793d6b5b87c59f1362a10fe8580acb1db6bfaf081a511f6758c";

    /// <summary>The DLLs, each with where it sits in the zip and its SHA-256.</summary>
    public static readonly SteamApiFile[] Files =
    {
        new("steam_api.dll", "Windows-x86/steam_api.dll", "8d36de57cc6436f4e82ee672023f17a7f83a7a55af558582c2c139f83fb33ed0"),
        new("steam_api64.dll", "Windows-x64/steam_api64.dll", "1add7f151fa644870a735ae86e68d1f019f296130d8e7c0a7ed3ecc7482dccbc"),
    };

    public static string CacheDir => Path.Combine(ToolCache.Dir, $"steamworks-{Version}");

    /// <summary>The cache folder with every DLL in it (the download happens first if one is missing).</summary>
    public static async Task<string> GetAsync(CancellationToken ct = default)
    {
        if (Files.All(IsCached))
            return CacheDir;

        Out.Info($"  Downloading Steamworks.NET {Version} (about {PackageSize / 1024} KB) from github.com...");
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        byte[] package = await http.GetByteArrayAsync(Url, ct);
        if (package.Length != PackageSize || Hash(package) != PackageSha256)
            throw new InvalidOperationException("The downloaded Steamworks.NET package isn't the expected file (checksum mismatch). Nothing was installed; try again.");

        Directory.CreateDirectory(CacheDir);
        using var zip = new ZipArchive(new MemoryStream(package), ZipArchiveMode.Read);
        foreach (var file in Files)
        {
            var entry = zip.GetEntry(file.ZipPath) ?? throw new InvalidDataException($"The package has no {file.ZipPath}.");
            using var stream = entry.Open();
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, ct);
            byte[] dll = buffer.ToArray();
            if (Hash(dll) != file.Sha256)
                throw new InvalidDataException($"{file.Name} in the package isn't the expected file (checksum mismatch).");

            string target = Path.Combine(CacheDir, file.Name);
            string partial = target + ".partial";
            await File.WriteAllBytesAsync(partial, dll, ct);
            File.Move(partial, target, overwrite: true);
        }
        return CacheDir;
    }

    private static bool IsCached(SteamApiFile file)
    {
        string path = Path.Combine(CacheDir, file.Name);
        return File.Exists(path) && Hash(File.ReadAllBytes(path)) == file.Sha256;
    }

    private static string Hash(byte[] data) => Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
}

/// <summary>One of Valve's Steam API DLLs: its file name, its path in the Steamworks.NET zip and its SHA-256.</summary>
public sealed record SteamApiFile(string Name, string ZipPath, string Sha256);
