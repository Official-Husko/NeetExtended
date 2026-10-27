using System.Collections.Generic;
using EmailExpansion.Mail;
using NeetModKit.Game;
using Newtonsoft.Json.Linq;

namespace EmailExpansion.Reader;

/// <summary>
/// Where each email conversation stands, kept in the framework's SharedState so every co-op player
/// sees the host's, and the two things players do to one (open it, reply), which run where the house
/// runs. In single player all of it simply happens here.
/// </summary>
internal static class SharedLetters
{
    private const string StatePrefix = "EmailExpansion.Letter.";
    private const string OpenAction = "EmailExpansion.Open";
    private const string ReplyAction = "EmailExpansion.Reply";

    internal sealed class View
    {
        public string Text = "";
        public string[] Choices = new string[0];
        public bool Done;
    }

    public static void Init(EmailExpansionMod mod)
    {
        HostActions.Register(mod, OpenAction, (args, player) =>
        {
            if (Resolve(args, out string key, out var mail))
                LetterRunner.Open(key, mail);
        });
        HostActions.Register(mod, ReplyAction, (args, player) =>
        {
            if (Resolve(args, out string key, out var mail))
                LetterRunner.Reply(key, mail, args.Value<int>("index"));
        });
    }

    public static void Publish(string key, MailMemory.Letter letter, List<string> choices) =>
        SharedState.Publish(StatePrefix + key, new JObject
        {
            ["text"] = letter.Text ?? "",
            ["choices"] = letter.Done ? new JArray() : new JArray(choices),
            ["done"] = letter.Done,
        });

    public static bool TryGet(string key, out View view)
    {
        view = null;
        if (!SharedState.TryGet(StatePrefix + key, out var state) || !(state is JObject json))
            return false;
        view = new View
        {
            Text = json.Value<string>("text") ?? "",
            Choices = (json["choices"] as JArray)?.ToObject<string[]>() ?? new string[0],
            Done = json.Value<bool>("done"),
        };
        return true;
    }

    /// <summary>The inbox entry a SharedState key belongs to, if it's one of ours.</summary>
    public static bool IsLetter(string stateKey, out string key)
    {
        bool ours = stateKey.StartsWith(StatePrefix, System.StringComparison.Ordinal);
        key = ours ? stateKey.Substring(StatePrefix.Length) : null;
        return ours;
    }

    public static void Open(string key, MailItem mail) =>
        HostActions.Run(OpenAction, new JObject { ["key"] = key, ["knot"] = mail.Knot });

    public static void Reply(string key, MailItem mail, int index) =>
        HostActions.Run(ReplyAction, new JObject { ["key"] = key, ["knot"] = mail.Knot, ["index"] = index });

    private static bool Resolve(JToken args, out string key, out MailItem mail)
    {
        key = args?.Value<string>("key");
        mail = MailBook.ByKnot(args?.Value<string>("knot"));
        return key != null && mail != null;
    }
}
