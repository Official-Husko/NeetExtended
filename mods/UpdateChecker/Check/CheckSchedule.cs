using System.Collections;
using UnityEngine;

namespace UpdateChecker.Check;

/// <summary>Checks when the mod loads and then every hour of real time, as long as the setting is on.</summary>
internal static class CheckSchedule
{
    private const float HourSeconds = 3600f;

    public static void Start(UpdateCheckerMod mod) => mod.StartCoroutine(Loop());

    /// <summary>One check now, whatever the setting says (the menu's CHECK NOW button).</summary>
    public static void CheckNow(UpdateCheckerMod mod) => mod.StartCoroutine(ReleaseCheck.Run());

    private static IEnumerator Loop()
    {
        while (true)
        {
            if (UpdateCheckerMod.Config.Value.Check)
                yield return ReleaseCheck.Run();
            yield return new WaitForSecondsRealtime(HourSeconds);
        }
    }
}
