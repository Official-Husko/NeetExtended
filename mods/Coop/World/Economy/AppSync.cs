using Coop.Net;
using HarmonyLib;
using LiteNetLib.Utils;
using UnityEngine;
using UnityEngine.UI;

namespace Coop.World.Economy;

/// <summary>
/// Which computer apps the house owns. Apps are bought on the host (a client's purchase is a
/// <see cref="Purchases"/> request); the host sends the owned list after each purchase and to a
/// joining player, and clients show new ones the way the game shows a save's apps.
///
/// WorldApps (host to all): count, then one bool per app.
/// </summary>
internal static class AppSync
{
    private static readonly AccessTools.FieldRef<AppDownloader, Computer_Button[]> Buttons =
        AccessTools.FieldRefAccess<AppDownloader, Computer_Button[]>("apps");

    private static AppDownloader _downloader;

    public static void Init() => Messages.On(MessageIds.WorldApps, OnApps);

    public static void Clear() => _downloader = null;

    /// <summary>The house's app store page (on the computer, often switched off).</summary>
    public static AppDownloader Downloader()
    {
        if (_downloader == null)
            _downloader = Object.FindObjectOfType<AppDownloader>(true);
        return _downloader;
    }

    /// <summary>Host, after <c>AppDownloader.BuyApp</c>: a real purchase goes out to everyone.</summary>
    public static void AfterBuy(AppDownloader apps, bool enableOnly)
    {
        if (enableOnly || !WorldSide.Running || !WorldSide.IsHost || apps != Downloader())
            return;
        Outbox.ToPlayers(Owned(apps), Channel.Events);
    }

    /// <summary>Host: the owned apps to a player who joined or resynced.</summary>
    public static void SendTo(byte slot)
    {
        var apps = Downloader();
        if (apps?.boughtApps != null)
            Outbox.ToSlot(slot, Owned(apps), Channel.Events);
    }

    private static NetDataWriter Owned(AppDownloader apps)
    {
        var w = Outbox.Begin(MessageIds.WorldApps);
        w.Put((byte)apps.boughtApps.Length);
        foreach (bool bought in apps.boughtApps)
            w.Put(bought);
        return w;
    }

    private static void OnApps(in Sender from, NetDataReader reader)
    {
        int count = reader.GetByte();
        var apps = WorldSide.Running && WorldSide.IsClient ? Downloader() : null;
        var buttons = apps != null ? Buttons(apps) : null;
        for (int i = 0; i < count; i++)
        {
            bool bought = reader.GetBool();
            if (bought && buttons != null && i < buttons.Length && i < apps.boughtApps.Length && !apps.boughtApps[i])
                Show(apps, buttons[i], i);
        }
    }

    // AppDownloader.BuyApp's owned branch, without its money check or sound (the money is the host's).
    private static void Show(AppDownloader apps, Computer_Button app, int index)
    {
        if (app == null)
            return;
        app.ButtonCommand();
        if (app.transform.childCount > 1 && app.transform.GetChild(1).TryGetComponent(out Text label))
            label.text = "Owned";
        if (app.TryGetComponent(out Button button))
            button.enabled = false;
        apps.boughtApps[index] = true;
    }
}
