namespace NeetModKit.Rules;

/// <summary>
/// What conditions and actions run for: a phone call, an email, ... Conditions like "times" read
/// it, and actions can look at it (for example to find another call from the same mod).
/// </summary>
public interface IRuleSubject
{
    /// <summary>Unique id, "ModId/name" for things from files.</summary>
    string Id { get; }

    /// <summary>How many times it happened before (a call played, an email sent).</summary>
    int Times { get; }
}

/// <summary>The subject that's running right now, for actions and checks called from Ink.</summary>
public static class RuleContext
{
    /// <summary>Set by whatever is playing (a call while it's on, an email while it's shown); null otherwise.</summary>
    public static IRuleSubject Active { get; set; }

    /// <summary>The mod part of an id ("PhoneExpansion" in "PhoneExpansion/pizza"), or null.</summary>
    public static string ModOf(IRuleSubject subject)
    {
        string id = subject?.Id;
        int slash = id?.IndexOf('/') ?? -1;
        return slash > 0 ? id.Substring(0, slash) : null;
    }
}
