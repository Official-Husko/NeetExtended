using System;
using NeetLoader;
using NeetLoader.Runtime;
using NeetModKit.Desktop;
using NeetModKit.Ui;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CardExpansion.UI;

/// <summary>
/// A row under the card site's header for the features' buttons, plus a status line. The row (and
/// the room made for it by moving the card grid down) exists while at least one button does.
/// </summary>
internal sealed class CardSiteUi
{
    // Looked up by app id, so it keeps working whatever URL the player gives the site.
    private const string SiteApp = "AnimeClicker";
    private const float RowHeight = 15f;
    private const float RowY = -19.5f;
    private const float FirstX = -70f;
    private const float SlotWidth = 32f;
    private static readonly Vector2 ButtonSize = new Vector2(30f, 12f);

    private readonly NeetMod _mod;
    private GameObject _site;
    private Transform _redeem, _meter;
    private RectTransform _cards;
    private IDisposable _rowSpace;
    private TMP_Text _status;
    private int _buttons;

    public CardSiteUi(NeetMod mod)
    {
        _mod = mod;
    }

    /// <summary>Adds a button in a fixed slot of the row, removed when <paramref name="owner"/> cleans up.</summary>
    public Button AddButton(ITracker owner, int slot, string name, Action onClick)
    {
        if (!Locate())
        {
            _mod.Log.Warn("Couldn't find the card site on the computer.");
            return null;
        }

        if (_buttons++ == 0)
            OpenRow();

        var button = ComputerUi.CloneButton(owner, _redeem, "CardExpansion_" + name, "", onClick);
        ComputerUi.Place(button, new Vector2(FirstX + slot * SlotWidth, RowY), ButtonSize);
        owner.Track(Disposable.Create(() =>
        {
            if (--_buttons == 0)
                CloseRow();
        }));
        return button;
    }

    public void SetStatus(string text)
    {
        if (_status != null)
            _status.text = text;
    }

    /// <summary>Finds the site; a new house load means a new site, so start from scratch.</summary>
    private bool Locate()
    {
        var site = ComputerApps.Find(SiteApp)?.Panel;
        if (site == null)
            return false;
        if (site == _site)
            return true;

        _site = site;
        _redeem = site.transform.Find("RedeemButton");
        _meter = site.transform.Find("CrystalMeter");
        _cards = site.transform.Find("Cards") as RectTransform;
        _rowSpace = null;
        _status = null;
        _buttons = 0;
        return _redeem != null && _meter != null && _cards != null;
    }

    private void OpenRow()
    {
        _rowSpace = ComputerUi.PushTopEdge(_cards, RowHeight);
        _status = ComputerUi.CloneLabel(_mod, _meter, "CardExpansion_Status", "");
        _status.enableAutoSizing = false;
        _status.fontSize = 6f;
        _status.alignment = TextAlignmentOptions.Left;
        var rect = (RectTransform)_status.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 1f);
        ComputerUi.Place(_status, new Vector2(28f, RowY + 1f), new Vector2(84f, 12f));
    }

    private void CloseRow()
    {
        _rowSpace?.Dispose();
        _rowSpace = null;
        if (_status != null)
            UiCloning.Remove(_status.gameObject);
        _status = null;
    }
}
