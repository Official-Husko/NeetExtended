using System.Diagnostics;

namespace NeetTool.Game;

/// <summary>Starting and stopping the game.</summary>
public static class GameProcess
{
    public static readonly string[] WindowedArgs =
        { "-screen-fullscreen", "0", "-screen-width", "1600", "-screen-height", "900" };

    public static bool IsRunning() => FindPids().Any();

    /// <summary>Launches through Steam, which applies the user's own Proton choice and launch options.</summary>
    public static void LaunchViaSteam(IEnumerable<string> gameArgs)
    {
        var psi = new ProcessStartInfo(OperatingSystem.IsWindows() ? "steam://" : "steam")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        psi.ArgumentList.Add("-applaunch");
        psi.ArgumentList.Add(Steam.AppId.ToString());
        foreach (string a in gameArgs)
            psi.ArgumentList.Add(a);
        Process.Start(psi);
    }

    /// <summary>Runs the game straight through a Proton build, without the Steam client.</summary>
    public static void LaunchViaProton(GameInstall game, string protonScript, IEnumerable<string> gameArgs)
    {
        string compatData = Steam.CompatDataDir()
            ?? throw new InvalidOperationException("No Proton prefix yet. Start the game once from Steam.");

        var psi = new ProcessStartInfo(protonScript)
        {
            WorkingDirectory = game.Dir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        psi.Environment["STEAM_COMPAT_DATA_PATH"] = compatData;
        psi.Environment["STEAM_COMPAT_CLIENT_INSTALL_PATH"] = Steam.Root ?? "";
        psi.Environment["SteamAppId"] = Steam.AppId.ToString();
        psi.Environment["SteamGameId"] = Steam.AppId.ToString();
        psi.ArgumentList.Add("run");
        psi.ArgumentList.Add(game.Exe);
        foreach (string a in gameArgs)
            psi.ArgumentList.Add(a);
        Process.Start(psi);
    }

    /// <summary>Kills the game process. Prefer asking the game to quit through the bridge first.</summary>
    public static int Kill()
    {
        int killed = 0;
        foreach (int pid in FindPids())
        {
            try
            {
                Process.GetProcessById(pid).Kill();
                killed++;
            }
            catch (ArgumentException)
            {
                // Already gone.
            }
        }
        return killed;
    }

    private static IEnumerable<int> FindPids()
    {
        if (OperatingSystem.IsWindows())
            return Process.GetProcessesByName("NEET").Select(p => p.Id);

        // Under Wine the process shows up with its Windows path as the command line.
        var pids = new List<int>();
        foreach (string dir in Directory.EnumerateDirectories("/proc"))
        {
            if (!int.TryParse(Path.GetFileName(dir), out int pid))
                continue;
            try
            {
                string cmd = File.ReadAllText(Path.Combine(dir, "cmdline"));
                if (cmd.StartsWith("Z:\\", StringComparison.Ordinal) && cmd.Contains("\\NEET.exe", StringComparison.Ordinal))
                    pids.Add(pid);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
        return pids;
    }
}
