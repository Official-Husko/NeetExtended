namespace Coop.Admin;

/// <summary>What the host can do to a player from the Players tab. The numbers go over the network.</summary>
internal enum AdminAction : byte
{
    // Help.
    Heal = 0,
    BringHere = 1,
    GoThere = 2,
    Give = 3,
    Kick = 4,

    // Trouble.
    Hitman = 10,
    Slap = 11,
    Yeet = 12,
    Spin = 13,
    Freeze = 14,
    Starve = 15,
    PissAlarm = 16,
    Shit = 17,
    KnockDown = 18,
    Fire = 19,
    Spider = 20,
    Rat = 21,
    Poopman = 22,
}

internal static class AdminActions
{
    /// <summary>Done in the target's own game, to their real body (the rest happen in the host's world).</summary>
    public static bool OnTheirBody(AdminAction action) => action switch
    {
        AdminAction.Heal or AdminAction.BringHere or AdminAction.Slap or AdminAction.Yeet or AdminAction.Spin or
            AdminAction.Freeze or AdminAction.Starve or AdminAction.PissAlarm or AdminAction.Shit or AdminAction.KnockDown => true,
        _ => false,
    };

    /// <summary>What the target is told ("The host slapped you"), or null to keep it a surprise.</summary>
    public static string Told(AdminAction action) => action switch
    {
        AdminAction.Heal => "patched you up",
        AdminAction.BringHere => "pulled you over",
        AdminAction.Give => "gave you something",
        AdminAction.Slap => "slapped you",
        AdminAction.Yeet => "yeeted you",
        AdminAction.Spin => "spun you around",
        AdminAction.Freeze => "froze you",
        AdminAction.Starve => "made you starving",
        AdminAction.PissAlarm => "filled your bladder",
        AdminAction.Shit => "made you shit yourself",
        AdminAction.KnockDown => "knocked you down",
        _ => null,
    };
}
