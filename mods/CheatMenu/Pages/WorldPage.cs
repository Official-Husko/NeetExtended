using NeetModKit.Game;
using NeetModKit.Ui.Toolkit;
using UnityEngine;
using UnityEngine.UI;

namespace CheatMenu.Pages;

/// <summary>Fire, power, doors and how fast the game runs.</summary>
internal sealed class WorldPage : CheatPage
{
    private Text _room;

    public override string Title => "World";
    public override int Order => 30;

    protected override void BuildPage(UiBuilder ui)
    {
        var house = ui.Panel();
        house.Header("HOUSE");
        _room = house.SubHeader("");
        var fire = house.Row(8f, 26f);
        fire.Button("START FIRE HERE", () => Run(() => HouseSystems.StartFire(CurrentRoom()), "Fire started. Good luck."), 200f, accent: true);
        fire.Button("PUT FIRE OUT", () => Run(() => HouseSystems.StopFire(CurrentRoom()), "Fire's out."), 180f);

        var power = house.Row(8f, 26f);
        power.Button("POWER ON", () => Run(() => HouseSystems.SetHousePower(true), "Power on everywhere."), 160f);
        power.Button("POWER OFF", () => Run(() => HouseSystems.SetHousePower(false), "Power off everywhere."), 160f);
        power.Button("UNLOCK ALL DOORS", () => Run(() => Doors.UnlockAll(), "Every door is unlocked."), 200f);

        var time = ui.Panel();
        time.Header("TIME");
        time.Slider("GAME SPEED", Time.timeScale, 0.1f, 4f, v => Time.timeScale = v);
        time.Text("1 is normal speed.");
    }

    protected override void RefreshPage()
    {
        if (GameRefs.Player != null)
            _room.text = "YOU'RE IN: " + CurrentRoom().ToString().ToUpperInvariant();
    }

    private static Location CurrentRoom() => Rooms.At(GameRefs.Player.transform.position);
}
