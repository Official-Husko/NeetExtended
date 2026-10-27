using NeetModKit.Conversations;
using NeetModKit.Rules;
using PhoneExpansion.Calls;

namespace PhoneExpansion.Script;

/// <summary>
/// The names .ink calls used before the rules were shared: PhoneAct, PhoneCheck, PhoneFlag,
/// SetPhoneFlag and AddPhoneFlag. They do the same as the framework's Act, Check, Flag, SetFlag and
/// AddFlag (which new scripts should use), always for the call in progress.
/// </summary>
internal static class PhoneInkFunctions
{
    public static void Register(PhoneExpansionMod mod)
    {
        InkFunctions.Register(mod, "PhoneAct", new[] { "action", "values" }, a =>
        {
            string name = InkValues.Str(a, 0);
            return Run(() => RuleActions.Run(name, RuleActions.ParseText(name, InkValues.Str(a, 1)), CallRouter.Active));
        });
        InkFunctions.Register(mod, "PhoneCheck", new[] { "condition", "value" }, a =>
            RuleConditions.Check(InkValues.Str(a, 0), RuleActions.Literal(InkValues.Str(a, 1)), CallRouter.Active), lookaheadSafe: true);
        InkFunctions.Register(mod, "PhoneFlag", new[] { "name" }, a => ModFlags.Get(InkValues.Str(a, 0)), lookaheadSafe: true);
        InkFunctions.Register(mod, "SetPhoneFlag", new[] { "name", "value" }, a => Run(() => ModFlags.Set(InkValues.Str(a, 0), InkValues.Int(a, 1))));
        InkFunctions.Register(mod, "AddPhoneFlag", new[] { "name", "amount" }, a => Run(() => ModFlags.Add(InkValues.Str(a, 0), InkValues.Int(a, 1))));
    }

    /// <summary>Ink functions used as statements still need to leave something on Ink's stack.</summary>
    private static object Run(System.Action action)
    {
        action();
        return null;
    }
}
