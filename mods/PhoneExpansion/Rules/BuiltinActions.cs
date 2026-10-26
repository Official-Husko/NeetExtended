using System;
using System.Collections;
using NeetModKit.Desktop;
using NeetModKit.Game;
using PhoneExpansion.Calls;
using UnityEngine;

namespace PhoneExpansion.Rules;

/// <summary>The actions every call file can use.</summary>
internal static class BuiltinActions
{
    public static void Register(PhoneExpansionMod mod)
    {
        PhoneActions.Register(mod, "money", a => Money.Add(a.Num("amount")), "amount",
            "Adds (or with a minus, takes) cash. Increases are taxed like any income.");
        PhoneActions.Register(mod, "give", a => Repeat(a, () => Spawning.Item(a.Require("item"), BesideSpeaker())), "item",
            "Pops an item out next to the phone, like the game's own GiveItem. \"count\" for more.");
        PhoneActions.Register(mod, "deliver", a => mod.StartCoroutine(After(a.Num("after"), () => Repeat(a, () => Deliveries.Drop(a.Require("item"))))), "item",
            "Drops a package with the item at the front door. \"after\": seconds to wait, \"count\" for more.");
        PhoneActions.Register(mod, "npc", a => Spawning.NpcInRoom(a.Require("name"), Room(a, Location.FrontYard)), "name",
            "Spawns a character (see the cheat menu's list) in \"room\" (default: the front yard).");
        PhoneActions.Register(mod, "fire", a => HouseSystems.StartFire(Room(a, PlayerRoom())), "room",
            "Starts a fire in \"room\" (default: wherever the player is).");
        PhoneActions.Register(mod, "fireout", a => PutOutFires(a), "room",
            "Puts out the fire in \"room\", or every fire if no room is given.");
        PhoneActions.Register(mod, "power", a => SetPower(mod, a), "on",
            "Switches power off (\"on\": false) or on, for one \"room\" or the whole house. \"for\": seconds until it comes back.");
        PhoneActions.Register(mod, "fuses", _ => RequireHouse().fuseBox_Manager.ExpelAllFuses(), null,
            "Pops every fuse out of the fuse box. Lights stay out until the player puts them back.");
        PhoneActions.Register(mod, "unlock", a => Doors.Unlock(a.Require("door")), "door",
            "Unlocks doors by id: BASEMENT, DADYARD, Attic.");
        PhoneActions.Register(mod, "site", a => ComputerControl.OpenSite(a.Require("url")), "url",
            "Opens a site or app on the computer.");
        PhoneActions.Register(mod, "ring", a => RingLater(a), "call",
            "Rings a call (by id) after \"after\" seconds of play. Survives saving.");
        PhoneActions.Register(mod, "flag", a => SetFlag(a), "name",
            "Sets a flag (\"set\", default 1) or changes it (\"add\"). Flags are saved and can be checked in conditions.");
        PhoneActions.Register(mod, "sequence", a => SequenceManager._.TriggerSequence(a.Require("name"), a.Str("arg", "")), "name",
            "Runs one of the game's scene sequences, like the game's TriggerSequence.");
        PhoneActions.Register(mod, "knockout", a => RequirePlayer().knockout.AddVal(a.Num("seconds", 5f)), "seconds",
            "Knocks the player out for a few seconds.");
        PhoneActions.Register(mod, "stat", a => ChangeStat(a), "name",
            "Changes a player stat (Hunger, Energy, Soul, ...): \"set\" a value or \"add\" to it.");
    }

    private static void Repeat(ActionArgs args, Action action)
    {
        int count = Mathf.Clamp(args.Int("count", 1), 1, 20);
        for (int i = 0; i < count; i++)
            action();
    }

    private static IEnumerator After(float seconds, Action action)
    {
        if (seconds > 0f)
            yield return new WaitForSeconds(seconds);
        try { action(); }
        catch (Exception e) { PhoneExpansionMod.Instance?.Log.Warn("A delayed phone action failed: " + e.Message); }
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

    private static void SetPower(PhoneExpansionMod mod, ActionArgs args)
    {
        bool on = args.Bool("on", false);
        Action<bool> apply = args.Has("room")
            ? state => HouseSystems.SetPower(Rooms.Parse(args.Str("room")), state)
            : (Action<bool>)HouseSystems.SetHousePower;
        apply(on);
        float seconds = args.Num("for");
        if (seconds > 0f)
            mod.StartCoroutine(After(seconds, () => apply(!on)));
    }

    private static void RingLater(ActionArgs args)
    {
        string id = args.Require("call");
        string nearMod = args.Call?.Id?.Split('/')[0];
        var call = PhoneBook.Find(id, nearMod) ?? throw new ArgumentException($"No call '{id}'.");
        IncomingCalls.Ring(call, args.Num("after"));
    }

    private static void SetFlag(ActionArgs args)
    {
        string name = args.Require("name");
        if (args.Has("add"))
            PhoneMemory.AddFlag(name, args.Int("add"));
        else
            PhoneMemory.SetFlag(name, args.Int("set", 1));
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
