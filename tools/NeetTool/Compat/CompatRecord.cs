using System.Text.Json;
using System.Text.Json.Serialization;

namespace NeetTool.Compat;

/// <summary>
/// compat/game.json: the game build the current version of the pack was tested with. The framework
/// ships a copy and compares it with the running game; the release script and the compatibility
/// badge read it too.
/// </summary>
public sealed class CompatRecord
{
    public const string RelativePath = "compat/game.json";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public string Game { get; set; } = "NEET Demo";
    public int AppId { get; set; } = NeetTool.Game.Steam.AppId;
    public string? BuildId { get; set; }
    public string? Unity { get; set; }
    public CodeInfo GameCode { get; set; } = new();
    public string? TestedWith { get; set; }
    public string? TestedOn { get; set; }

    public sealed class CodeInfo
    {
        public string File { get; set; } = GameBuild.CodeFile;
        public string Mvid { get; set; } = "";
        public string Sha256 { get; set; } = "";
    }

    public GameBuild ToBuild() => new(BuildId, Unity, GameCode.Mvid, GameCode.Sha256);

    public static CompatRecord FromBuild(GameBuild build, string packVersion) => new()
    {
        BuildId = build.BuildId,
        Unity = build.UnityVersion,
        GameCode = new CodeInfo { Mvid = build.CodeMvid, Sha256 = build.CodeSha256 },
        TestedWith = packVersion,
        TestedOn = DateOnly.FromDateTime(DateTime.Now).ToString("yyyy-MM-dd"),
    };

    public static CompatRecord? Load(string repo)
    {
        string path = Path.Combine(repo, RelativePath);
        return File.Exists(path) ? JsonSerializer.Deserialize<CompatRecord>(File.ReadAllText(path), Options) : null;
    }

    public void Save(string repo)
    {
        string path = Path.Combine(repo, RelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(this, Options) + "\n");
    }
}
