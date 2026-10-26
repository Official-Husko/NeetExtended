using NeetModKit.Desktop;
using NeetModKit.Ui;
using PumpkinSmash.Game;
using UnityEngine;
using UnityEngine.UI;

namespace PumpkinSmash.UI;

/// <summary>The app page: title, score line, a 3x3 grid of holes and a start button.</summary>
internal sealed class SmashBoard
{
    private static readonly Color Lit = new Color(1f, 0.45f, 0.08f);
    private static readonly Color Dark = new Color(0.25f, 0.18f, 0.3f);

    private readonly SmashRound _round;
    private readonly Button[] _holes = new Button[SmashRound.Holes];
    private Text _score;
    private Button _start;

    public SmashBoard(SmashRound round)
    {
        _round = round;
    }

    /// <summary>The page is rebuilt on every house load; visible means the browser shows it.</summary>
    public GameObject Page { get; private set; }

    public void Build(ComputerAppContext ctx)
    {
        Page = ctx.Panel.gameObject;
        var ui = ctx.Ui;
        ui.Header("PUMPKIN SMASH");
        _score = ui.Text("");

        var grid = ui.Grid(46f, 16f, 3f);
        for (int i = 0; i < _holes.Length; i++)
        {
            int hole = i;
            _holes[i] = grid.Button("", () => _round.Hit(hole), -1f);
        }

        _start = ui.Button("START", StartRound, 60f);
        Refresh();
    }

    public void Refresh()
    {
        if (_score == null)
            return;

        var mod = PumpkinSmashMod.Instance;
        _score.text = _round.Running
            ? $"Score {_round.Score}    Time {Mathf.CeilToInt(_round.TimeLeft)}"
            : $"Best {mod.Best.BestScore}    Rounds {mod.Best.Rounds}";

        for (int i = 0; i < _holes.Length; i++)
        {
            if (_holes[i] == null)
                continue;
            bool up = i == _round.Pumpkin;
            _holes[i].image.color = up ? Lit : Dark;
            UiCloning.SetText(_holes[i].transform, up ? "SMASH!" : "");
        }
        if (_start != null)
            _start.gameObject.SetActive(!_round.Running);
    }

    private void StartRound() => _round.Start(PumpkinSmashMod.Instance.Config.Value.RoundSeconds);
}
