using System.Diagnostics;

namespace NeetTool.Game;

/// <summary>Starting and stopping the game.</summary>
public static class GameProcess
{
    public static readonly string[] WindowedArgs =
        { "-screen-fullscreen", "0", "-screen-width", "1600", "-screen-height", "900" };

    /// <summary>Is the game running? With an instance, only that copy counts (1 is the normal one).</summary>
    public static bool IsRunning(int? instance = null) => FindPids(instance).Any();

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
    public static void LaunchViaProton(GameInstall game, string protonScript, IEnumerable<string> gameArgs, int instance = 1)
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
        if (instance > 1)
            psi.Environment["NEET_INSTANCE"] = instance.ToString();
        psi.ArgumentList.Add("run");
        psi.ArgumentList.Add(game.Exe);
        foreach (string a in gameArgs)
            psi.ArgumentList.Add(a);
        Process.Start(psi);
    }

    /// <summary>
    /// Starts one copy of the game. Extra instances always go straight through Proton, because Steam
    /// won't start a second copy or pass an environment variable, and write their own Unity log.
    /// Returns a short description of how it was started.
    /// </summary>
    public static string Launch(GameInstall game, int instance, bool direct, IEnumerable<string> gameArgs)
    {
        var args = gameArgs.ToList();
        if (instance > 1)
        {
            if (OperatingSystem.IsWindows())
                throw new InvalidOperationException("--instance 2 and up is only supported through Proton on Linux.");
            Directory.CreateDirectory(game.LogsDir);
            // Without its own Unity log, both processes would write Player.log. Wine wants the path in Windows form.
            args.Add("-logFile");
            args.Add("Z:" + game.PlayerLogFile(instance).Replace('/', '\\'));
            direct = true;
        }

        if (!direct)
        {
            LaunchViaSteam(args);
            return "Asked Steam to start the game";
        }

        string tool = Steam.CompatToolName()
            ?? throw new InvalidOperationException("No Proton build is set for the game in Steam.");
        string proton = Steam.ProtonScript(tool)
            ?? throw new InvalidOperationException($"Couldn't find {tool} on disk.");
        LaunchViaProton(game, proton, args, instance);
        return instance > 1 ? $"Started instance {instance} through {tool}" : $"Started through {tool}";
    }

    /// <summary>Kills the game process (one instance, or all of them). Prefer asking the game to quit through the bridge first.</summary>
    public static int Kill(int? instance = null)
    {
        int killed = 0;
        foreach (int pid in FindPids(instance))
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

    /// <summary>
    /// Which instance a game process is: its NEET_INSTANCE environment variable, 1 when it has none.
    /// Only readable on Linux (/proc); null when it can't be told.
    /// </summary>
    private static int? InstanceOf(string procDir)
    {
        try
        {
            foreach (string entry in File.ReadAllText(Path.Combine(procDir, "environ")).Split('\0'))
            {
                if (entry.StartsWith("NEET_INSTANCE=", StringComparison.Ordinal))
                    return int.TryParse(entry.AsSpan("NEET_INSTANCE=".Length), out int n) && n > 1 ? n : 1;
            }
            return 1;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static IEnumerable<int> FindPids(int? instance)
    {
        // Windows can't tell instances apart from outside the process, so the filter is ignored there.
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
                if (cmd.StartsWith("Z:\\", StringComparison.Ordinal) && cmd.Contains("\\NEET.exe", StringComparison.Ordinal)
                    && (instance is null || InstanceOf(dir) == instance))
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
