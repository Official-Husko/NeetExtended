using NeetModKit.Events;
using NeetModKit.Game;
using NeetModKit.Rules;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UpdateChecker.Check;

namespace UpdateChecker.Notice;

/// <summary>
/// Tells the player about an update in the story: the first time the house is ready in a launch,
/// then once per hour of play. Each time it sends the update email if this save hasn't had it,
/// otherwise rings the update call, up to twice. So a playthrough sees at most one email and two
/// calls. Only the game that runs the house does it (in co-op, the host). The email and the call
/// go through the shared "email" and "ring" actions, so neither mod is needed to load this one.
/// </summary>
internal static class UpdateNotice
{
    public const string EmailId = "UpdateChecker/update";
    public const string CallId = "UpdateChecker/update";
    public const int MaxCalls = 2;

    // Per save (ModFlags).
    internal const string EmailFlag = "updatechecker_email";
    internal const string CallsFlag = "updatechecker_calls";
    internal const string HourFlag = "updatechecker_hour";

    private const float HourSeconds = 3600f;
    private const float CheckEvery = 5f;

    private static bool _toldThisLaunch;
    private static float _nextCheck;

    public static void Init(UpdateCheckerMod mod)
    {
        GameEvents.WhenHouseReady(mod, TellFirstTime);
        // The check can finish after the house is ready.
        UpdateState.Subscribe(mod, () =>
        {
            if (GameEvents.IsHouseReady)
                TellFirstTime();
        });
    }

    /// <summary>The hourly part. Looks once every few seconds.</summary>
    public static void Tick()
    {
        if (Time.time < _nextCheck)
            return;
        _nextCheck = Time.time + CheckEvery;
        if (CanTell() && CurrentHour() > ModFlags.Get(HourFlag))
            Tell();
    }

    private static void TellFirstTime()
    {
        if (_toldThisLaunch || !CanTell())
            return;
        _toldThisLaunch = true;
        Tell();
    }

    internal static bool CanTell() =>
        UpdateState.Available
        && UpdateCheckerMod.Config.Value.TellInGame
        && GameEvents.IsHouseReady
        && WorldRole.RunsWorld;

    private static int CurrentHour() => Mathf.FloorToInt(ModFlags.PlaySeconds / HourSeconds);

    /// <summary>The email if this save hasn't had it, else the call if it rang fewer than twice. Returns what it did.</summary>
    internal static string Tell()
    {
        ModFlags.Set(HourFlag, CurrentHour());

        if (ModFlags.Get(EmailFlag) == 0 && RuleActions.Exists("email"))
        {
            ModFlags.Set(EmailFlag, 1);
            RuleActions.Run("email", new JObject { ["email"] = EmailId, ["after"] = 5 }, null);
            return "email";
        }
        if (ModFlags.Get(CallsFlag) < MaxCalls && RuleActions.Exists("ring"))
        {
            ModFlags.Add(CallsFlag, 1);
            RuleActions.Run("ring", new JObject { ["call"] = CallId, ["after"] = 3 }, null);
            return "call";
        }
        return "nothing";
    }
}
