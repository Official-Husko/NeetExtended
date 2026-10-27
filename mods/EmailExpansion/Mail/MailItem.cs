using NeetModKit.Rules;

namespace EmailExpansion.Mail;

/// <summary>
/// One email: a subject for the inbox and an Ink knot in the email story with the body (written
/// by hand or generated from an email file's "body" list).
/// </summary>
public sealed class MailItem : IRuleSubject
{
    /// <summary>Unique id, "ModId/name" for emails from files.</summary>
    public string Id;

    /// <summary>What the inbox shows.</summary>
    public string Subject;

    /// <summary>Knot in the email story (<c>EmailHandler._.masterStory</c>).</summary>
    public string Knot;

    /// <summary>When it arrives by itself; null for emails only something else sends.</summary>
    public SendRules Send;

    /// <summary>Where it came from (a file path or a mod name), for messages.</summary>
    public string Source;

    string IRuleSubject.Id => Id;

    int IRuleSubject.Times => MailMemory.TimesSent(Id);

    public override string ToString() => Id;
}

/// <summary>
/// When an email arrives by itself: "random" (every few minutes, one of the emails that can
/// arrive), "order", "arrive", "wake" or "manual". Emails arrive once per save unless "once" is false.
/// </summary>
public sealed class SendRules : TriggerRules
{
    public static readonly string[] Triggers = { Random, Order, Arrive, Wake, Manual };

    internal static readonly SendRules Defaults = new SendRules { Once = true };
}
