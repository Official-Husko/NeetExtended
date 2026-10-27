using NeetModKit.Conversations;

namespace NeetModKit.Rules;

/// <summary>
/// Ink functions for the rules, bound to the dialogue and the email story. RuleIf and RuleDo are
/// what <see cref="StepWriter"/> writes; the rest are for hand-written scripts:
/// <c>~ Act("deliver", "item=Pizza; after=30")</c>, <c>{Check("money", "&gt;= 20"): ...}</c>,
/// <c>{Flag("met_ghost")}</c>, <c>~ SetFlag("met_ghost", 1)</c>, <c>~ AddFlag("visits", 1)</c>,
/// <c>{RuleText("money", "")}</c>.
/// They run for <see cref="RuleContext.Active"/>.
/// </summary>
internal static class RuleInkFunctions
{
    public static void Register(NeetLoader.NeetMod kit)
    {
        InkFunctions.Register(kit, "RuleIf", new[] { "id" }, a => RuleSteps.Test(InkValues.Int(a, 0)), lookaheadSafe: true);
        InkFunctions.Register(kit, "RuleDo", new[] { "id" }, a => Run(() => RuleSteps.Run(InkValues.Int(a, 0))));

        InkFunctions.Register(kit, "Act", new[] { "action", "values" }, a =>
        {
            string name = InkValues.Str(a, 0);
            return Run(() =>
            {
                if (!RuleSteps.Muted)
                    RuleActions.Run(name, RuleActions.ParseText(name, InkValues.Str(a, 1)), RuleContext.Active);
            });
        });
        InkFunctions.Register(kit, "Check", new[] { "condition", "value" }, a =>
            RuleConditions.Check(InkValues.Str(a, 0), RuleActions.Literal(InkValues.Str(a, 1)), RuleContext.Active), lookaheadSafe: true);

        InkFunctions.Register(kit, "RuleText", new[] { "name", "arg" }, a => RuleTexts.Read(InkValues.Str(a, 0), InkValues.Str(a, 1)), lookaheadSafe: true);

        InkFunctions.Register(kit, "Flag", new[] { "name" }, a => ModFlags.Get(InkValues.Str(a, 0)), lookaheadSafe: true);
        InkFunctions.Register(kit, "SetFlag", new[] { "name", "value" }, a => Run(() =>
        {
            if (!RuleSteps.Muted)
                ModFlags.Set(InkValues.Str(a, 0), InkValues.Int(a, 1));
        }));
        InkFunctions.Register(kit, "AddFlag", new[] { "name", "amount" }, a => Run(() =>
        {
            if (!RuleSteps.Muted)
                ModFlags.Add(InkValues.Str(a, 0), InkValues.Int(a, 1));
        }));
    }

    /// <summary>Ink functions used as statements still need to leave something on Ink's stack.</summary>
    private static object Run(System.Action action)
    {
        action();
        return null;
    }
}
