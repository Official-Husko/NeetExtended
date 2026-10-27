using NeetLoader.Runtime;
using NeetModKit.Ui.Toolkit;
using UnityEngine;

namespace NeetModKit.Ui.Notifications;

/// <summary>
/// Short messages stacked in the bottom right corner of every screen, over the game and the menus,
/// in the pause menu's look: "Receiving clip.webm" with a bar that fills. They never take clicks.
/// </summary>
/// <example>
/// var notice = Notices.Show(this, "Receiving clip.webm", "from the host");
/// notice.Progress = 0.4f;   // later
/// notice.Close(2f);         // gone two seconds from now
/// </example>
public static class Notices
{
    /// <summary>Over the game's screens and the mod menu's windows (1000).</summary>
    private const int SortingOrder = 1100;

    private static NoticeBoard _board;

    /// <summary>
    /// Puts a notice up until it's closed, or for <paramref name="seconds"/> when that's over 0. It
    /// also goes when <paramref name="owner"/> unloads.
    /// </summary>
    public static Notice Show(ITracker owner, string title, string detail = null, float? progress = null, float seconds = 0f)
    {
        var notice = new Notice(title, detail, progress);
        if (seconds > 0f)
            notice.Close(seconds);
        owner.Track(notice);
        Board()?.Add(notice);
        return notice;
    }

    private static NoticeBoard Board()
    {
        if (_board != null)
            return _board;
        var kit = KitMod.Instance;
        if (kit == null)
            return null;
        var canvas = UiCanvas.Create(kit, "NeetModKit.Notices", SortingOrder, interactive: false,
            referenceResolution: new Vector2(1600f, 900f));
        _board = canvas.gameObject.AddComponent<NoticeBoard>();
        return _board;
    }
}
