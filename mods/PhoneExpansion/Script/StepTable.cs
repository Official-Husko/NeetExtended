using System.Collections.Generic;
using NeetLoader.Runtime;
using Newtonsoft.Json.Linq;
using PhoneExpansion.Calls;
using PhoneExpansion.Rules;

namespace PhoneExpansion.Script;

/// <summary>
/// Conditions and actions from call files, numbered so the generated Ink can refer to them with
/// PhoneIf(n) and PhoneDo(n). Entries belong to the call file they came from and go away with it.
/// </summary>
internal static class StepTable
{
    private sealed class ActionStep
    {
        public string Name;
        public JObject Values;
    }

    private static readonly Dictionary<int, Condition> Conditions = new Dictionary<int, Condition>();
    private static readonly Dictionary<int, ActionStep> Actions = new Dictionary<int, ActionStep>();
    private static int _next;

    public static int AddCondition(ITracker owner, JToken json)
    {
        var condition = Condition.Parse(json);
        int id = ++_next;
        Conditions[id] = condition;
        owner.Track(Disposable.Create(() => Conditions.Remove(id)));
        return id;
    }

    /// <summary>Takes a { "do": name, ...values } step. Unknown actions are reported now, not mid-call.</summary>
    public static int AddAction(ITracker owner, JObject step)
    {
        string name = step.Value<string>("do");
        if (!PhoneActions.Exists(name))
            throw new System.FormatException($"unknown action '{name}'.");
        var values = (JObject)step.DeepClone();
        values.Remove("do");
        int id = ++_next;
        Actions[id] = new ActionStep { Name = name, Values = values };
        owner.Track(Disposable.Create(() => Actions.Remove(id)));
        return id;
    }

    public static bool Test(int id) => Conditions.TryGetValue(id, out var condition) && condition.Test(CallRouter.Active);

    public static void Run(int id)
    {
        if (Actions.TryGetValue(id, out var action))
            PhoneActions.Run(action.Name, action.Values, CallRouter.Active);
    }
}
