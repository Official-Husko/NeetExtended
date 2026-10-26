using UnityEngine;

namespace PerformanceFixes.Logging;

/// <summary>
/// Turns off stack traces for plain log lines and warnings. Unity captures a stack trace for every
/// line it logs, which is slow next to the logging itself, and the game logs a lot (a line every
/// time you crouch, warnings for every save it loads). Errors and exceptions keep theirs.
/// </summary>
internal static class StackTraces
{
    private static StackTraceLogType? _log;
    private static StackTraceLogType? _warning;

    public static void Off()
    {
        _log ??= Application.GetStackTraceLogType(LogType.Log);
        _warning ??= Application.GetStackTraceLogType(LogType.Warning);
        Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
        Application.SetStackTraceLogType(LogType.Warning, StackTraceLogType.None);
    }

    public static void Restore()
    {
        if (_log.HasValue)
            Application.SetStackTraceLogType(LogType.Log, _log.Value);
        if (_warning.HasValue)
            Application.SetStackTraceLogType(LogType.Warning, _warning.Value);
        _log = _warning = null;
    }
}
