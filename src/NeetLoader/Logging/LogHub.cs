using System;
using System.IO;
using UnityEngine;

namespace NeetLoader.Logging;

/// <summary>
/// Central log sink. Every <see cref="ModLogger"/> writes here; the hub fans out to the
/// ring buffer, the log file and Unity's own console.
/// </summary>
public static class LogHub
{
    [ThreadStatic] private static bool _forwardingToUnity;

    private static LogFile _file;

    /// <summary>Recent lines from mods, the loader and Unity itself.</summary>
    public static LogBuffer Buffer { get; } = new LogBuffer(4096);

    /// <summary>Lines below this level are dropped (still kept in the buffer when from Unity).</summary>
    public static LogLevel MinFileLevel { get; set; } = LogLevel.Info;

    internal static void Init(string logsDir)
    {
        _file = new LogFile(Path.Combine(logsDir, "latest.log"));
        Application.logMessageReceivedThreaded += OnUnityLog;
    }

    internal static void Flush() => _file?.Flush();

    internal static void Shutdown()
    {
        Application.logMessageReceivedThreaded -= OnUnityLog;
        _file?.Dispose();
        _file = null;
    }

    internal static void Write(LogLevel level, string source, string message)
    {
        Buffer.Add(level, source, message);
        if (level >= MinFileLevel)
            _file?.Write(new LogEntry(Buffer.LastSequence, DateTime.Now, level, source, message));

        _forwardingToUnity = true;
        try
        {
            string line = "[" + source + "] " + message;
            switch (level)
            {
                case LogLevel.Error: Debug.LogError(line); break;
                case LogLevel.Warning: Debug.LogWarning(line); break;
                default: Debug.Log(line); break;
            }
        }
        finally
        {
            _forwardingToUnity = false;
        }
    }

    private static void OnUnityLog(string condition, string stackTrace, LogType type)
    {
        if (_forwardingToUnity)
            return;

        LogLevel level = type switch
        {
            LogType.Error or LogType.Exception or LogType.Assert => LogLevel.Error,
            LogType.Warning => LogLevel.Warning,
            _ => LogLevel.Debug,
        };

        string message = level == LogLevel.Error && !string.IsNullOrEmpty(stackTrace)
            ? condition + "\n" + stackTrace.TrimEnd()
            : condition;

        Buffer.Add(level, "Unity", message);
        if (level >= LogLevel.Warning)
            _file?.Write(new LogEntry(Buffer.LastSequence, DateTime.Now, level, "Unity", message));
    }
}
