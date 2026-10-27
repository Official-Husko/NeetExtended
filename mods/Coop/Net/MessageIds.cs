namespace Coop.Net;

/// <summary>
/// Every message type, in one place so ids never clash. Ranges: 1-19 session, 20-39 join,
/// 40-59 players, 60-79 assets (files), 100-199 world. Raise <see cref="Protocol.Version"/> when a payload changes.
/// </summary>
public static class MessageIds
{
    // Session
    public const ushort Hello = 1;
    public const ushort Welcome = 2;
    public const ushort PlayerJoined = 3;
    public const ushort PlayerLeft = 4;
    public const ushort Ready = 5;
    public const ushort Resync = 6;
    public const ushort ModAction = 7;
    public const ushort ModState = 8;
    public const ushort ModSetup = 9;

    // Join
    public const ushort Snapshot = 20;
    public const ushort SendHouse = 21;

    // Players
    public const ushort Pose = 40;
    public const ushort Appearance = 41;
    public const ushort Body = 42;
    public const ushort Downed = 43;
    public const ushort Revive = 44;
    public const ushort CarryRequest = 45;
    public const ushort CarryState = 46;

    // Assets: 60-79, see Assets/.
    public const ushort AssetWant = 60;
    public const ushort AssetChunk = 61;
    public const ushort AssetAck = 62;
    public const ushort AssetMissing = 63;
    public const ushort AssetCancel = 64;
    public const ushort AssetOffer = 65;
    public const ushort AssetPath = 66;
    public const ushort AssetFace = 67;
    public const ushort AssetMedia = 68;
    public const ushort AssetManifest = 69;

    // World: 100-199, see World/.
    public const ushort WorldSpawn = 100;
    public const ushort WorldDestroy = 101;
    public const ushort WorldBodies = 102;
    public const ushort WorldSettled = 103;
    public const ushort WorldClaim = 104;
    public const ushort WorldOwner = 105;
    public const ushort WorldNpcPoses = 106;
    public const ushort WorldNpcEvent = 107;
    public const ushort WorldState = 108;
    public const ushort MoneyDelta = 109;
    public const ushort MoneyTotal = 110;
    public const ushort WorldManifest = 111;
    public const ushort WorldStocks = 112;
    public const ushort WorldStockOrder = 113;
    public const ushort WorldTaxes = 114;
    public const ushort WorldTaxPay = 115;
    public const ushort WorldCards = 116;
    public const ushort WorldCardAction = 117;
    public const ushort WorldFire = 118;
    public const ushort WorldFireOut = 119;
    public const ushort WorldCopVan = 120;
    public const ushort WorldForward = 121;
    public const ushort WorldHealth = 122;
    public const ushort WorldEmail = 123;
    public const ushort WorldPurchase = 124;
    public const ushort WorldPurchaseResult = 125;
    public const ushort WorldApps = 126;
    public const ushort WorldBoxExtra = 127;
    public const ushort WorldTalk = 128;
    public const ushort WorldTalkView = 129;
    public const ushort WorldPhoneButton = 130;
    public const ushort WorldPhones = 131;
    public const ushort WorldSoulTrade = 132;
    public const ushort WorldStoryFunction = 133;
    public const ushort WorldPaint = 134;
    public const ushort WorldPaintDry = 135;
    public const ushort WorldSky = 136;
    public const ushort WorldToilet = 137;
    public const ushort WorldTv = 138;
}
