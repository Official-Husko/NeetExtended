using System.Text;

namespace Coop.World.Breakables;

/// <summary>
/// Test hook for the dev bridge: <c>type.call type=Coop.World.Breakables.BreakTestApi method=Registry</c>
/// lists the shared breakables as this game sees them, so two games can be compared line by line.
/// </summary>
public static class BreakTestApi
{
    public static string Registry()
    {
        var lines = new System.Collections.Generic.List<string>();
        foreach (var pair in SceneBreakers.All)
        {
            var b = pair.Value;
            lines.Add(b == null
                ? $"{pair.Key}: gone"
                : $"{pair.Key}: {Objects.ScenePath.Describe(b.transform)} hits {b.breakCollisionAmounts}/{b.breakCollisionAmountsMax}{(b.broken ? " broken" : "")}");
        }
        lines.Sort(string.CompareOrdinal);
        var sb = new StringBuilder();
        foreach (var line in lines)
            sb.AppendLine(line);
        return sb.ToString();
    }
}
