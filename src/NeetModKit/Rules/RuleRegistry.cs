using System;

namespace NeetModKit.Rules;

/// <summary>
/// Tells content packs when a condition or action comes or goes. Conditions are compiled when a
/// file loads, so files that use another mod's conditions load again after that mod reloads.
/// </summary>
internal static class RuleRegistry
{
    public static event Action Changed;

    public static void Raise() => Changed?.Invoke();
}
