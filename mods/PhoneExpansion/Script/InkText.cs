using System.Text;

namespace PhoneExpansion.Script;

/// <summary>Writing plain text into Ink source so none of it is read as Ink syntax.</summary>
internal static class InkText
{
    // Everything Ink gives a meaning somewhere in a line: logic, choices, gathers, diverts,
    // glue, threads, tags, comments and escapes. A backslash makes any of them literal.
    private const string Special = "\\{}[]|#~*+-=/<>";

    public static string Escape(string text)
    {
        var sb = new StringBuilder(text.Length + 8);
        foreach (char c in text)
        {
            if (c == '\r' || c == '\t')
            {
                sb.Append(' ');
                continue;
            }
            if (Special.IndexOf(c) >= 0)
                sb.Append('\\');
            sb.Append(c);
        }
        return sb.ToString().Trim();
    }

    /// <summary>A safe Ink identifier made from any text (letters, digits and underscores).</summary>
    public static string Identifier(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (char c in text)
            sb.Append(char.IsLetterOrDigit(c) && c < 128 ? c : '_');
        if (sb.Length == 0 || char.IsDigit(sb[0]))
            sb.Insert(0, '_');
        return sb.ToString();
    }
}
