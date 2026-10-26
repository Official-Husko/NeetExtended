using System;
using NeetModKit.Events;
using NeetModKit.Menu;
using NeetModKit.Ui.Toolkit;
using UnityEngine.UI;

namespace CheatMenu.Pages;

/// <summary>Shared bits of every cheat page: the "only in the house" guard and a status line.</summary>
internal abstract class CheatPage : UiPage
{
    private Text _status;

    public sealed override void Build(UiBuilder ui)
    {
        _status = ui.Text("", MenuPalette.Muted);
        var page = ui.Scroll(out _);
        if (!GameEvents.IsHouseReady)
        {
            page.Text("Cheats work once you're in the house.");
            return;
        }
        BuildPage(page);
    }

    public sealed override void Refresh()
    {
        if (GameEvents.IsHouseReady)
            RefreshPage();
    }

    protected abstract void BuildPage(UiBuilder ui);

    protected virtual void RefreshPage() { }

    /// <summary>Runs a cheat and shows what happened instead of throwing into the log.</summary>
    protected void Run(Action cheat, string success = null)
    {
        try
        {
            cheat();
            if (success != null && _status != null)
                _status.text = MenuPalette.Paint(success, MenuPalette.Good);
        }
        catch (Exception e)
        {
            if (_status != null)
                _status.text = MenuPalette.Paint("Didn't work: " + e.GetBaseException().Message, MenuPalette.Bad);
        }
    }
}
