using System;
using NeetLoader.Runtime;
using NeetModKit.About;

namespace UpdateChecker.Check;

/// <summary>
/// What the last check found: whether a newer release is out, which one, and where its page is.
/// Everything else (the corner note, the email, the call, the menu page) reads this.
/// </summary>
internal static class UpdateState
{
    /// <summary>A newer release than the one running exists.</summary>
    public static bool Available { get; private set; }

    /// <summary>The newest release found, "0.3.0" (no "v"), or null before any check worked.</summary>
    public static string Latest { get; private set; }

    /// <summary>The release's page, or null.</summary>
    public static string Url { get; private set; }

    /// <summary>What happened last, for the menu page.</summary>
    public static string Status { get; private set; } = "Not checked yet.";

    public static string Current => PackInfo.Version;

    /// <summary>Raised on the main thread whenever <see cref="Available"/>, <see cref="Latest"/> or the status changes.</summary>
    public static event Action Changed;

    /// <summary>Takes a release tag ("v0.3.0") and its page. Returns false if the tag isn't a version.</summary>
    public static bool Set(string tag, string url)
    {
        if (!TryParse(tag, out var latest) || !TryParse(Current, out var current))
            return false;
        Latest = latest.ToString();
        Url = url;
        Available = latest > current;
        Status = Available ? $"v{Latest} is out." : "You have the newest version.";
        Changed?.Invoke();
        return true;
    }

    public static void Fail(string reason)
    {
        Status = "Couldn't check: " + reason;
        Changed?.Invoke();
    }

    /// <summary>Forgets what was found (the check was switched off).</summary>
    public static void Clear()
    {
        Available = false;
        Latest = Url = null;
        Status = "Not checked yet.";
        Changed?.Invoke();
    }

    /// <summary>Runs <paramref name="handler"/> on every change until <paramref name="owner"/> unloads.</summary>
    public static void Subscribe(ITracker owner, Action handler)
    {
        Changed += handler;
        owner.Track(Disposable.Create(() => Changed -= handler));
    }

    /// <summary>"v0.3.0", "0.3.0" and "0.3.0-beta" all become 0.3.0; "0.3" becomes 0.3.0.</summary>
    internal static bool TryParse(string text, out Version version)
    {
        version = null;
        if (string.IsNullOrWhiteSpace(text))
            return false;
        text = text.Trim().TrimStart('v', 'V');
        int cut = text.IndexOfAny(new[] { '-', '+' });
        if (cut >= 0)
            text = text.Substring(0, cut);
        if (!Version.TryParse(text, out var parsed))
            return false;
        version = new Version(parsed.Major, parsed.Minor, Math.Max(parsed.Build, 0));
        return true;
    }
}
