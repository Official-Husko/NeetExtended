using NeetModKit.Menu;
using NeetModKit.Ui.Toolkit;
using TvStations.Stations;
using TvStations.Tv;
using UnityEngine.UI;

namespace TvStations.Menu;

/// <summary>Mod menu tab: the TV's channels in remote order, with a button to watch each.</summary>
internal sealed class TvPage : MenuPage
{
    private Text _status;

    public override string Title => "TV";
    public override int Order => 35;

    public override void Build(UiBuilder ui)
    {
        ui.Text(MenuPalette.Paint(
            "Videos (MP4 or WebM) in any mod's tv folder become stations on the living room TV. The remote's six buttons pick " +
            "channels 1 to 6; hitting the remote goes to the next one. A stations.jsonc in the folder names them, sets their " +
            "volume, puts them in place of a game channel and hides game channels.", MenuPalette.Muted));

        var top = ui.Row(8f, 26f);
        top.Button("RELOAD STATIONS", () =>
        {
            StationLoader.Reload();
            Rebuild();
        }, 200f);
        top.Button("TV OFF", () => Report(TurnOff()), 120f);
        _status = top.Text("");

        if (StationLoader.Problems.Count > 0)
        {
            var problems = ui.Panel();
            problems.SubHeader(MenuPalette.Paint($"PROBLEMS ({StationLoader.Problems.Count})", MenuPalette.Bad));
            foreach (string problem in StationLoader.Problems)
                problems.Text(MenuPalette.Paint(problem, MenuPalette.Bad));
        }

        var list = ui.Scroll(out _);
        var channels = TvChannels.Current;
        if (channels.Count == 0)
        {
            list.Text("The TV's channels show up here once you're in the house.");
            return;
        }
        for (int i = 0; i < channels.Count; i++)
            BuildChannel(list.Panel(), i, channels[i]);
    }

    private void BuildChannel(UiBuilder panel, int index, ScreenModule channel)
    {
        var video = TvChannels.VideoFor(channel);
        var row = panel.Row(10f, 26f);
        var number = row.SubHeader((index + 1).ToString());
        UiBuilder.Size(number, width: 30f, flexibleWidth: 0f);
        var name = row.SubHeader((video != null ? video.Station.Name : TvChannels.GameName(channel)).ToUpperInvariant());
        UiBuilder.Size(name, width: 260f, flexibleWidth: 0f);
        row.Text(video == null
            ? MenuPalette.Paint("the game's channel", MenuPalette.Muted)
            : MenuPalette.Paint(video.Station.Source + (video.Station.Replaces != null ? $", in place of {video.Station.Replaces}" : ""), MenuPalette.Muted));
        int target = index;
        row.Button("WATCH", () => Report(Watch(target)), 110f);

        if (video?.Problem != null)
            panel.Text(MenuPalette.Paint("Doesn't play: " + video.Problem, MenuPalette.Bad));
    }

    private static string Watch(int index)
    {
        var tv = TvChannels.Tv;
        if (tv == null)
            return "No TV here.";
        if (!tv.IsOn)
            tv.Control(TV.TVCommand.On);
        tv.Control(index);
        return $"Channel {index + 1} is on.";
    }

    private static string TurnOff()
    {
        var tv = TvChannels.Tv;
        if (tv == null || !tv.IsOn)
            return "The TV is already off.";
        tv.Control(TV.TVCommand.Off);
        return "TV off.";
    }

    private void Report(string message)
    {
        if (_status != null)
            _status.text = message;
    }
}
