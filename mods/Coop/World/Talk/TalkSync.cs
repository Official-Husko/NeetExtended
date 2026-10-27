using Coop.Net;

namespace Coop.World.Talk;

/// <summary>Conversations and phones as one part of world sync (see <see cref="TalkProtocol"/>).</summary>
internal static class TalkSync
{
    public static void Init()
    {
        Messages.On(MessageIds.WorldTalk, HostTalks.OnRequest);
        Messages.On(MessageIds.WorldTalkView, ClientTalk.OnView);
        PhoneRequests.Init();
        PhoneScreens.Init();
        SoulTrade.Init();
    }

    public static void Start(bool host)
    {
        Clear();
        if (host)
            TalkExternals.Install();
        else if (Dialogue._ != null && Dialogue._.currSpeaker != null)
        {
            // A conversation from before a reload or a resync: the host has forgotten it.
            Dialogue._.CloseDialogue();
        }
    }

    public static void Stop()
    {
        TalkExternals.Uninstall();
        Clear();
    }

    public static void Update(bool host)
    {
        if (!host)
            ClientTalk.Tick();
        TalkView.Tick();
    }

    public static void HostLateUpdate() => PhoneScreens.HostTick();

    public static void PlayerReady() => PhoneScreens.SendAgain();

    public static void PlayerLeft(byte slot)
    {
        HostTalks.PlayerLeft(slot);
        Players.PlayerFacts.Forget(slot);
    }

    private static void Clear()
    {
        HostTalks.Clear();
        ClientTalk.Clear();
        PhoneRequests.Clear();
        PhoneScreens.Clear();
        Speakers.Clear();
        TalkView.Clear();
        Players.PlayerFacts.Clear();
    }
}
