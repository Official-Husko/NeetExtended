using System;
using System.Linq;
using Coop.Admin;
using Coop.Players;
using Coop.Session;
using NeetModKit.Game;
using NeetModKit.Menu;
using NeetModKit.Ui.Toolkit;

namespace Coop.Menu;

/// <summary>
/// Mod menu tab for the host: pick a player, then help them (heal, bring them over, go to them,
/// give them something, kick a player who's stuck or bugged) or mess with them.
/// </summary>
internal sealed class PlayersPage : MenuPage
{
    private const int MaxItems = 24;

    private byte _selected = byte.MaxValue;
    private string _search = "";
    private string _message = "";
    private UiBuilder _items;

    public override string Title => "Players";
    public override int Order => 37;

    public PlayersPage()
    {
        CoopSession.Started += Rebuild;
        CoopSession.Ended += _ => Rebuild();
        CoopSession.PlayerReady += _ => Rebuild();
        CoopSession.PlayerLeft += _ => Rebuild();
    }

    public override void Build(UiBuilder ui)
    {
        ui.Text(MenuPalette.Paint(
            "The host's hand on everyone in the house: help a player who's stuck or bugged, or mess with your friends.", MenuPalette.Muted));
        if (!CoopSession.IsHost)
        {
            ui.Text(MenuPalette.Paint("Only the host of a co-op game has this. Host your house from the Co-op tab.", MenuPalette.Muted));
            return;
        }

        var target = Selected();
        var pick = ui.Row(6f, 26f);
        pick.SubHeader("PLAYER");
        foreach (var player in CoopSession.Players.Where(p => p.IsLocal || p.InWorld))
        {
            byte slot = player.Slot;
            pick.Button(player.Name.ToUpperInvariant() + (player.IsLocal ? " (YOU)" : ""), () => Select(slot), 170f, accent: slot == target?.Slot);
        }
        if (target == null)
        {
            ui.Text("Nobody's in the house yet.");
            return;
        }
        ui.Text(MenuPalette.Paint(Describe(target), MenuPalette.Muted));

        var help = ui.Panel();
        help.Header("HELP");
        var helpGrid = help.Grid(185f, 26f);
        Add(helpGrid, "HEAL", AdminAction.Heal);
        if (!target.IsLocal)
        {
            Add(helpGrid, "BRING TO ME", AdminAction.BringHere);
            Add(helpGrid, "GO TO THEM", AdminAction.GoThere);
            Add(helpGrid, "KICK", AdminAction.Kick);
        }

        var give = ui.Panel();
        give.Header("GIVE");
        give.Input(_search, text =>
        {
            _search = text;
            FillItems();
        }, "Search items...");
        _items = give.Grid(200f, 26f);
        FillItems();

        var trouble = ui.Panel();
        trouble.Header("TROUBLE");
        var grid = trouble.Grid(185f, 26f);
        Add(grid, "SEND THE HITMAN", AdminAction.Hitman);
        Add(grid, "SLAP", AdminAction.Slap);
        Add(grid, "YEET", AdminAction.Yeet);
        Add(grid, "SPIN AROUND", AdminAction.Spin);
        Add(grid, "FREEZE", AdminAction.Freeze);
        Add(grid, "STARVE", AdminAction.Starve);
        Add(grid, "PISS ALARM", AdminAction.PissAlarm);
        Add(grid, "MAKE THEM SHIT", AdminAction.Shit);
        Add(grid, "KNOCK DOWN", AdminAction.KnockDown);
        Add(grid, "ROOM ON FIRE", AdminAction.Fire);
        Add(grid, "SPIDER", AdminAction.Spider);
        Add(grid, "RAT", AdminAction.Rat);
        Add(grid, "POOPMAN", AdminAction.Poopman);

        ui.Text(_message);
    }

    private void Add(UiBuilder grid, string label, AdminAction action) =>
        grid.Button(label, () => Run(action, null), -1f, accent: action == AdminAction.Kick);

    private void FillItems()
    {
        if (_items == null)
            return;
        _items.Clear();
        var matches = Spawning.ItemNames
            .Where(n => _search.Length == 0 || n.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0)
            .Take(MaxItems);
        foreach (string name in matches)
        {
            string item = name;
            _items.Button(item, () => Run(AdminAction.Give, item), -1f);
        }
    }

    private void Run(AdminAction action, string item)
    {
        var target = Selected();
        if (target == null)
            return;
        try
        {
            _message = PlayerAdmin.Do(target.Slot, action, item);
        }
        catch (Exception e)
        {
            _message = "That didn't work: " + e.Message;
        }
        Rebuild();
    }

    private void Select(byte slot)
    {
        _selected = slot;
        _message = "";
        Rebuild();
    }

    // The picked player if they're still here, else the first other player in the house, else yourself.
    private CoopPlayer Selected()
    {
        var picked = CoopSession.FindSlot(_selected);
        if (picked != null && (picked.IsLocal || picked.InWorld))
            return picked;
        var other = CoopSession.Players.FirstOrDefault(p => !p.IsLocal && p.InWorld);
        return other ?? CoopSession.Local;
    }

    private static string Describe(CoopPlayer player)
    {
        if (player.IsLocal)
            return "You. Help and trouble work on yourself too.";
        string state = Downed.IsDown(player.Slot) ? "down, needs food" : "up and about";
        return $"{player.Name}: {state}, ping {CoopSession.Transport.PingOf(player.Peer)} ms.";
    }
}
