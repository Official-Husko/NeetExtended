using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using NeetTool.Game;

namespace NeetTool.Compat;

/// <summary>
/// Which build of the game is installed: Steam's build id from the app manifest, the Unity version,
/// and the game code assembly (Assembly-CSharp.dll) by module version id and SHA-256. The game code
/// is what the mods patch, and unlike NEET.exe the installer never swaps it.
/// </summary>
public sealed partial record GameBuild(string? BuildId, string? UnityVersion, string CodeMvid, string CodeSha256)
{
    public const string CodeFile = "NEET_Data/Managed/Assembly-CSharp.dll";

    public static GameBuild Read(GameInstall game)
    {
        string code = Path.Combine(game.Dir, CodeFile);
        if (!File.Exists(code))
            throw new FileNotFoundException("The game's code assembly is missing.", code);

        string mvid;
        using (var pe = new PEReader(File.OpenRead(code)))
        {
            var metadata = pe.GetMetadataReader();
            mvid = metadata.GetGuid(metadata.GetModuleDefinition().Mvid).ToString("D");
        }
        string sha;
        using (var stream = File.OpenRead(code))
            sha = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();

        return new GameBuild(SteamBuildId(game), ReadUnityVersion(game), mvid, sha);
    }

    /// <summary>The same game code, whatever the build id says.</summary>
    public bool SameCode(GameBuild other) => string.Equals(CodeMvid, other.CodeMvid, StringComparison.OrdinalIgnoreCase)
                                             && string.Equals(CodeSha256, other.CodeSha256, StringComparison.OrdinalIgnoreCase);

    // The manifest sits two folders up, in the steamapps folder of the library the game is in.
    private static string? SteamBuildId(GameInstall game)
    {
        string manifest = Path.GetFullPath(Path.Combine(game.Dir, "..", "..", $"appmanifest_{Steam.AppId}.acf"));
        if (!File.Exists(manifest))
            return null;
        var m = BuildIdEntry().Match(File.ReadAllText(manifest));
        return m.Success ? m.Groups[1].Value : null;
    }

    // Unity writes its version near the start of every serialized data file.
    private static string? ReadUnityVersion(GameInstall game)
    {
        string data = Path.Combine(game.DataDir, "globalgamemanagers");
        if (!File.Exists(data))
            return null;
        var head = new byte[256];
        using (var stream = File.OpenRead(data))
            stream.ReadAtLeast(head, head.Length, throwOnEndOfStream: false);
        var m = UnityVersionText().Match(System.Text.Encoding.ASCII.GetString(head));
        return m.Success ? m.Value : null;
    }

    [GeneratedRegex("\"buildid\"\\s+\"(\\d+)\"")]
    private static partial Regex BuildIdEntry();

    [GeneratedRegex(@"\d{4}\.\d+\.\d+[abfp]\d+")]
    private static partial Regex UnityVersionText();
}
