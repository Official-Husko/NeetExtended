namespace NeetTool.Game;

/// <summary>A NEET Demo install on disk.</summary>
public sealed class GameInstall
{
    public const string FolderName = "NEET Demo";
    public const string ExeName = "NEET.exe";

    public string Dir { get; }
    public string DataDir => Path.Combine(Dir, "NEET_Data");
    public string ManagedDir => Path.Combine(DataDir, "Managed");
    public string ModsDir => Path.Combine(Dir, "Mods");
    public string LoaderDataDir => Path.Combine(Dir, "NeetLoader");
    public string LogsDir => Path.Combine(LoaderDataDir, "logs");
    public string Exe => Path.Combine(Dir, ExeName);

    /// <summary>"" for the first instance, "-2" for the second: what the loader adds to its file names.</summary>
    public static string Suffix(int instance) => instance <= 1 ? "" : "-" + instance;

    public string LoaderLogFile(int instance = 1) => Path.Combine(LogsDir, "latest" + Suffix(instance) + ".log");

    public string ErrorLogFile(int instance = 1) => Path.Combine(LogsDir, "errors" + Suffix(instance) + ".log");

    /// <summary>Unity's log. Extra instances write theirs next to the loader logs (see <c>launch --instance</c>).</summary>
    public string PlayerLogFile(int instance = 1) => instance <= 1
        ? Path.Combine(PersistentDataDir() ?? "", "Player.log")
        : Path.Combine(LogsDir, $"player{Suffix(instance)}.log");
    private GameInstall(string dir)
    {
        Dir = Path.GetFullPath(dir);
    }

    /// <summary>Uses --game, then NEET_GAME_DIR, then every Steam library.</summary>
    public static GameInstall Locate(string? explicitDir)
    {
        foreach (string? candidate in Candidates(explicitDir))
        {
            if (candidate != null && File.Exists(Path.Combine(candidate, ExeName)))
                return new GameInstall(candidate);
        }
        throw new InvalidOperationException(
            "Couldn't find NEET Demo. Pass --game <folder> or set NEET_GAME_DIR.");
    }

    private static IEnumerable<string?> Candidates(string? explicitDir)
    {
        yield return explicitDir;
        yield return Environment.GetEnvironmentVariable("NEET_GAME_DIR");
        foreach (string lib in Steam.LibraryFolders())
            yield return Path.Combine(lib, "common", FolderName);
    }

    /// <summary>Folder holding Player.log and save files (inside the Proton prefix on Linux).</summary>
    public string? PersistentDataDir()
    {
        const string relative = "AppData/LocalLow/NEET Games/NEET";
        if (OperatingSystem.IsWindows())
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), relative);

        string? prefix = Steam.CompatDataDir();
        return prefix is null ? null : Path.Combine(prefix, "pfx/drive_c/users/steamuser", relative);
    }
}
