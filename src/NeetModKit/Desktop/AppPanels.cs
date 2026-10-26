using System;
using NeetLoader;
using NeetLoader.Runtime;
using NeetModKit.Ui.Toolkit;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace NeetModKit.Desktop;

/// <summary>Creates the page for a modded app inside the browser, shaped like the game's own pages.</summary>
internal static class AppPanels
{
    private const float Padding = 6f;
    private const string ShapeSource = "LuckyCassette";

    public static GameObject Create(ComputerApp app, NeetMod owner)
    {
        var browser = ComputerApps.Browser ?? throw new InvalidOperationException("The computer isn't loaded.");
        if (!UiThemes.Computer.Ensure())
            throw new InvalidOperationException("The computer's UI isn't available.");

        var shape = FindPage(browser, ShapeSource) ?? (RectTransform)browser.websites[0].transform;
        var go = new GameObject("App_" + app.Id, typeof(RectTransform));
        go.SetActive(false);
        var panel = (RectTransform)go.transform;
        panel.SetParent(shape.parent, false);
        panel.anchorMin = shape.anchorMin;
        panel.anchorMax = shape.anchorMax;
        panel.pivot = shape.pivot;
        panel.sizeDelta = new Vector2(shape.sizeDelta.x, app.Definition.Height);
        var layout = go.AddComponent<LayoutElement>();
        layout.preferredWidth = shape.sizeDelta.x;
        layout.preferredHeight = app.Definition.Height;
        owner.Track(Disposable.Destroying(go));

        var background = Object.Instantiate(UiThemes.Computer.Get(UiTemplate.Backdrop), panel, false);
        UiBuilder.Stretch((RectTransform)background.transform);

        var content = new UiBuilder(panel, UiThemes.Computer).NewChild("Content");
        UiBuilder.Stretch(content, Padding, Padding, Padding, Padding);
        var ui = UiBuilder.Vertical(content, UiThemes.Computer.Metrics.Spacing, theme: UiThemes.Computer);

        app.Definition.Build(new ComputerAppContext(app, panel, ui));
        return go;
    }

    private static RectTransform FindPage(Computer_Browser_Addresser browser, string name)
    {
        foreach (var site in browser.websites)
        {
            if (site != null && site.name == name)
                return (RectTransform)site.transform;
        }
        return null;
    }
}
