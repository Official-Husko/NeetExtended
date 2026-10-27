using System.ComponentModel;
using NeetModKit.Settings;

namespace Coop;

/// <summary>Stored in Mods/Coop/config.json and shown in the mod menu.</summary>
public sealed class CoopConfig
{
    [Label("Your name")]
    [Description("What the other players see above your head.")]
    public string PlayerName = "";

    [Label("Port")]
    [Description("The UDP port a hosted game listens on. Whoever hosts has to let it through their router and firewall.")]
    [Range(1024, 65535)]
    public int Port = 8889;

    [Label("Last address")]
    [Description("The address you joined last, filled in for next time.")]
    public string LastAddress = "";

    [Label("Max players")]
    [Description("How many people a hosted game takes, you included.")]
    [Range(2, 4)]
    public int MaxPlayers = 4;

    [Label("Physics hands on other players (experimental)")]
    [Description("Other players get the floppy, physics-driven hands you see on yourself, following where their hands really are. Off: the plain arms of their body.")]
    public bool PhysicsHands = false;

    [Label("Hitman line cooldown (minutes)")]
    [Description("How long after a hit is ordered before the hitman line (448-626) takes another job. The host's setting counts.")]
    [Range(0, 120)]
    [Shared]
    public int HitmanCooldown = 10;

    [Label("Biggest file to share (MB)")]
    [Description("Custom photos and tapes, faces, wallpapers, TV videos and mixtapes are sent to the other players. Files bigger than this aren't sent or taken; others see what the game shows without them.")]
    [Range(1, 4096)]
    public int MaxFileMb = 512;

    [Label("File upload speed (KB/s)")]
    [Description("How fast this game sends those files to the others, so a big video doesn't slow the game down.")]
    [Range(64, 102400)]
    public int UploadKbps = 4096;

    [Label("Get files before joining")]
    [Description("When you join, the files already in the game (TV videos, mixtapes, custom pictures and tapes, photos, faces) come first, and the house loads once they're here. Off: the house loads straight away and the files follow.")]
    public bool GetFilesFirst = true;

    [Label("Show file transfers")]
    [Description("A note in the bottom right corner while files are sent or received, with how far along they are.")]
    public bool ShowFileNotices = true;

    [Label("File cache size (MB)")]
    [Description("Files from other players are kept in Mods/Coop/cache, so each is only sent once. When it's full, the ones used longest ago go.")]
    [Range(64, 65536)]
    public int CacheMb = 2048;

    /// <summary>Identifies this install to hosts, so a player who comes back gets their own body back.</summary>
    [Hidden]
    public string PlayerId = "";
}
