using System;
using NeetLoader;
using NeetModKit.Desktop;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CardExpansion.UI;

/// <summary>
/// A second row under the card site's header with the Summon and Auto-click buttons and a status
/// line. The card grid is moved down to make room.
/// </summary>
internal sealed class CardSiteUi
{
    private const string SiteUrl = "anime";
    private const float RowHeight = 15f;
    private const float RowY = -19.5f;

    private Button _summon;
    private Button _auto;
    private TMP_Text _status;

    public bool Build(NeetMod owner, Action onSummon, Action onAuto)
    {
        var site = ComputerControl.Site(SiteUrl);
        var redeem = site != null ? site.transform.Find("RedeemButton") : null;
        var meter = site != null ? site.transform.Find("CrystalMeter") : null;
        var cards = site != null ? site.transform.Find("Cards") as RectTransform : null;
        if (redeem == null || meter == null || cards == null)
            return false;

        ComputerUi.PushTopEdge(owner, cards, RowHeight);

        var buttonSize = new Vector2(30f, 12f);
        _summon = ComputerUi.CloneButton(owner, redeem, "CardExpansion_Summon", "", onSummon);
        ComputerUi.Place(_summon, new Vector2(-70f, RowY), buttonSize);

        _auto = ComputerUi.CloneButton(owner, redeem, "CardExpansion_Auto", "", onAuto);
        ComputerUi.Place(_auto, new Vector2(-38f, RowY), buttonSize);

        _status = ComputerUi.CloneLabel(owner, meter, "CardExpansion_Status", "");
        _status.enableAutoSizing = false;
        _status.fontSize = 6f;
        _status.alignment = TextAlignmentOptions.Left;
        var statusRect = (RectTransform)_status.transform;
        statusRect.anchorMin = statusRect.anchorMax = statusRect.pivot = new Vector2(0.5f, 1f);
        ComputerUi.Place(_status, new Vector2(28f, RowY + 1f), new Vector2(84f, 12f));
        return true;
    }

    public void SetLabels(string summon, string auto)
    {
        if (_summon != null)
            ComputerUi.SetText(_summon.transform, summon);
        if (_auto != null)
            ComputerUi.SetText(_auto.transform, auto);
    }

    public void SetStatus(string text)
    {
        if (_status != null)
            _status.text = text;
    }
}
