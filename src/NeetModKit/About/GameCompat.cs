using System.IO;
using NeetLoader;
using NeetLoader.Config;

namespace NeetModKit.About;

public enum GameCompatStatus
{
    /// <summary>No record shipped with the framework.</summary>
    Unknown,

    /// <summary>The running game is the build this version was tested with.</summary>
    Tested,

    /// <summary>The game has changed since this version was tested.</summary>
    Untested,
}

/// <summary>
/// Compares the running game with the build this version of the pack was tested with
/// (compat/game.json in the repository, shipped as game-compat.json). The game code assembly's
/// module version id changes with every build of the game, so it tells builds apart without
/// hashing anything.
/// </summary>
public static class GameCompat
{
    public const string FileName = "game-compat.json";

    public static GameCompatStatus Status { get; private set; }

    /// <summary>Steam build id of the tested build, if known.</summary>
    public static string TestedBuild { get; private set; }

    internal static void Check(NeetMod kit)
    {
        Status = GameCompatStatus.Unknown;
        string path = kit.PathTo(FileName);
        if (!File.Exists(path) || !JsonFile.TryRead(path, out Record record, out _) || record?.GameCode?.Mvid == null)
            return;

        TestedBuild = record.BuildId;
        string running = typeof(HouseManager).Assembly.ManifestModule.ModuleVersionId.ToString("D");
        Status = string.Equals(running, record.GameCode.Mvid, System.StringComparison.OrdinalIgnoreCase)
            ? GameCompatStatus.Tested
            : GameCompatStatus.Untested;

        if (Status == GameCompatStatus.Tested)
            kit.Log.Info($"The game is the build {PackInfo.Name} {PackInfo.Version} was tested with (Steam build {record.BuildId}).");
        else
            kit.Log.Warn($"The game has changed since {PackInfo.Name} {PackInfo.Version} was tested (with Steam build {record.BuildId}). " +
                         "Most things should still work; please report anything that doesn't.");
    }

    private sealed class Record
    {
        public string BuildId;
        public CodeInfo GameCode;
    }

    private sealed class CodeInfo
    {
        public string Mvid;
    }
}
