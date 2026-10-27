using System.Collections.Generic;
using Mixtapes.Audio;
using Mixtapes.Tapes;
using NeetModKit.Events;
using NeetModKit.Game;
using NeetModKit.Menu;
using NeetModKit.Ui.Toolkit;
using UnityEngine.UI;

namespace Mixtapes.Menu;

/// <summary>Mod menu tab listing every mixtape, with a button to get one.</summary>
internal sealed class MixtapesPage : MenuPage
{
    private Text _status;

    public override string Title => "Mixtapes";
    public override int Order => 33;

    public override void Build(UiBuilder ui)
    {
        ui.Text(MenuPalette.Paint(
            "Songs from each mod's mixtapes folder, as cassettes for the radio. Hold the radio, open it, and use a tape on it. " +
            "Add MP3, OGG or WAV files to the folder and they show up here within a second.", MenuPalette.Muted));

        var top = ui.Row(8f, 26f);
        top.Button("RELOAD TAPES", () =>
        {
            TapeLoader.Reload();
            Rebuild();
        }, 180f);
        _status = top.Text("");

        if (TapeLoader.Problems.Count > 0)
        {
            var panel = ui.Panel();
            panel.SubHeader(MenuPalette.Paint($"PROBLEMS ({TapeLoader.Problems.Count})", MenuPalette.Bad));
            foreach (string problem in TapeLoader.Problems)
                panel.Text(MenuPalette.Paint(problem, MenuPalette.Bad));
        }

        var list = ui.Scroll(out _);
        if (TapeLoader.Tapes.Count == 0)
            list.Text("No tapes. Put a song in Mods/Mixtapes/mixtapes/.");
        foreach (var tape in TapeLoader.Tapes)
            BuildTape(list.Panel(), tape);
    }

    private void BuildTape(UiBuilder panel, Tape tape)
    {
        var top = panel.Row(10f, 26f);
        top.SubHeader(tape.Name.ToUpperInvariant());
        top.Text(MenuPalette.Paint(tape.Source, MenuPalette.Muted));
        panel.Text(Describe(tape));
        panel.Row(8f, 26f).Button("GET ONE", () => Report(Give(tape)), 140f);
    }

    private static string Describe(Tape tape)
    {
        var parts = new List<string>();
        parts.Add(tape.Sold ? $"In the store for {tape.Price:0}$" : "Not sold in the store");
        if (tape.Room != null)
            parts.Add($"left in {tape.Room} once per save");
        string error = SongLibrary.ErrorOf(tape.File);
        if (error != null)
            parts.Add(MenuPalette.Paint("the song " + error, MenuPalette.Bad));
        else if (SongLibrary.Loaded(tape.File) == null)
            parts.Add("loading the song");
        else
            parts.Add($"{SongLibrary.Loaded(tape.File).length / 60f:0.0} minutes");
        return string.Join(", ", parts) + ".";
    }

    private static string Give(Tape tape)
    {
        if (!GameEvents.IsHouseReady || !Spawning.IsItem(tape.ItemName))
            return "Tapes only exist in the house.";
        Spawning.ItemNearPlayer(tape.ItemName);
        return $"{tape.Name} is in front of you.";
    }

    private void Report(string message)
    {
        if (_status != null)
            _status.text = message;
    }
}
