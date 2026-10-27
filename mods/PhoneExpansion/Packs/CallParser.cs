using System;
using System.Collections.Generic;
using System.Text;
using NeetLoader.Runtime;
using NeetModKit.IO;
using Newtonsoft.Json.Linq;
using NeetModKit.Rules;
using PhoneExpansion.Calls;

namespace PhoneExpansion.Packs;

/// <summary>
/// Reads a call file: one call object, a list of them, or { "calls": [...] }. Comments are allowed.
/// Every call with a "say" list becomes a generated Ink knot; all knots of a file compile together.
/// </summary>
internal static class CallParser
{
    // Calls end on the game's EndPhoneCall tag, like its own calls, and { "player": "..." } is a
    // line the player says.
    private static readonly StepWriterOptions Writing = new StepWriterOptions
    {
        EndLines = new[] { "# EndPhoneCall" },
        TaggedLines = { ["player"] = "SpeakerAudio:Player" },
    };

    public static void LoadFile(ContentFile file)
    {
        var calls = new List<PhoneCall>();
        var ink = new StringBuilder();
        var objects = file.Items("calls");
        for (int i = 0; i < objects.Count; i++)
        {
            try
            {
                calls.Add(Parse(objects[i], file.ModId, file.Display, file.Scope, ink));
            }
            catch (Exception e) when (e is FormatException || e is InvalidCastException || e is ArgumentException)
            {
                file.Problem($"{ContentFile.Where("call", objects[i], i, objects.Count)}: {e.Message}");
            }
        }

        if (ink.Length > 0)
            file.AddInk(ink.ToString());

        foreach (var call in calls)
        {
            try { PhoneBook.Add(file.Scope, call); }
            catch (ArgumentException e) { file.Problem(e.Message); }
        }
    }

    private static PhoneCall Parse(JObject json, string modId, string source, ITracker scope, StringBuilder ink)
    {
        string number = json["number"]?.ToString();
        string name = json.Value<string>("name");
        string id = json.Value<string>("id")
                    ?? (number != null ? PhoneBook.Digits(number) : null)
                    ?? (name != null ? InkText.Identifier(name).ToLowerInvariant() : null)
                    ?? throw new FormatException("needs an \"id\", a \"number\" or a \"name\".");

        var call = new PhoneCall
        {
            Id = modId + "/" + id,
            Number = number,
            Name = name ?? (number != null ? null : id),
            CallerId = json.Value<string>("callerId"),
            Source = source,
        };

        string knot = "Phone_" + InkText.Identifier(modId) + "_" + InkText.Identifier(id);
        Func<JObject, int> addAction = step => RuleSteps.AddAction(scope, step, () => CallRouter.Active);
        Func<JToken, int> addCondition = when => RuleSteps.AddCondition(scope, when, () => CallRouter.Active);

        if (json["say"] != null)
        {
            ink.Append(StepWriter.Write(knot, json["say"], addAction, addCondition, Writing));
            call.Knot = knot;
        }
        else if (json["ink"] != null)
        {
            call.Knot = json.Value<string>("ink");
        }
        else
        {
            throw new FormatException("needs \"say\" (what's said) or \"ink\" (a knot from an .ink file).");
        }

        if (json["when"] != null)
            call.When = Condition.Parse(json["when"]);

        if (json["otherwise"] != null)
        {
            ink.Append(StepWriter.Write(knot + "_otherwise", json["otherwise"], addAction, addCondition, Writing, "otherwise"));
            call.OtherwiseKnot = knot + "_otherwise";
        }

        if (TriggerRules.IsOn(json["incoming"]))
            call.Incoming = ParseIncoming(json["incoming"]);
        return call;
    }

    private static IncomingRules ParseIncoming(JToken json) =>
        TriggerRules.Parse(json, IncomingRules.Triggers, IncomingRules.Defaults);
}
