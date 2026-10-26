using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace NeetModKit.Ui.Toolkit;

/// <summary>
/// A set of game UI elements to build from. The elements are copied once into a hidden holder and
/// stripped of what the originals did, then UiBuilder clones them. Templates the theme doesn't have
/// come from its fallback theme.
/// </summary>
public sealed class UiTheme
{
    // Decorations that belong to a particular original control, not to the control type.
    private static readonly string[] Extras = { "sliderTreshold_Image", "Brightness_Text" };

    private readonly Func<Transform> _findSource;
    private readonly Dictionary<UiTemplate, string> _paths;
    private readonly Dictionary<UiTemplate, GameObject> _templates = new Dictionary<UiTemplate, GameObject>();
    private Transform _holder;

    public string Name { get; }

    /// <summary>Used for templates this theme doesn't define.</summary>
    public UiTheme Fallback { get; set; }

    /// <summary>The source object the templates were taken from (for its canvas settings).</summary>
    public Transform Source { get; private set; }

    public UiTheme(string name, Func<Transform> findSource, Dictionary<UiTemplate, string> paths)
    {
        Name = name;
        _findSource = findSource;
        _paths = paths;
    }

    public bool Ready => _holder != null && _templates.Count > 0;

    /// <summary>Collects the templates if needed. False if the source UI isn't loaded yet.</summary>
    public bool Ensure()
    {
        if (Ready)
            return Fallback == null || Fallback.Ensure();

        var source = _findSource();
        if (source == null)
            return false;

        Release();
        Source = source;
        var holder = new GameObject("NeetModKit.Theme." + Name);
        holder.SetActive(false);
        Object.DontDestroyOnLoad(holder);
        _holder = holder.transform;

        foreach (var pair in _paths)
        {
            var original = source.Find(pair.Value);
            if (original == null)
            {
                KitMod.Instance?.Log.Warn($"{Name} theme: '{pair.Value}' wasn't found.");
                continue;
            }
            var copy = Object.Instantiate(original.gameObject, _holder);
            copy.name = pair.Key.ToString();
            copy.SetActive(true);
            Sanitize(copy);
            _templates[pair.Key] = copy;
        }
        return Fallback == null || Fallback.Ensure();
    }

    public bool Has(UiTemplate kind) => _templates.ContainsKey(kind) || (Fallback != null && Fallback.Has(kind));

    public GameObject Get(UiTemplate kind)
    {
        if (_templates.TryGetValue(kind, out var go) && go != null)
            return go;
        if (Fallback != null)
            return Fallback.Get(kind);
        throw new InvalidOperationException($"The {Name} theme has no {kind}.");
    }

    public void Release()
    {
        if (_holder != null)
            Object.Destroy(_holder.gameObject);
        _holder = null;
        _templates.Clear();
    }

    /// <summary>Strips everything that would make a copy do what the original did.</summary>
    private static void Sanitize(GameObject copy)
    {
        foreach (string extra in Extras)
        {
            var child = copy.transform.Find(extra);
            if (child != null)
                Object.DestroyImmediate(child.gameObject);
        }
        foreach (var trigger in copy.GetComponentsInChildren<EventTrigger>(true))
            Object.DestroyImmediate(trigger);
        foreach (var button in copy.GetComponentsInChildren<Button>(true))
            button.onClick = new Button.ButtonClickedEvent();
        foreach (var toggle in copy.GetComponentsInChildren<Toggle>(true))
            toggle.onValueChanged = new Toggle.ToggleEvent();
        foreach (var slider in copy.GetComponentsInChildren<Slider>(true))
            slider.onValueChanged = new Slider.SliderEvent();
        foreach (var input in copy.GetComponentsInChildren<InputField>(true))
        {
            input.onValueChanged = new InputField.OnChangeEvent();
            input.onEndEdit = new InputField.EndEditEvent();
            input.text = "";
        }
        foreach (var text in copy.GetComponentsInChildren<Text>(true))
            text.supportRichText = true;
    }
}
