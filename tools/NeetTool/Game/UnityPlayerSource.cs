using System.Security.Cryptography;
using NeetTool.Archives;
using NeetTool.Cli;

namespace NeetTool.Game;

/// <summary>
/// The 64-bit Windows player of the game's exact Unity version (2022.3.17f1, Mono). Unity's licence
/// doesn't let us ship it, so it's downloaded from Unity's own servers when the player asks for it:
/// the only standalone package with the Windows players is the "Windows Build Support (Mono)"
/// module for the macOS editor, a .pkg (xar) whose Payload is a gzipped cpio archive. Only the
/// player files are kept, in the tool's cache folder.
/// </summary>
public static class UnityPlayerSource
{
    public const string UnityVersion = "2022.3.17f1";
    private const string Url =
        "https://download.unity3d.com/download_unity/4fc78088f837/MacEditorTargetInstaller/UnitySetup-Windows-Mono-Support-for-Editor-2022.3.17f1.pkg";
    private const long PackageSize = 180_824_073;
    private const string PackageSha256 = "d5630e57146099d6a1778938283fc276e426a358888783952482167ad7e5d519";
    private const string PayloadPath = "TargetSupport.pkg.tmp/Payload";
    private const string PlayerPrefix = "./Variations/win64_player_nondevelopment_mono/";

    /// <summary>The files a game folder needs from the player, relative to its root.</summary>
    public static readonly string[] Files = { "WindowsPlayer.exe", "UnityPlayer.dll", "UnityCrashHandler64.exe" };
    public const string MonoFolder = "MonoBleedingEdge";

    public static string CacheDir => Path.Combine(ToolCache.Dir, $"unity-{UnityVersion}-win64-mono");

    private static bool Ready => Files.All(f => File.Exists(Path.Combine(CacheDir, f)))
                                 && Directory.Exists(Path.Combine(CacheDir, MonoFolder));

    /// <summary>Downloads and unpacks the player unless it's already in the cache. Returns the folder.</summary>
    public static async Task<string> GetAsync(CancellationToken ct = default)
    {
        if (Ready)
            return CacheDir;

        Directory.CreateDirectory(ToolCache.Dir);
        string package = Path.Combine(ToolCache.Dir, $"unity-{UnityVersion}-windows-mono.pkg");
        if (!File.Exists(package) || new FileInfo(package).Length != PackageSize)
            await DownloadAsync(package, ct);
        Verify(package);

        Out.Info("  Unpacking the 64-bit player...");
        string staging = CacheDir + ".partial";
        if (Directory.Exists(staging))
            Directory.Delete(staging, recursive: true);
        Extract(package, staging);
        if (Directory.Exists(CacheDir))
            Directory.Delete(CacheDir, recursive: true);
        Directory.Move(staging, CacheDir);
        File.Delete(package);
        return CacheDir;
    }

    private static async Task DownloadAsync(string target, CancellationToken ct)
    {
        Out.Info($"  Downloading Unity's Windows player ({PackageSize / 1048576} MB) from download.unity3d.com...");
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
        using var response = await http.GetAsync(Url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        string partial = target + ".partial";
        await using (var source = await response.Content.ReadAsStreamAsync(ct))
        await using (var file = File.Create(partial))
        {
            var buffer = new byte[1 << 16];
            long done = 0;
            int lastPercent = -1;
            int read;
            while ((read = await source.ReadAsync(buffer, ct)) > 0)
            {
                await file.WriteAsync(buffer.AsMemory(0, read), ct);
                done += read;
                int percent = (int)(done * 100 / PackageSize);
                if (percent / 10 != lastPercent / 10)
                {
                    Out.Info($"    {percent}%");
                    lastPercent = percent;
                }
            }
        }
        File.Move(partial, target, overwrite: true);
    }

    // The package isn't signed in a way we can check here, so it has to match the one this was tested with.
    private static void Verify(string package)
    {
        using var stream = File.OpenRead(package);
        string hash = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        if (hash != PackageSha256)
        {
            stream.Dispose();
            File.Delete(package);
            throw new InvalidOperationException("The downloaded Unity package isn't the expected file (checksum mismatch). It was deleted; try again.");
        }
    }

    private static void Extract(string package, string destination)
    {
        using var xar = new XarArchive(File.OpenRead(package));
        using var payload = new System.IO.Compression.GZipStream(xar.Open(PayloadPath), System.IO.Compression.CompressionMode.Decompress);
        var cpio = new CpioReader(payload);
        int files = 0;
        for (var entry = cpio.Next(); entry != null; entry = cpio.Next())
        {
            if (!entry.Path.StartsWith(PlayerPrefix, StringComparison.Ordinal))
                continue;
            string relative = entry.Path[PlayerPrefix.Length..];
            if (!Wanted(relative))
                continue;
            string path = Path.Combine(destination, relative);
            if (entry.IsDirectory)
            {
                Directory.CreateDirectory(path);
                continue;
            }
            if (!entry.IsFile)
                continue;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using (var file = File.Create(path))
                cpio.CopyTo(file);
            // Keep the archive's permissions (executables stay executable); File.Copy carries them over.
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(path, (UnixFileMode)(entry.Mode & 0x1FF));
            files++;
        }
        if (files == 0)
            throw new InvalidDataException("The Unity package had no 64-bit player in it.");
    }

    private static bool Wanted(string relative) =>
        Array.IndexOf(Files, relative) >= 0
        || relative == MonoFolder
        || relative.StartsWith(MonoFolder + "/", StringComparison.Ordinal);
}
