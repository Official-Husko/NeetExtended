using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NeetLoader.Runtime;
using NeetModKit.Conversations;
using Newtonsoft.Json.Linq;
using PhoneExpansion.Calls;
using PhoneExpansion.Rules;
using PhoneExpansion.Script;

namespace PhoneExpansion.Packs;

/// <summary>
/// Reads a call file: one call object, a list of them, or { "calls": [...] }. Comments are allowed.
/// Every call with a "say" list becomes a generated Ink knot; all knots of a file compile together.
/// </summary>
internal static class CallParser
{
    private static readonly JsonLoadSettings Settings = new JsonLoadSettings { CommentHandling = CommentHandling.Ignore };

    public static void LoadFile(string path, string displayName, string modId, ITracker scope, List<string> problems, List<InkScript> scripts)
    {
        JToken root;
        try
        {
            root = JToken.Parse(File.ReadAllText(path), Settings);
        }
        catch (Exception e)
        {
            problems.Add($"{displayName}: {e.Message}");
            return;
        }

        var calls = new List<PhoneCall>();
        var ink = new StringBuilder();
        var objects = CallObjects(root);
        for (int i = 0; i < objects.Count; i++)
        {
            var json = objects[i];
            string where = json["id"] != null ? $"call \"{json["id"]}\"" : objects.Count > 1 ? $"call {i + 1}" : "call";
            try
            {
                calls.Add(Parse(json, modId, displayName, scope, ink));
            }
            catch (Exception e) when (e is FormatException || e is InvalidCastException || e is ArgumentException)
            {
                problems.Add($"{displayName}, {where}: {e.Message}");
            }
        }
        if (objects.Count == 0)
            problems.Add($"{displayName}: no calls in this file.");

        if (ink.Length > 0)
            scripts.Add(InkScripts.Add(scope, displayName, ink.ToString()));

        foreach (var call in calls)
        {
            try { PhoneBook.Add(scope, call); }
            catch (ArgumentException e) { problems.Add($"{displayName}: {e.Message}"); }
        }
    }

    private static List<JObject> CallObjects(JToken root)
    {
        var list = new List<JObject>();
        var items = root is JObject single && single["calls"] is JArray wrapped ? wrapped : root as JArray;
        if (items == null)
        {
            if (root is JObject one)
                list.Add(one);
            return list;
        }
        foreach (var item in items)
        {
            if (item is JObject obj)
                list.Add(obj);
        }
        return list;
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
        Func<JObject, int> addAction = step => StepTable.AddAction(scope, step);
        Func<JToken, int> addCondition = when => StepTable.AddCondition(scope, when);

        if (json["say"] != null)
        {
            ink.Append(InkWriter.Write(knot, json["say"], addAction, addCondition));
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
            ink.Append(InkWriter.Write(knot + "_otherwise", json["otherwise"], addAction, addCondition));
            call.OtherwiseKnot = knot + "_otherwise";
        }

        var incoming = json["incoming"];
        if (incoming != null && incoming.Type != JTokenType.Null && !(incoming.Type == JTokenType.Boolean && !incoming.Value<bool>()))
            call.Incoming = ParseIncoming(incoming);
        return call;
    }

    private static IncomingRules ParseIncoming(JToken json)
    {
        var rules = new IncomingRules();
        if (!(json is JObject obj))
            return rules; // "incoming": true

        rules.Trigger = (obj.Value<string>("trigger") ?? IncomingRules.Random).ToLowerInvariant();
        if (rules.Trigger != IncomingRules.Random && rules.Trigger != IncomingRules.Order &&
            rules.Trigger != IncomingRules.Arrive && rules.Trigger != IncomingRules.Manual)
            throw new FormatException($"incoming trigger '{rules.Trigger}' isn't one of random, order, arrive, manual.");

        rules.Weight = obj.Value<float?>("weight") ?? rules.Weight;
        rules.Once = obj.Value<bool?>("once") ?? false;
        rules.CooldownMinutes = obj.Value<float?>("cooldown") ?? 0f;
        rules.DelaySeconds = obj.Value<float?>("delay") ?? rules.DelaySeconds;
        rules.Store = obj.Value<string>("store");
        if (obj["when"] != null)
            rules.When = Condition.Parse(obj["when"]);
        return rules;
    }
}
