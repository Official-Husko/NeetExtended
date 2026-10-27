using Coop.Session;
using NeetModKit.Ui;
using UnityEngine.UI;

namespace Coop.Menu;

/// <summary>
/// Client: the pause menu's "Get out" button says "Disconnect" and leaves the host's game straight
/// away (the game's own button asks what you really want, and its way back is the main menu, which
/// leaves anyway). It's the game's button again once the session is over.
/// </summary>
internal static class DisconnectButton
{
    private const string ButtonPath = "Canvas_Settings/Quit Button";
    private const string Label = "Disconnect";

    private static Button _button;
    private static Button.ButtonClickedEvent _original;
    private static string _originalText;

    public static void Init()
    {
        CoopSession.WorldReady += Apply;
        CoopSession.Resumed += Apply;
        CoopSession.Ended += _ => Restore();
        CoopLoop.Unloading += Restore;
    }

    private static void Apply()
    {
        if (!CoopSession.IsClient || _button != null)
            return;
        var menu = Feedback_Sender._ != null ? Feedback_Sender._.transform.Find(ButtonPath) : null;
        var button = menu != null ? menu.GetComponent<Button>() : null;
        if (button == null)
            return;
        var text = menu.GetComponentInChildren<Text>(true);
        _button = button;
        _original = button.onClick;
        _originalText = text != null ? text.text : null;
        UiCloning.SetClick(button, () => CoopSession.Leave());
        UiCloning.SetText(menu, Label);
    }

    private static void Restore()
    {
        if (_button == null)
            return;
        _button.onClick = _original;
        if (_originalText != null)
            UiCloning.SetText(_button.transform, _originalText);
        _button = null;
        _original = null;
    }
}
