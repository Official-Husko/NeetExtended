using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NeetModKit.Ui.Toolkit;

/// <summary>
/// Copies of the pause menu's own controls, kept hidden and used as templates. Building from the
/// game's real elements gives exactly its fonts, sprites, colours and hover/press transitions.
/// Harvested once per session the first time UI is built.
/// </summary>
public static class UiTemplates
{
    private const string Settings = "Canvas_Settings/";

    private static readonly Dictionary<UiTemplate, string> Sources = new Dictionary<UiTemplate, string>
    {
        [UiTemplate.Backdrop] = Settings + "Image",
        [UiTemplate.Header] = Settings + "Controls/Display_Text (1)",
        [UiTemplate.SubHeader] = Settings + "Display/Resolution_Text",
        [UiTemplate.Text] = Settings + "DataDisclaimer",
        [UiTemplate.Button] = Settings + "Controls/ChangeControlsButton",
        [UiTemplate.AccentButton] = Settings + "Quit Button",
        [UiTemplate.Toggle] = Settings + "Bloom_Toggle",
        [UiTemplate.Slider] = Settings + "Brightness_Slider",
        [UiTemplate.Input] = Settings + "CommentInput",
        [UiTemplate.Scrollbar] = Settings + "Quality_Dropdown/Template/Scrollbar",
    };

    // Decorations that belong to the original control, not to the control type.
    private static readonly string[] Extras = { "sliderTreshold_Image", "Brightness_Text" };

    private static readonly Dictionary<UiTemplate, GameObject> Templates = new Dictionary<UiTemplate, GameObject>();
    private static Transform _root;

    /// <summary>The canvas scaler settings of the pause menu, so copies render at the same size.</summary>
    public static CanvasScaler ScalerSource { get; private set; }

    public static bool Ready => _root != null && Templates.Count == Sources.Count;

    /// <summary>Collects the templates if needed. False if the pause menu isn't available yet.</summary>
    public static bool Ensure()
    {
        if (Ready)
            return true;

        var menu = Feedback_Sender._;
        if (menu == null)
            return false;

        Release();
        var rootGo = new GameObject("NeetModKit.UiTemplates");
        rootGo.SetActive(false);
        Object.DontDestroyOnLoad(rootGo);
        _root = rootGo.transform;

        foreach (var pair in Sources)
        {
            var source = menu.transform.Find(pair.Value);
            if (source == null)
            {
                KitMod.Instance?.Log.Warn($"UI template '{pair.Value}' wasn't found in the pause menu.");
                continue;
            }
            var copy = Object.Instantiate(source.gameObject, _root);
            copy.name = pair.Key.ToString();
            copy.SetActive(true);
            Sanitize(copy);
            Templates[pair.Key] = copy;
        }

        ScalerSource = menu.GetComponentInChildren<CanvasScaler>(true);
        return Ready;
    }

    public static GameObject Get(UiTemplate kind) =>
        Templates.TryGetValue(kind, out var go) ? go : throw new System.InvalidOperationException($"UI template {kind} isn't available.");

    internal static void Release()
    {
        if (_root != null)
            Object.Destroy(_root.gameObject);
        _root = null;
        Templates.Clear();
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
