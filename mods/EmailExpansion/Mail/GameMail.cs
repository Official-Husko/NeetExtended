using HarmonyLib;
using NeetModKit.Game;

namespace EmailExpansion.Mail;

/// <summary>
/// Sends the game's unused emails when they make sense: the clone's hate mail ("Your own clone.")
/// after trading soul for a clone, and the tax reminder with the first tax bill. Both once per save.
/// </summary>
[HarmonyPatch]
internal static class GameMail
{
    private const string CloneMail = "EmailExpansion/game-hate-you";
    private const string TaxMail = "EmailExpansion/game-tax-reminder";
    private const float CloneDelay = 90f;
    private const float TaxDelay = 20f;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Dialogue), nameof(Dialogue.TradeSoulForClone))]
    private static void BeforeTrade(out float __state)
    {
        var soul = GameRefs.PlayerStats != null ? PlayerControl.FindStat("Soul") : null;
        __state = soul != null ? soul.GetVal() : 0f;
    }

    // The trade only happens with more than 20 soul; it takes 20.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Dialogue), nameof(Dialogue.TradeSoulForClone))]
    private static void AfterTrade(float __state)
    {
        var soul = GameRefs.PlayerStats != null ? PlayerControl.FindStat("Soul") : null;
        if (soul != null && soul.GetVal() < __state)
            SendOnce(CloneMail, CloneDelay);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(TaxHandler), "SpawnTaxBill")]
    private static void AfterTaxBill() => SendOnce(TaxMail, TaxDelay);

    private static void SendOnce(string id, float delay)
    {
        var mail = MailBook.Find(id);
        if (mail != null && MailMemory.TimesSent(id) == 0 && !Mailer.IsWaiting(mail))
            Mailer.Send(mail, delay);
    }
}
