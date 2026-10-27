using System.Globalization;
using NeetModKit.Game;
using UnityEngine;

namespace NeetModKit.Rules;

/// <summary>The text values every content file can use (see <see cref="RuleTexts"/>).</summary>
internal static class BuiltinTexts
{
    public static void Register(NeetLoader.NeetMod kit)
    {
        RuleTexts.Register(kit, "money", _ => Mathf.FloorToInt(Money.Get()).ToString(CultureInfo.InvariantCulture),
            "The cash on the computer, in whole dollars.");
        RuleTexts.Register(kit, "flag", name => ModFlags.Get(name).ToString(CultureInfo.InvariantCulture),
            "A flag's value: {{flag:name}}.");
    }
}
