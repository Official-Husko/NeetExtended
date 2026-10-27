using UpdateChecker.Check;
using UpdateChecker.Notice;

namespace UpdateChecker;

/// <summary>Hooks for trying the mod from the dev bridge (type.call), since the real latest release may be the one running.</summary>
internal static class Testing
{
    /// <summary>Pretends GitHub's newest release is this version ("0.9.0").</summary>
    public static string FakeLatest(string version)
    {
        return UpdateState.Set("v" + version, "https://example.invalid/releases/v" + version)
            ? $"available={UpdateState.Available} latest={UpdateState.Latest}"
            : "not a version";
    }

    /// <summary>Forgets what was found (until the next check).</summary>
    public static string Clear()
    {
        UpdateState.Clear();
        return "cleared";
    }

    /// <summary>Runs the in-game telling now, the way the hourly one would, if the settings and state allow it.</summary>
    public static string TellNow() => UpdateNotice.CanTell() ? UpdateNotice.Tell() : "not allowed right now";

    /// <summary>The state, the three flags and whether telling is allowed.</summary>
    public static string Report() =>
        $"available={UpdateState.Available} latest={UpdateState.Latest} status='{UpdateState.Status}' canTell={UpdateNotice.CanTell()} " +
        $"email={NeetModKit.Rules.ModFlags.Get(UpdateNotice.EmailFlag)} calls={NeetModKit.Rules.ModFlags.Get(UpdateNotice.CallsFlag)} " +
        $"hour={NeetModKit.Rules.ModFlags.Get(UpdateNotice.HourFlag)}";
}
