using NeetModKit.Desktop;
using NeetModKit.Ui.Toolkit;
using UnityEngine.UI;

namespace NeetModKit.Menu.Pages;

/// <summary>Every app on the in-game computer, with its URL editable.</summary>
internal sealed class PcAppsPage : MenuPage
{
    private Text _status;

    public override string Title => "PC Apps";
    public override int Order => 20;

    public override void Build(UiBuilder ui)
    {
        ui.Text(MenuPalette.Paint("Websites and apps on the in-game computer. Change an address and type it in the computer's browser; " +
                                  "the original address keeps working too, so the start menu still finds everything.", MenuPalette.Muted));
        _status = ui.Text("");

        var list = ui.Scroll(out _);
        if (ComputerApps.All.Count == 0)
            list.Text("The computer's apps show up here once you've been in the house.");

        foreach (var app in ComputerApps.All)
            BuildApp(list.Panel(), app);
    }

    private void BuildApp(UiBuilder panel, ComputerApp app)
    {
        var top = panel.Row(10f, 26f);
        top.SubHeader(app.Title.ToUpperInvariant());
        top.Text(MenuPalette.Paint(app.BuiltIn ? "from the game" : "added by " + app.Owner.Info.Manifest.DisplayName, MenuPalette.Muted));
        if (!string.IsNullOrEmpty(app.Description))
            panel.Text(MenuPalette.Paint(app.Description, MenuPalette.Muted));

        var row = panel.Row(6f, 26f);
        UiBuilder.Size(row.SubHeader("HTTP://"), width: 70f);
        var input = row.Input(app.Url, _ => { }, app.DefaultUrl, 220f);
        UiBuilder.Size(row.SubHeader(".COM"), width: 50f);
        row.Button("SAVE", () => Report(app, ComputerApps.SetUrl(app, input.text)), 90f);
        row.Button("RESET", () =>
        {
            Report(app, ComputerApps.SetUrl(app, app.DefaultUrl));
            Rebuild();
        }, 90f);
    }

    private void Report(ComputerApp app, string error)
    {
        if (_status == null)
            return;
        _status.text = error == null
            ? MenuPalette.Paint($"{app.Title} is now at http://{app.Url}.com", MenuPalette.Good)
            : MenuPalette.Paint(error, MenuPalette.Bad);
    }
}
