using NeetModKit.Features;
using TvStations.Tv;

namespace TvStations.Features;

/// <summary>The game's own six channels next to the stations.</summary>
internal sealed class GameChannelsFeature : ModFeature
{
    public override string Id => "GameChannels";
    public override string Name => "Keep the game's channels";
    public override string Description =>
        "Static, Bingo, Snail, Church, Sports and Rap stay on the TV next to your stations. Switch this off to only " +
        "watch your own. To drop single channels, list them under \"hide\" in a stations.jsonc.";

    protected override void OnEnable() => TvChannels.SetKeepGameChannels(true);

    protected override void OnDisable() => TvChannels.SetKeepGameChannels(false);
}
