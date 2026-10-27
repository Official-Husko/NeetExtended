namespace Coop.World.Talk;

/// <summary>
/// Conversations run on the host. Messages:
///
/// WorldTalk (client to host): kind, then
/// - Start: speaker, how (talk, harm, disgust), the player's lists (<see cref="PlayerMasks"/>);
/// - Advance: conversation id, lists;
/// - Choose: conversation id, choice index, lists;
/// - Leave: conversation id;
/// - Showed: conversation id. The choices came up in the talker's box (the others' boxes follow, see <see cref="Watch.WatchProtocol"/>).
///
/// WorldTalkView (host to clients): kind, then
/// - Line (to the talker): id, speaker, flags, the line's voice seed, text, choices;
/// - Close (to the talker): id;
/// - Busy (to the one who asked): speaker, who's talking to them;
/// - Left (to everyone): speaker (it walked off; nobody can talk to it any more).
///
/// WorldPhoneButton (client to host): handset, button. WorldPhones (host to all): each handset's screen and sounds.
/// </summary>
internal static class TalkProtocol
{
    public const byte Start = 0;
    public const byte Advance = 1;
    public const byte Choose = 2;
    public const byte Leave = 3;
    public const byte Showed = 4;

    public const byte Line = 0;
    public const byte Close = 1;
    public const byte Busy = 2;
    public const byte Left = 3;

    // How a conversation starts (CharacterStories: talking, being hurt, something gross thrown at them).
    public const byte Talk = 0;
    public const byte Harm = 1;
    public const byte Disgust = 2;

    // Line flags.
    public const byte HasText = 1;
    public const byte IsChoice = 2;
    public const byte More = 4;
    public const byte Closed = 8;
    public const byte KeepText = 16;
    public const byte SpeakerLeft = 32;
    public const byte OfferingTime = 64;
    public const byte BucketOff = 128;

    public static byte KindOf(string flow)
    {
        if (flow == null)
            return Talk;
        if (flow.EndsWith("_Harm", System.StringComparison.Ordinal))
            return Harm;
        if (flow.EndsWith("_Disgust", System.StringComparison.Ordinal))
            return Disgust;
        return Talk;
    }

    // What a speaker says when someone else is talking to them; {0} is that player.
    public static readonly string[] BusyLines =
    {
        "<size=150%>WAIT UR TURN</size>\nim talkin to {0} dipshit",
        "One at a time asshole.\n{0} was here first.",
        "Shut up shut up shut up.\nIm busy with {0}.",
    };
}
