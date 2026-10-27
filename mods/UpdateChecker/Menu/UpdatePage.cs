using NeetModKit.Menu;
using NeetModKit.Ui.Toolkit;
using UnityEngine;
using UnityEngine.UI;
using UpdateChecker.Check;

namespace UpdateChecker.Menu;

/// <summary>Mod menu tab: the running version, the newest one found and where to get it.</summary>
internal sealed class UpdatePage : MenuPage
{
    private readonly UpdateCheckerMod _mod;
    private Text _current;
    private Text _latest;
    private Text _url;
    private Text _status;

    public UpdatePage(UpdateCheckerMod mod) => _mod = mod;

    public override string Title => "Updates";
    public override int Order => 35;

    public override void Build(UiBuilder ui)
    {
        ui.Text(MenuPalette.Paint(
            "Asks GitHub for the newest release when the game starts and then every hour (switch it off in Settings). " +
            "To update, get the new release from the page below, the same way you got these mods.", MenuPalette.Muted));

        var panel = ui.Panel();
        _current = panel.Text("");
        _latest = panel.Text("");
        _url = panel.Text("");
        _status = panel.Text("");

        var buttons = ui.Row(8f, 26f);
        buttons.Button("CHECK NOW", () => CheckSchedule.CheckNow(_mod), 160f);
        buttons.Button("COPY LINK", CopyLink, 160f);
        Refresh();
    }

    public override void Refresh()
    {
        if (_current == null)
            return;
        _current.text = "This version: v" + UpdateState.Current;
        _latest.text = UpdateState.Latest == null
            ? "Newest version found: none yet"
            : "Newest version found: v" + UpdateState.Latest + (UpdateState.Available ? " (newer than yours)" : "");
        _url.text = "Release page: " + (UpdateState.Url ?? ReleaseCheck.ReleasesPage);
        _status.text = UpdateState.Status;
    }

    private static void CopyLink() => GUIUtility.systemCopyBuffer = UpdateState.Url ?? ReleaseCheck.ReleasesPage;
}
