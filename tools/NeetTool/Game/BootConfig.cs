namespace NeetTool.Game;

/// <summary>
/// NEET_Data/boot.config, the settings Unity's player reads before anything else. With
/// "gc-max-time-slice=3" the player runs Mono's garbage collector incrementally (spread over frames,
/// at most 3 ms each) instead of stopping the game for each whole collection. Unity writes that line
/// for games built with incremental GC; NEET was built without it. The original is backed up once.
/// </summary>
public sealed class BootConfig
{
    private const string Key = "gc-max-time-slice";
    private const string IncrementalLine = Key + "=3";
    private const string BackupSuffix = ".neetbak";

    private readonly string _path;

    public BootConfig(GameInstall game)
    {
        _path = Path.Combine(game.DataDir, "boot.config");
    }

    private string Backup => _path + BackupSuffix;

    public bool IncrementalGc => File.Exists(_path) && File.ReadLines(_path).Any(IsGcLine);

    public void EnableIncrementalGc()
    {
        if (IncrementalGc)
            return;
        if (!File.Exists(Backup))
            File.Copy(_path, Backup);
        string text = File.ReadAllText(_path);
        string newline = text.Contains("\r\n") ? "\r\n" : "\n";
        if (text.Length > 0 && !text.EndsWith('\n'))
            text += newline;
        File.WriteAllText(_path, text + IncrementalLine + newline);
    }

    /// <summary>Puts the original back if there is one, otherwise just drops the line.</summary>
    public void Restore()
    {
        if (File.Exists(Backup))
        {
            File.Copy(Backup, _path, overwrite: true);
            File.Delete(Backup);
            return;
        }
        if (!IncrementalGc)
            return;
        var kept = File.ReadAllLines(_path).Where(line => !IsGcLine(line));
        File.WriteAllLines(_path, kept);
    }

    private static bool IsGcLine(string line) => line.TrimStart().StartsWith(Key + "=", StringComparison.Ordinal);
}
