using System.Collections;
using DevBridge.Server;
using NeetModKit.Desktop;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace DevBridge.Commands;

internal static class ComputerCommands
{
    [BridgeCommand("computer.view", "Turn the computer on, open a site and stand the player in front of it.",
        Params = new[] { "site?: URL name, e.g. anime, pun, stonk (default: keep current)" })]
    private static object View(JObject p)
    {
        ComputerControl.PowerOn();
        if (p["site"] != null)
            ComputerControl.OpenSite(p.Str("site"));
        ComputerControl.StandInFront();

        // The first frame after a teleport can still use the old pose; aim again once it settled.
        var done = new Deferred();
        DevBridgeMod.Instance.StartCoroutine(AimAgain(done));
        return done;
    }

    [BridgeCommand("computer.wake", "Hide the computer's screensaver.")]
    private static object Wake(JObject p)
    {
        ComputerControl.WakeScreen();
        return "awake";
    }

    private static IEnumerator AimAgain(Deferred done)
    {
        yield return null;
        yield return null;
        ComputerControl.StandInFront();
        done.Complete(new JArray(ComputerControl.SiteUrls));
    }
}
