using System.Collections.Generic;
using NeetModKit.Events;
using NeetModKit.Menu;
using NeetModKit.Ui.Toolkit;
using PhoneExpansion.Calls;
using PhoneExpansion.Packs;
using UnityEngine.UI;

namespace PhoneExpansion.Menu;

/// <summary>Mod menu tab listing every modded call, with a button to ring each one.</summary>
internal sealed class PhonePage : MenuPage
{
    private Text _status;

    public override string Title => "Phone";
    public override int Order => 30;

    public override void Build(UiBuilder ui)
    {
        ui.Text(MenuPalette.Paint(
            "Calls added by mods. Dial a number on either phone in the house, or ring a call to try it. " +
            "Call files live in each mod's phone folder and load again as soon as you save them.", MenuPalette.Muted));

        var top = ui.Row(8f, 26f);
        top.Button("RELOAD CALLS", () =>
        {
            PackLoader.Reload();
            Rebuild();
        }, 180f);
        top.Button("HANG UP", () => Report(HangUp()), 140f);
        _status = top.Text("");

        var problems = PackLoader.Problems();
        if (problems.Count > 0)
            BuildProblems(ui.Panel(), problems);

        var list = ui.Scroll(out _);
        if (PhoneBook.All.Count == 0)
            list.Text("No calls loaded.");
        foreach (var call in PhoneBook.All)
            BuildCall(list.Panel(), call);
    }

    private static void BuildProblems(UiBuilder panel, List<string> problems)
    {
        panel.SubHeader(MenuPalette.Paint($"PROBLEMS ({problems.Count})", MenuPalette.Bad));
        foreach (string problem in problems)
            panel.Text(MenuPalette.Paint(problem, MenuPalette.Bad));
    }

    private void BuildCall(UiBuilder panel, PhoneCall call)
    {
        var top = panel.Row(10f, 26f);
        top.SubHeader((call.Number != null ? call.DisplayNumber + "   " : "") + (call.Name ?? call.Id).ToUpperInvariant());
        top.Text(MenuPalette.Paint(call.Source ?? call.Id, MenuPalette.Muted));
        panel.Text(Describe(call));

        var row = panel.Row(8f, 26f);
        row.Button("RING NOW", () => Report(Ring(call)), 140f);
    }

    private static string Describe(PhoneCall call)
    {
        var parts = new List<string>();
        if (call.Number != null)
            parts.Add(call.ReplacesGameNumber ? $"Answers the game's {call.DisplayNumber}" + (call.When != null ? " when its conditions hold" : "")
                                              : "Dial " + call.DisplayNumber + (call.When != null ? " (only picks up sometimes)" : ""));
        if (call.Incoming != null)
            parts.Add(call.Incoming.Describe("Rings"));
        int times = PhoneMemory.TimesPlayed(call.Id);
        parts.Add(times == 0 ? "not heard yet" : times == 1 ? "heard once" : $"heard {times} times");
        return string.Join(". ", parts) + ".";
    }

    private static string Ring(PhoneCall call)
    {
        if (!GameEvents.IsHouseReady)
            return "Phones only ring in the house.";
        bool idle = PhoneLine.IsIdle;
        PhoneRing.Ring(call);
        return idle ? $"Ringing: {call.Name ?? call.Id}. Go pick up!" : "The line's busy; it'll ring when it's free.";
    }

    private static string HangUp()
    {
        if (CallRouter.Active == null)
            return "No modded call in progress.";
        CallRouter.HangUpActive();
        return "Hung up.";
    }

    private void Report(string message)
    {
        if (_status != null)
            _status.text = message;
    }
}
