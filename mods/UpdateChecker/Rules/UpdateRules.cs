using Newtonsoft.Json.Linq;
using NeetModKit.Rules;
using UpdateChecker.Check;

namespace UpdateChecker.Rules;

/// <summary>What the update email and call can say and check.</summary>
internal static class UpdateRules
{
    public static void Register(UpdateCheckerMod mod)
    {
        RuleTexts.Register(mod, "updateVersion", _ => UpdateState.Latest ?? "new",
            "The newest version found, without the v: v{{updateVersion}}.");
        RuleConditions.Register(mod, "updateAvailable", value =>
        {
            bool want = value.Value<bool>();
            return _ => UpdateState.Available == want;
        }, "true while a newer version than the one running is out (false: it isn't).");
    }
}
