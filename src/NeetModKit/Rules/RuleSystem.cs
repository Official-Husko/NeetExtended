namespace NeetModKit.Rules;

/// <summary>Sets up the shared rules: flags, the built-in conditions, actions and text values, and their Ink functions and console commands.</summary>
internal static class RuleSystem
{
    public static void Init(NeetLoader.NeetMod kit)
    {
        ModFlags.Init(kit);
        BuiltinConditions.Register(kit);
        BuiltinActions.Register(kit);
        BuiltinTexts.Register(kit);
        WeatherRules.Register(kit);
        RuleInkFunctions.Register(kit);
        FlagCommands.Register(kit);
    }
}
