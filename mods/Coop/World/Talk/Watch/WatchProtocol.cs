namespace Coop.World.Talk.Watch;

/// <summary>
/// Everyone near a conversation sees it. The host tells all players what the talker's dialogue box
/// does, one event per thing the talker sees (their own game draws the same box from the same
/// events, so nobody has a timer of their own deciding when a line changes).
///
/// WorldTalkWatch (host to all): kind, the speaker (<see cref="SpeakerRef"/>), then
/// - Show: talker slot, flags, text. A new line; the typing and the voice start over;
/// - Choices: count, texts. The talker pressed Use and the choices came up;
/// - Pick: index. The talker picked one;
/// - Close: keep text. The conversation is over; with keep text the last line stays up.
///
/// One conversation per speaker, so the speaker is the key. A talker's client tells the host when
/// its choices come up (WorldTalk, <see cref="TalkProtocol.Showed"/>); everything else the host
/// already knows. Each game decides for itself whether it is close enough to show it
/// (<see cref="Watching"/>).
/// </summary>
internal static class WatchProtocol
{
    public const byte Show = 0;
    public const byte Choices = 1;
    public const byte Pick = 2;
    public const byte Close = 3;

    // Show flags.
    public const byte PlayerVoice = 1;
}
