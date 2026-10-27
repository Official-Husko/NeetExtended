using System;
using System.Collections.Generic;
using NeetLoader.Runtime;
using NeetModKit.Ui;
using NeetModKit.Ui.Toolkit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace EmailExpansion.Reader;

/// <summary>
/// Shows a modded email in the email app: the game's body text box has no room for long emails or
/// replies, so modded emails get a scrolling copy of it in the same spot, with reply buttons copied
/// from the app's own button. The game's own emails still use the game's text box.
/// </summary>
internal sealed class ReaderView
{
    private const string ButtonPath = "ToS/Button";

    private readonly GameObject _root;
    private readonly GameObject _gameBody;
    private readonly TextMeshProUGUI _text;
    private readonly ScrollRect _scroll;
    private readonly UiBuilder _replies;
    private readonly GameObject _buttonTemplate;
    private readonly float _buttonHeight;

    private ReaderView(ITracker owner, EmailApp app)
    {
        var body = app.emailBody;
        var bodyRect = body.rectTransform;
        _gameBody = body.gameObject;

        _root = new GameObject("NeetMailReader", typeof(RectTransform));
        var rect = (RectTransform)_root.transform;
        rect.SetParent(bodyRect.parent, false);
        rect.SetSiblingIndex(bodyRect.GetSiblingIndex() + 1);
        rect.anchorMin = bodyRect.anchorMin;
        rect.anchorMax = bodyRect.anchorMax;
        rect.pivot = bodyRect.pivot;
        rect.anchoredPosition = bodyRect.anchoredPosition;
        rect.sizeDelta = bodyRect.sizeDelta;
        var gameBody = _gameBody;
        owner.Track(Disposable.Create(() =>
        {
            UiCloning.Remove(_root);
            if (gameBody != null)
                gameBody.SetActive(true);
        }));

        // The inbox list's own scrollbar, so both parts of the app look the same.
        var inboxBar = app.emailContainer != null ? app.emailContainer.GetComponentInParent<ScrollRect>()?.verticalScrollbar : null;
        var ui = UiBuilder.Vertical(rect, 0f, theme: UiThemes.Computer);
        var list = ui.Scroll(out _scroll, 1f, 1.5f, inboxBar != null ? inboxBar.gameObject : null);
        _scroll.scrollSensitivity = 4f;

        _text = Object.Instantiate(_gameBody, list.Root, false).GetComponent<TextMeshProUGUI>();
        _text.name = "Text";
        _text.gameObject.SetActive(true);
        _text.enableWordWrapping = true;
        _text.overflowMode = TextOverflowModes.Overflow;
        _text.enableAutoSizing = false;
        _text.raycastTarget = false;
        UiBuilder.Size(_text, flexibleWidth: 1f);

        _replies = list.Column(spacing: 1f);
        var button = bodyRect.parent.Find(ButtonPath);
        _buttonTemplate = button != null ? button.gameObject : null;
        _buttonHeight = button != null ? ((RectTransform)button).rect.height : 5f;
    }

    /// <summary>The view for <paramref name="app"/>, built the first time it's needed.</summary>
    public static ReaderView For(ITracker owner, EmailApp app, ReaderView current) =>
        current != null && current._root != null && current._gameBody != null && current._gameBody.transform.parent == app.emailBody.transform.parent
            ? current
            : new ReaderView(owner, app);

    /// <summary>Shows <paramref name="text"/> and a reply button per choice.</summary>
    public void Show(string text, IReadOnlyList<string> choices, Action<int> onReply, bool toBottom)
    {
        _gameBody.SetActive(false);
        _root.SetActive(true);
        _text.text = text;

        _replies.Clear();
        for (int i = 0; i < choices.Count; i++)
            AddReply(choices[i], i, onReply);

        Canvas.ForceUpdateCanvases();
        _scroll.verticalNormalizedPosition = toBottom ? 0f : 1f;
    }

    /// <summary>Back to the game's own text box, for the game's emails.</summary>
    public void Hide()
    {
        if (_root != null)
            _root.SetActive(false);
        if (_gameBody != null)
            _gameBody.SetActive(true);
    }

    private void AddReply(string text, int index, Action<int> onReply)
    {
        if (_buttonTemplate == null)
        {
            _replies.Button(text, () => Pick(index, onReply), -1f);
            return;
        }
        var go = Object.Instantiate(_buttonTemplate, _replies.Root, false);
        go.name = "Reply";
        go.SetActive(true);
        // It opened the terms-of-service popup; replies only need the look.
        var command = go.GetComponent<Computer_Button>();
        if (command != null)
            Object.Destroy(command);
        var button = UiCloning.SetClick(go.GetComponent<Button>(), () => Pick(index, onReply));
        UiBuilder.Size(button, height: _buttonHeight, flexibleWidth: 1f);
        FitLabel(go, text);
    }

    private void Pick(int index, Action<int> onReply)
    {
        var computer = _root != null ? _root.GetComponentInParent<Computer>() : null;
        if (computer != null)
            computer.PlayClickSound();
        onReply(index);
    }

    private static void FitLabel(GameObject button, string text)
    {
        var legacy = button.GetComponentInChildren<Text>(true);
        if (legacy != null)
        {
            legacy.text = text;
            legacy.alignment = TextAnchor.MiddleLeft;
            legacy.horizontalOverflow = HorizontalWrapMode.Wrap;
            legacy.resizeTextForBestFit = true;
            legacy.resizeTextMinSize = 1;
            legacy.resizeTextMaxSize = Math.Max(legacy.fontSize, 1);
            return;
        }
        UiCloning.SetText(button.transform, text);
    }
}
