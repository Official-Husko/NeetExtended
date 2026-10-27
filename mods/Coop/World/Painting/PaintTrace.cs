using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Coop.World.Painting;

/// <summary>
/// For tests: counts which game code paints (the first caller outside PaintTarget), for a few seconds
/// after <see cref="PaintTestApi.Trace"/>. Off, it costs one comparison per splat.
/// </summary>
internal static class PaintTrace
{
    private static readonly Dictionary<string, int> Callers = new Dictionary<string, int>();
    private static float _until;

    public static bool On => _until > 0f && UnityEngine.Time.unscaledTime < _until;

    public static void Begin(float seconds)
    {
        Callers.Clear();
        _until = UnityEngine.Time.unscaledTime + seconds;
    }

    public static void Note(bool sent)
    {
        var frames = new StackTrace(2, false).GetFrames();
        string caller = "?";
        if (frames != null)
        {
            foreach (var frame in frames)
            {
                var method = frame.GetMethod();
                var type = method?.DeclaringType;
                if (type == null || type == typeof(PaintTarget) || type.Namespace?.StartsWith("Coop") == true || method.Name.Contains("_Patch"))
                    continue;
                caller = type.Name + "." + method.Name;
                break;
            }
        }
        var painter = PaintSources.Painter;
        string key = caller + (painter != null ? " [" + painter.name + "]" : "") + (sent ? "" : " (not sent)");
        Callers.TryGetValue(key, out int n);
        Callers[key] = n + 1;
    }

    public static string Report()
    {
        if (Callers.Count == 0)
            return On ? "Tracing, nothing painted yet." : "Nothing traced.";
        var text = new StringBuilder();
        foreach (var pair in Callers)
            text.Append(pair.Key).Append(' ').Append(pair.Value).Append("; ");
        return text.ToString();
    }
}
