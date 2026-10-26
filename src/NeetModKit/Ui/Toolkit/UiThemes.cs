using System.Collections.Generic;
using NeetModKit.Game;
using UnityEngine;
using UnityEngine.UI;

namespace NeetModKit.Ui.Toolkit;

/// <summary>The themes the toolkit knows: the pause menu and the in-game computer.</summary>
public static class UiThemes
{
    private const string Settings = "Canvas_Settings/";
    private const string Sites = "Browser/Scroll View (Site Container)/Viewport (Site Container)/Content (Site Container)/";

    /// <summary>The pause menu: orange display-font headers, violet buttons, dark purple backdrop.</summary>
    public static readonly UiTheme Menu = new UiTheme("Menu", () => Feedback_Sender._ != null ? Feedback_Sender._.transform : null,
        new Dictionary<UiTemplate, string>
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
        });

    /// <summary>The computer's websites: their fonts, page backgrounds and buttons. Controls fall back to the menu theme.</summary>
    public static readonly UiTheme Computer = new UiTheme("Computer", () => GameRefs.Computer != null ? GameRefs.Computer.transform : null,
        new Dictionary<UiTemplate, string>
        {
            [UiTemplate.Backdrop] = Sites + "LuckyCassette/Background",
            [UiTemplate.Header] = Sites + "LuckyCassette/Title",
            [UiTemplate.SubHeader] = Sites + "LuckyCassette/Static Track Header",
            [UiTemplate.Text] = Sites + "LuckyCassette/Slogan",
            [UiTemplate.Button] = Sites + "AnimeClicker/RedeemButton",
            [UiTemplate.AccentButton] = Sites + "AnimeClicker/RedeemButton",
        })
    {
        Fallback = Menu,
        Metrics = UiMetrics.Computer,
    };

    /// <summary>The pause menu's canvas scaler, so overlay windows come out the same size.</summary>
    public static CanvasScaler MenuScaler => Menu.Source != null ? Menu.Source.GetComponentInChildren<CanvasScaler>(true) : null;

    internal static void ReleaseAll()
    {
        Menu.Release();
        Computer.Release();
    }
}
