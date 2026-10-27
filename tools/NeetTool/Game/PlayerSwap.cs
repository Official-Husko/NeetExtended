using System.Buffers.Binary;

namespace NeetTool.Game;

public enum PlayerArch
{
    Unknown,
    X86,
    X64,
}

/// <summary>
/// Swaps the game's 32-bit Unity player for the 64-bit one of the same Unity version, and back.
/// The game's data (NEET_Data) is the same for both, so only the exe, UnityPlayer.dll, the crash
/// handler and the Mono runtime change. The 32-bit files are kept in NeetLoader/backup/player-x86;
/// Steam's "Verify integrity of game files" also brings them back.
/// </summary>
public sealed class PlayerSwap
{
    private const string CrashHandler32 = "UnityCrashHandler32.exe";
    private const string CrashHandler64 = "UnityCrashHandler64.exe";
    private const string Player = "UnityPlayer.dll";

    private readonly GameInstall _game;

    public PlayerSwap(GameInstall game)
    {
        _game = game;
    }

    public string BackupDir => Path.Combine(_game.LoaderDataDir, "backup", "player-x86");

    public PlayerArch Current => ReadArch(_game.Exe);

    public bool HasBackup => File.Exists(Path.Combine(BackupDir, GameInstall.ExeName))
                             && Directory.Exists(Path.Combine(BackupDir, UnityPlayerSource.MonoFolder));

    public IEnumerable<string> To64Bit(string playerDir)
    {
        if (Current == PlayerArch.X64)
        {
            yield return "The game already runs the 64-bit player";
            yield break;
        }
        if (Current != PlayerArch.X86)
            throw new InvalidOperationException($"{GameInstall.ExeName} isn't a Windows player this tool knows.");

        Directory.CreateDirectory(BackupDir);
        foreach (string file in new[] { GameInstall.ExeName, Player, CrashHandler32 })
        {
            string source = Path.Combine(_game.Dir, file);
            if (File.Exists(source))
                File.Copy(source, Path.Combine(BackupDir, file), overwrite: true);
        }
        CopyFolder(Path.Combine(_game.Dir, UnityPlayerSource.MonoFolder), Path.Combine(BackupDir, UnityPlayerSource.MonoFolder));
        yield return $"Kept the 32-bit player in {BackupDir}";

        Put64BitIn(playerDir);
        yield return $"Switched to the 64-bit player (Unity {UnityPlayerSource.UnityVersion})";
    }

    public IEnumerable<string> To32Bit()
    {
        if (Current != PlayerArch.X64)
        {
            yield return "The game already runs the 32-bit player";
            yield break;
        }
        if (!HasBackup)
            throw new InvalidOperationException(
                "The 32-bit player wasn't backed up here. Use Steam's \"Verify integrity of game files\" to get it back.");

        Restore32Bit();
        yield return "Switched back to the 32-bit player";
    }

    // If a copy fails half way (a full disk, say), the 32-bit files go back, so the game still starts.
    private void Put64BitIn(string playerDir)
    {
        try
        {
            File.Copy(Path.Combine(playerDir, "WindowsPlayer.exe"), _game.Exe, overwrite: true);
            File.Copy(Path.Combine(playerDir, Player), Path.Combine(_game.Dir, Player), overwrite: true);
            File.Copy(Path.Combine(playerDir, CrashHandler64), Path.Combine(_game.Dir, CrashHandler64), overwrite: true);
            DeleteIfThere(Path.Combine(_game.Dir, CrashHandler32));
            CopyFolder(Path.Combine(playerDir, UnityPlayerSource.MonoFolder), Path.Combine(_game.Dir, UnityPlayerSource.MonoFolder));
        }
        catch
        {
            Restore32Bit();
            throw;
        }
    }

    // Copies the backed-up 32-bit files over the game's, then drops the backup.
    private void Restore32Bit()
    {
        foreach (string file in new[] { GameInstall.ExeName, Player, CrashHandler32 })
        {
            string source = Path.Combine(BackupDir, file);
            if (File.Exists(source))
                File.Copy(source, Path.Combine(_game.Dir, file), overwrite: true);
        }
        DeleteIfThere(Path.Combine(_game.Dir, CrashHandler64));
        CopyFolder(Path.Combine(BackupDir, UnityPlayerSource.MonoFolder), Path.Combine(_game.Dir, UnityPlayerSource.MonoFolder));
        Directory.Delete(BackupDir, recursive: true);
        string backupRoot = Path.GetDirectoryName(BackupDir)!;
        if (Directory.Exists(backupRoot) && !Directory.EnumerateFileSystemEntries(backupRoot).Any())
            Directory.Delete(backupRoot);
    }

    /// <summary>Reads the machine type from a Windows executable's PE header.</summary>
    public static PlayerArch ReadArch(string exe)
    {
        if (!File.Exists(exe))
            return PlayerArch.Unknown;
        using var stream = File.OpenRead(exe);
        Span<byte> buffer = stackalloc byte[64];
        if (stream.Read(buffer) < 64 || buffer[0] != 'M' || buffer[1] != 'Z')
            return PlayerArch.Unknown;
        int peOffset = BinaryPrimitives.ReadInt32LittleEndian(buffer[0x3C..]);
        stream.Position = peOffset;
        Span<byte> pe = stackalloc byte[6];
        if (stream.Read(pe) < 6 || pe[0] != 'P' || pe[1] != 'E')
            return PlayerArch.Unknown;
        return BinaryPrimitives.ReadUInt16LittleEndian(pe[4..]) switch
        {
            0x014C => PlayerArch.X86,
            0x8664 => PlayerArch.X64,
            _ => PlayerArch.Unknown,
        };
    }

    // Replaces the target folder with a copy of the source.
    private static void CopyFolder(string source, string target)
    {
        if (Directory.Exists(target))
            Directory.Delete(target, recursive: true);
        foreach (string dir in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(target, Path.GetRelativePath(source, dir)));
        Directory.CreateDirectory(target);
        foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            File.Copy(file, Path.Combine(target, Path.GetRelativePath(source, file)), overwrite: true);
    }

    private static void DeleteIfThere(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }
}
