using HarmonyLib;

namespace NeetModKit.Game;

/// <summary>
/// The player's cash, which lives on the in-game computer. Going through <see cref="Computer.Money"/>
/// adds 12% of every increase to the tax bill; <see cref="SetUntaxed"/> skips that.
/// </summary>
public static class Money
{
    private static readonly AccessTools.FieldRef<Computer, float> MoneyField =
        AccessTools.FieldRefAccess<Computer, float>("money");

    public static float Get() => GameRefs.Computer != null ? GameRefs.Computer.Money : 0f;

    /// <summary>Same as the game does it: listeners fire and increases get taxed.</summary>
    public static void Set(float value)
    {
        var computer = Require();
        computer.Money = value;
    }

    /// <summary>Changes cash and refreshes the UI without triggering tax or achievement tracking.</summary>
    public static void SetUntaxed(float value)
    {
        var computer = Require();
        float previous = MoneyField(computer);
        MoneyField(computer) = value;
        computer.UpdateAppCashText(previous, value);
    }

    public static void Add(float amount, bool taxed = true)
    {
        if (taxed)
            Set(Get() + amount);
        else
            SetUntaxed(Get() + amount);
    }

    private static Computer Require() =>
        GameRefs.Computer ?? throw new System.InvalidOperationException("The computer isn't loaded (not in the house).");
}
