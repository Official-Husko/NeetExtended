using System.Reflection;
using System.Text.RegularExpressions;

namespace NeetModKit.About;

/// <summary>
/// The mod pack's name and the build it is: the version from Directory.Build.props and the commit
/// it was built from (build/Revision.targets puts both into the informational version,
/// "0.1.0+1a2b3c4").
/// </summary>
public static class PackInfo
{
    /// <summary>The project's name, shown in game. Change it here to rename the pack.</summary>
    public const string Name = "NeetExtended";

    static PackInfo()
    {
        string informational = typeof(PackInfo).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (string.IsNullOrEmpty(informational))
        {
            Version = typeof(PackInfo).Assembly.GetName().Version.ToString(3);
            return;
        }
        int plus = informational.IndexOf('+');
        Version = plus < 0 ? informational : informational.Substring(0, plus);
        Commit = plus < 0 ? null : informational.Substring(plus + 1);
    }

    /// <summary>The name with spaces between its words ("Neet Extended"), for all-caps headers.</summary>
    public static string Title { get; } = Regex.Replace(Name, "(?<=[a-z])(?=[A-Z])", " ");

    /// <summary>"0.1.0".</summary>
    public static string Version { get; }

    /// <summary>Short commit hash of the build ("1a2b3c4", "-dirty" when built from uncommitted changes), or null.</summary>
    public static string Commit { get; }

    /// <summary>"NeetExtended v0.1.0 (1a2b3c4)".</summary>
    public static string Label => Commit == null ? $"{Name} v{Version}" : $"{Name} v{Version} ({Commit})";
}
