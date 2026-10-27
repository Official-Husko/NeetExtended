namespace NeetTool.Game;

/// <summary>
/// Which player the installer sets up: the 64-bit one, unless the original 32-bit one was asked for
/// (<c>install --32bit</c>, the package's use-32bit script). That choice is a small file in the
/// loader's folder, so installing again to update the mods keeps it. <c>install --64bit</c> clears it.
/// </summary>
public sealed class PlayerChoice
{
    private const string FileName = "keep-32bit.txt";
    private const string Note =
        "The original 32-bit player was picked for this game (install --32bit), so installing again keeps it.\n"
        + "Run install --64bit (or the package's use-64bit script) to switch to the 64-bit player.\n";

    private readonly string _path;

    public PlayerChoice(GameInstall game)
    {
        _path = Path.Combine(game.LoaderDataDir, FileName);
    }

    public bool Keeps32Bit => File.Exists(_path);

    public void Keep32Bit()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, Note);
    }

    public void Clear()
    {
        if (File.Exists(_path))
            File.Delete(_path);
    }
}
