using System;
using System.Collections.Generic;
using NeetLoader.Runtime;
using Newtonsoft.Json.Linq;

namespace NeetModKit.Rules;

/// <summary>
/// Conditions and actions from content files, numbered so generated Ink can refer to them with
/// RuleIf(n) and RuleDo(n) (see <see cref="StepWriter"/>). Each entry belongs to the file it came
/// from and goes away with it, and knows which subject it runs for.
/// </summary>
public static class RuleSteps
{
    private sealed class Step
    {
        public Condition Condition;
        public string Action;
        public JObject Values;
        public Func<IRuleSubject> Subject;
    }

    private static readonly Dictionary<int, Step> Steps = new Dictionary<int, Step>();
    private static int _next;

    /// <summary>While true, RuleDo does nothing: used to replay text that was already shown, without doing its actions again.</summary>
    public static bool Muted { get; set; }

    public static int AddCondition(ITracker owner, JToken json, Func<IRuleSubject> subject) =>
        Add(owner, new Step { Condition = Condition.Parse(json), Subject = subject });

    /// <summary>Takes a { "do": name, ...values } step. Unknown actions are reported now, not while playing.</summary>
    public static int AddAction(ITracker owner, JObject step, Func<IRuleSubject> subject)
    {
        var (name, values) = ActionList.Split(step);
        return Add(owner, new Step { Action = name, Values = values, Subject = subject });
    }

    public static bool Test(int id) =>
        Steps.TryGetValue(id, out var step) && step.Condition != null && step.Condition.Test(step.Subject?.Invoke());

    public static void Run(int id)
    {
        if (!Muted && Steps.TryGetValue(id, out var step) && step.Action != null)
            RuleActions.Run(step.Action, step.Values, step.Subject?.Invoke());
    }

    private static int Add(ITracker owner, Step step)
    {
        int id = ++_next;
        Steps[id] = step;
        owner.Track(Disposable.Create(() => Steps.Remove(id)));
        return id;
    }
}

/// <summary>A list of actions from a content file, checked when it loads and run later: <c>[ { "do": "money", "amount": 50 } ]</c>.</summary>
public sealed class ActionList
{
    private readonly List<(string Name, JObject Values)> _actions = new List<(string, JObject)>();

    public static readonly ActionList Empty = new ActionList();

    public int Count => _actions.Count;

    /// <summary>Takes one { "do": ... } object or a list of them. Throws <see cref="FormatException"/> on unknown actions.</summary>
    public static ActionList Parse(JToken json)
    {
        var list = new ActionList();
        if (json == null || json.Type == JTokenType.Null)
            return list;
        var items = json as JArray ?? new JArray(json);
        for (int i = 0; i < items.Count; i++)
        {
            if (!(items[i] is JObject step))
                throw new FormatException($"[{i}]: an action is an object like {{ \"do\": \"money\", \"amount\": 20 }}.");
            try { list._actions.Add(Split(step)); }
            catch (FormatException e) { throw new FormatException($"[{i}]: {e.Message}"); }
        }
        return list;
    }

    public void Run(IRuleSubject subject)
    {
        foreach (var (name, values) in _actions)
            RuleActions.Run(name, values, subject);
    }

    internal static (string Name, JObject Values) Split(JObject step)
    {
        string name = step.Value<string>("do");
        if (!RuleActions.Exists(name))
            throw new FormatException($"unknown action '{name}'.");
        var values = (JObject)step.DeepClone();
        values.Remove("do");
        return (name, values);
    }
}
