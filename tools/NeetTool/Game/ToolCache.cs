namespace NeetTool.Game;

/// <summary>Where the tool keeps downloads between runs.</summary>
public static class ToolCache
{
    public static string Dir
    {
        get
        {
            if (OperatingSystem.IsWindows())
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "neet", "cache");
            string? xdg = Environment.GetEnvironmentVariable("XDG_CACHE_HOME");
            string root = string.IsNullOrEmpty(xdg)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache")
                : xdg;
            return Path.Combine(root, "neet");
        }
    }
}
