using System.Collections.Generic;
using Coop.Net;
using Coop.Session;
using NeetModKit.Events;
using NeetModKit.Menu;
using NeetModKit.Ui.Toolkit;
using UnityEngine.UI;

namespace Coop.Menu;

/// <summary>Mod menu tab: host your house, join someone else's, and see who's in.</summary>
internal sealed class CoopPage : MenuPage
{
    private string _address;
    private string _message = "";
    private readonly List<(CoopPlayer, Text)> _pings = new List<(CoopPlayer, Text)>();
    private Text _files;

    public override string Title => "Co-op";
    public override int Order => 36;

    public CoopPage()
    {
        CoopSession.Started += Changed;
        CoopSession.Ended += _ => Changed();
        CoopSession.Welcomed += Changed;
        CoopSession.WorldReady += Changed;
        CoopSession.PlayerArrived += _ => Changed();
        CoopSession.PlayerReady += _ => Changed();
        CoopSession.PlayerLeft += _ => Changed();
    }

    public override void Build(UiBuilder ui)
    {
        _address ??= CoopMod.Config.Value.LastAddress;
        ui.Text(MenuPalette.Paint(
            "Play the house together. The host's house, items and money are shared; everyone keeps their own " +
            "health, needs and clothes. The host needs the port from the settings (" + CoopMod.Config.Value.Port +
            ", UDP) open in their router and firewall.", MenuPalette.Muted));

        if (CoopSession.IsActive)
            BuildSession(ui);
        else
            BuildOffline(ui);

        ui.Text(_message);
    }

    private void BuildOffline(UiBuilder ui)
    {
        if (CoopSession.LastEndReason.Length > 0)
            ui.Text(MenuPalette.Paint("Last game: " + CoopSession.LastEndReason, MenuPalette.Muted));

        var host = ui.Row(8f, 26f);
        host.SubHeader("HOST");
        foreach (var option in Transports.All)
        {
            string id = option.Id;
            string label = id == Transports.DirectId ? "OPEN MY HOUSE" : "OPEN OVER " + option.Name.ToUpperInvariant();
            host.Button(label, () => Report(CoopApi.HostWith(id)), 200f, accent: id == Transports.DirectId);
        }
        if (!GameEvents.IsHouseReady)
            ui.Text(MenuPalette.Paint("Load a game first to host it.", MenuPalette.Muted));

        var join = ui.Row(8f, 26f);
        join.SubHeader("JOIN");
        join.Input(_address, value => _address = value, JoinHint(), 320f);
        join.Button("JOIN", () => Report(CoopApi.Join(_address)), 120f);
    }

    private void BuildSession(UiBuilder ui)
    {
        var top = ui.Row(8f, 26f);
        top.SubHeader(Describe(CoopSession.State));
        top.Button("LEAVE", () => Report(CoopApi.Leave()), 120f);
        string address = CoopSession.IsHost ? Transports.Find(CoopSession.TransportId)?.HostAddress?.Invoke() : null;
        if (address != null)
            ui.Text("Others join with: " + address);

        _pings.Clear();
        var panel = ui.Panel();
        panel.SubHeader("PLAYERS");
        foreach (var player in CoopSession.Players)
        {
            var row = panel.Row(8f, 22f);
            row.Text(player.Name + (player.IsHost ? "  (host)" : "") + (player.IsLocal ? "  (you)" : ""));
            if (!player.IsLocal)
                _pings.Add((player, row.Text(PingText(player))));
        }
        _files = ui.Text(MenuPalette.Paint(FilesLine(), MenuPalette.Muted));
    }

    private static string FilesLine() =>
        Join.JoinFiles.Status.Length > 0 ? Join.JoinFiles.Status : Assets.FileSharing.StatusLine();

    private static string PingText(CoopPlayer player) => MenuPalette.Paint(
        player.InWorld ? $"{CoopSession.Transport.PingOf(player.Peer)} ms" : "loading...", MenuPalette.Muted);

    private static string JoinHint() => Transports.All.Count > 1
        ? "address (203.0.113.5:8889) or the host's Steam ID"
        : "address, like 203.0.113.5 or 203.0.113.5:8889";

    private static string Describe(SessionState state) => state switch
    {
        SessionState.Hosting => "HOSTING",
        SessionState.Connecting => "CONNECTING...",
        SessionState.Joining => "LOADING THE HOST'S HOUSE...",
        SessionState.Playing => "IN THE HOST'S HOUSE",
        _ => "OFFLINE",
    };

    // The page is rebuilt on the next frame, so the message is kept for it.
    private void Report(string message)
    {
        _message = message;
        Rebuild();
    }

    public override void Refresh()
    {
        foreach (var (player, label) in _pings)
            label.text = PingText(player);
        if (_files != null)
            _files.text = MenuPalette.Paint(FilesLine(), MenuPalette.Muted);
    }

    private void Changed() => Rebuild();
}
