using System;

namespace NeetLoader.Runtime;

/// <summary>
/// Which copy of the game this process is. Normally there's one, but a second can run next to it
/// for testing (NEET_INSTANCE=2, started with <c>neet launch --instance 2</c>). Things that can't
/// be shared between two processes (log files, the dev bridge's port) are told apart with this.
/// </summary>
public static class Instance
{
    /// <summary>1 for the normal game, 2 and up for extra copies.</summary>
    public static int Number { get; } = Read();

    public static bool IsFirst => Number == 1;

    /// <summary>Added to file names so processes never share one: "" for the first instance, "-2" for the second.</summary>
    public static string Suffix { get; } = Number == 1 ? "" : "-" + Number;

    private static int Read()
    {
        string text = Environment.GetEnvironmentVariable("NEET_INSTANCE");
        return int.TryParse(text, out int number) && number > 1 ? number : 1;
    }
}
