using System;
using NeetModKit.Desktop;
using NeetModKit.Game;
using UnityEngine;

namespace NeetModKit.Rules;

/// <summary>The actions every content file can use.</summary>
internal static class BuiltinActions
{
    public static void Register(NeetLoader.NeetMod kit)
    {
        RuleActions.Register(kit, "money", a => Money.Add(a.Num("amount")), "amount",
            "Adds (or with a minus, takes) cash. Increases are taxed like any income.");
        RuleActions.Register(kit, "give", a => a.Repeat(() => Spawning.Item(a.Require("item"), BesideSpeaker())), "item",
            "Pops an item out next to whoever is talking (like the game's GiveItem), or in front of the player. \"count\" for more.");
        RuleActions.Register(kit, "deliver", a => RuleActions.Later(kit, a.Num("after"), () => a.Repeat(() => Deliveries.Drop(a.Require("item")))), "item",
            "Drops a package with the item at the front door. \"after\": seconds to wait, \"count\" for more.");
        RuleActions.Register(kit, "npc", a => Spawning.NpcInRoom(a.Require("name"), Room(a, Location.FrontYard)), "name",
            "Spawns a character (see the cheat menu's list) in \"room\" (default: the front yard).");
        RuleActions.Register(kit, "fire", a => HouseSystems.StartFire(Room(a, PlayerRoom())), "room",
            "Starts a fire in \"room\" (default: wherever the player is).");
        RuleActions.Register(kit, "fireout", a => PutOutFires(a), "room",
            "Puts out the fire in \"room\", or every fire if no room is given.");
        RuleActions.Register(kit, "power", a => SetPower(kit, a), "on",
            "Switches power off (\"on\": false) or on, for one \"room\" or the whole house. \"for\": seconds until it comes back.");
        RuleActions.Register(kit, "fuses", _ => RequireHouse().fuseBox_Manager.ExpelAllFuses(), null,
            "Pops every fuse out of the fuse box. Lights stay out until the player puts them back.");
        RuleActions.Register(kit, "unlock", a => Doors.Unlock(a.Require("door")), "door",
            "Unlocks doors by id: BASEMENT, DADYARD, Attic.");
        RuleActions.Register(kit, "site", a => ComputerControl.OpenSite(a.Require("url")), "url",
            "Opens a site or app on the computer.");
        RuleActions.Register(kit, "flag", a => SetFlag(a), "name",
            "Sets a flag (\"set\", default 1) or changes it (\"add\"). Flags are saved and can be checked in conditions.");
        RuleActions.Register(kit, "sequence", a => SequenceManager._.TriggerSequence(a.Require("name"), a.Str("arg", "")), "name",
            "Runs one of the game's scene sequences, like the game's TriggerSequence.");
        RuleActions.Register(kit, "knockout", a => RequirePlayer().knockout.AddVal(a.Num("seconds", 5f)), "seconds",
            "Knocks the player out for a few seconds.");
        RuleActions.Register(kit, "stat", a => ChangeStat(a), "name",
            "Changes a player stat (Hunger, Energy, Soul, ...): \"set\" a value or \"add\" to it.");
    }

    private static Vector3 BesideSpeaker()
    {
        var speaker = Dialogue._ != null ? Dialogue._.currSpeaker : null;
        if (speaker == null)
            return Spawning.InFrontOfPlayer();
        // Same spot the game uses for GiveItem.
        return speaker.transform.position + Vector3.up * 0.6f + speaker.transform.forward * 0.3f;
    }

    private static Location Room(ActionArgs args, Location fallback) =>
        args.Has("room") ? Rooms.Parse(args.Str("room")) : fallback;

    private static Location PlayerRoom() => Rooms.At((GameRefs.Player ?? throw new InvalidOperationException("No player.")).transform.position);

    private static void PutOutFires(ActionArgs args)
    {
        if (args.Has("room"))
        {
            HouseSystems.StopFire(Rooms.Parse(args.Str("room")));
            return;
        }
        foreach (var room in Rooms.All)
        {
            if (Rooms.Exists(room) && HouseSystems.IsOnFire(room))
                HouseSystems.StopFire(room);
        }
    }

    private static void SetPower(NeetLoader.NeetMod owner, ActionArgs args)
    {
        bool on = args.Bool("on", false);
        Action<bool> apply = args.Has("room")
            ? state => HouseSystems.SetPower(Rooms.Parse(args.Str("room")), state)
            : (Action<bool>)HouseSystems.SetHousePower;
        apply(on);
        float seconds = args.Num("for");
        if (seconds > 0f)
            RuleActions.Later(owner, seconds, () => apply(!on));
    }

    private static void SetFlag(ActionArgs args)
    {
        string name = args.Require("name");
        if (args.Has("add"))
            ModFlags.Add(name, args.Int("add"));
        else
            ModFlags.Set(name, args.Int("set", 1));
    }

    private static void ChangeStat(ActionArgs args)
    {
        var stat = PlayerControl.FindStat(args.Require("name"));
        if (args.Has("set"))
            stat.SetVal(args.Num("set"));
        else
            stat.AddVal(args.Num("add"));
    }

    private static HouseManager RequireHouse() => GameRefs.House ?? throw new InvalidOperationException("Not in the house.");

    private static PlayerStats RequirePlayer() => GameRefs.PlayerStats ?? throw new InvalidOperationException("No player.");
}
