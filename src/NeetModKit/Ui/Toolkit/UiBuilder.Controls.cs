using System;
using UnityEngine;
using UnityEngine.UI;

namespace NeetModKit.Ui.Toolkit;

public sealed partial class UiBuilder
{
    private const float ButtonHeight = 26f;
    private const float RowHeight = 22f;

    /// <summary>Big orange header.</summary>
    public Text Header(string text) => Label(UiTemplate.Header, text, TextAnchor.MiddleLeft);

    /// <summary>Small caps label in the game's display font.</summary>
    public Text SubHeader(string text) => Label(UiTemplate.SubHeader, text, TextAnchor.MiddleLeft);

    /// <summary>Wrapping body text (rich text allowed).</summary>
    public Text Text(string text, Color? color = null, int size = 0)
    {
        var label = Label(UiTemplate.Text, text, TextAnchor.UpperLeft);
        if (color.HasValue)
            label.color = color.Value;
        if (size > 0)
            label.fontSize = size;
        return label;
    }

    public Button Button(string label, Action onClick, float width = 140f, bool accent = false)
    {
        var go = Copy(accent ? UiTemplate.AccentButton : UiTemplate.Button, "Button " + label);
        UiCloning.SetText(go.transform, label);
        FitText(go.GetComponentInChildren<Text>());
        var button = go.GetComponent<Button>();
        button.onClick.AddListener(() => onClick());
        Size(go.transform, width: width, height: ButtonHeight, flexibleWidth: width < 0f ? 1f : 0f);
        return button;
    }

    /// <summary>A checkbox with its label on the right.</summary>
    public Toggle Toggle(string label, bool value, Action<bool> onChange)
    {
        var go = Copy(UiTemplate.Toggle, "Toggle " + label);
        var toggle = go.GetComponent<Toggle>();
        toggle.SetIsOnWithoutNotify(value);
        toggle.onValueChanged.AddListener(v => onChange(v));

        // The pause menu's toggle has its label on the left of the box; put it on the right.
        var box = go.transform.Find("Background") as RectTransform;
        if (box != null)
        {
            box.anchorMin = box.anchorMax = box.pivot = new Vector2(0f, 0.5f);
            box.anchoredPosition = Vector2.zero;
        }
        float boxWidth = box != null ? box.sizeDelta.x : 16f;

        var text = go.transform.Find("Label")?.GetComponent<Text>();
        float labelWidth = 0f;
        if (text != null)
        {
            text.text = label;
            text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            var rect = text.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(boxWidth + 8f, 0f);
            rect.offsetMax = Vector2.zero;
            labelWidth = text.preferredWidth;
        }

        Size(go.transform, width: boxWidth + 8f + labelWidth + 4f, height: RowHeight, flexibleWidth: Horizontal ? 0f : 1f);
        return toggle;
    }

    /// <summary>A labelled slider with its value shown on the right.</summary>
    public Slider Slider(string label, float value, float min, float max, Action<float> onChange, bool wholeNumbers = false)
    {
        var row = Row(10f, RowHeight);
        var caption = row.SubHeader(label);
        Size(caption, width: 190f);

        var go = row.Copy(UiTemplate.Slider, "Slider " + label);
        var slider = go.GetComponent<Slider>();
        slider.minValue = min;
        slider.maxValue = max;
        slider.wholeNumbers = wholeNumbers;
        slider.SetValueWithoutNotify(value);
        Size(go.transform, height: 16f, flexibleWidth: 1f);

        var readout = row.Text(Format(value, wholeNumbers));
        Size(readout, width: 60f);
        slider.onValueChanged.AddListener(v =>
        {
            readout.text = Format(v, wholeNumbers);
            onChange(v);
        });
        return slider;
    }

    /// <summary>A single-line text box. <paramref name="onChange"/> fires on every edit.</summary>
    public InputField Input(string value, Action<string> onChange, string placeholder = null, float width = -1f)
    {
        var go = Copy(UiTemplate.Input, "Input");
        var input = go.GetComponent<InputField>();
        input.lineType = InputField.LineType.SingleLine;
        input.SetTextWithoutNotify(value ?? "");
        if (input.placeholder is Text hint)
            hint.text = placeholder ?? "";
        input.onValueChanged.AddListener(v => onChange(v));
        Size(go.transform, width: width, height: RowHeight + 4f, flexibleWidth: width < 0f ? 1f : 0f);
        return input;
    }

    /// <summary>A label on the left and a text box on the right.</summary>
    public InputField LabeledInput(string label, string value, Action<string> onChange)
    {
        var row = Row(10f, RowHeight + 4f);
        Size(row.SubHeader(label), width: 190f);
        return row.Input(value, onChange);
    }

    private Text Label(UiTemplate template, string text, TextAnchor anchor)
    {
        var go = Copy(template, template.ToString());
        var label = go.GetComponent<Text>();
        label.text = text;
        label.alignment = anchor;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        Size(go.transform, flexibleWidth: 1f);
        return label;
    }

    /// <summary>Shrinks a label to fit its box instead of spilling out of it.</summary>
    private static void FitText(Text text)
    {
        if (text == null)
            return;
        text.resizeTextForBestFit = true;
        text.resizeTextMaxSize = text.fontSize;
        text.resizeTextMinSize = Math.Max(6, text.fontSize / 2);
    }

    private static string Format(float value, bool whole) => whole ? ((int)value).ToString() : value.ToString("0.##");
}
