using NeetModKit.Conversations;
using Newtonsoft.Json.Linq;
using PhoneExpansion.Calls;
using PhoneExpansion.Rules;

namespace PhoneExpansion.Script;

/// <summary>
/// Functions calls can use in Ink. PhoneIf and PhoneDo are what call files turn into; the rest are
/// for hand-written .ink calls.
/// </summary>
internal static class PhoneInkFunctions
{
    public static void Register(PhoneExpansionMod mod)
    {
        InkFunctions.Register(mod, "PhoneIf", new[] { "id" }, a => StepTable.Test(InkValues.Int(a, 0)), lookaheadSafe: true);
        InkFunctions.Register(mod, "PhoneDo", new[] { "id" }, a => Run(() => StepTable.Run(InkValues.Int(a, 0))));

        // PhoneAct("deliver", "item=Pizza; after=30") or PhoneAct("money", "-20")
        InkFunctions.Register(mod, "PhoneAct", new[] { "action", "values" }, a =>
        {
            string name = InkValues.Str(a, 0);
            return Run(() => PhoneActions.Run(name, PhoneActions.ParseText(name, InkValues.Str(a, 1)), CallRouter.Active));
        });

        // {PhoneCheck("money", ">= 20"): ...}
        InkFunctions.Register(mod, "PhoneCheck", new[] { "condition", "value" }, a =>
            PhoneConditions.Check(InkValues.Str(a, 0), Value(InkValues.Str(a, 1)), CallRouter.Active), lookaheadSafe: true);

        InkFunctions.Register(mod, "PhoneFlag", new[] { "name" }, a => PhoneMemory.Flag(InkValues.Str(a, 0)), lookaheadSafe: true);
        InkFunctions.Register(mod, "SetPhoneFlag", new[] { "name", "value" }, a => Run(() => PhoneMemory.SetFlag(InkValues.Str(a, 0), InkValues.Int(a, 1))));
        InkFunctions.Register(mod, "AddPhoneFlag", new[] { "name", "amount" }, a => Run(() => PhoneMemory.AddFlag(InkValues.Str(a, 0), InkValues.Int(a, 1))));
    }

    /// <summary>Ink functions used as statements still need to leave something on Ink's stack.</summary>
    private static object Run(System.Action action)
    {
        action();
        return null;
    }

    /// <summary>A number stays a number (so "money", "20" means at least 20); anything else is text.</summary>
    private static JToken Value(string text) =>
        float.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float number)
            ? (JToken)number
            : text;
}
