using System.Collections.Generic;
using NeetLoader.Logging;
using NeetModKit.Ui.Toolkit;
using UnityEngine;
using UnityEngine.UI;

namespace NeetModKit.Menu.Pages;

/// <summary>
/// The error report (NeetLoader/logs/errors.log) in colour: every problem this session, newest
/// first, with how often and where it happened. Copy it to paste into a bug report.
/// </summary>
internal sealed class ErrorsPage : MenuPage
{
    public const string PageTitle = "Errors";
    private const int TitleSize = 17;
    private const int DetailSize = 13;

    private static readonly Color ErrorTint = new Color(0.45f, 0.05f, 0.05f, 0.35f);
    private static readonly Color CaughtTint = new Color(0.45f, 0.3f, 0.02f, 0.3f);

    private readonly HashSet<ErrorReport.Problem> _expanded = new HashSet<ErrorReport.Problem>();
    private readonly List<KeyValuePair<ErrorReport.Problem, Text>> _counts = new List<KeyValuePair<ErrorReport.Problem, Text>>();
    private int _builtVersion = -1;
    private string _status;
    private Text _summary, _statusText;

    public override string Title => PageTitle;
    public override int Order => 40;

    public override void Build(UiBuilder ui)
    {
        _builtVersion = ErrorReport.Version;
        _counts.Clear();
        var problems = ErrorReport.Snapshot();
        problems.Reverse();

        var top = ui.Row(8f, 26f);
        _summary = top.SubHeader(Summary(problems.Count));
        top.FlexibleSpace();
        top.Button("COPY REPORT", Copy, 170f, accent: true);
        top.Button("CLEAR", () =>
        {
            ErrorReport.Clear();
            _expanded.Clear();
            _status = "Cleared. New problems show up here as they happen.";
            Rebuild();
        }, 110f);
        _statusText = ui.Text(_status ?? MenuPalette.Paint("Also saved to NeetLoader/logs/errors.log. The last session's report is errors-previous.log.", MenuPalette.Muted));

        var list = ui.Scroll(out _);
        if (problems.Count == 0)
        {
            var panel = list.Panel();
            panel.SubHeader(MenuPalette.Paint("NOTHING HAS GONE WRONG THIS SESSION", MenuPalette.Good));
            panel.Text(MenuPalette.Paint("Errors from the game and from mods, and problems the fixes catch, are listed here.", MenuPalette.Muted));
            return;
        }
        foreach (var problem in problems)
            BuildProblem(list.Panel(problem.Caught ? CaughtTint : ErrorTint), problem);
    }

    public override void Refresh()
    {
        // New problems rebuild the list; repeats only change the counts.
        if (ErrorReport.Version != _builtVersion)
        {
            Rebuild();
            return;
        }
        foreach (var pair in _counts)
            pair.Value.text = Meta(pair.Key);
    }

    private void BuildProblem(UiBuilder panel, ErrorReport.Problem problem)
    {
        var head = panel.Row(10f, 24f);
        var tag = head.SubHeader(problem.Caught ? "CAUGHT" : "ERROR");
        tag.color = problem.Caught ? MenuPalette.Warn : MenuPalette.Bad;
        // Labels stretch by default; the tag must not, or it takes half the row.
        UiBuilder.Size(tag, width: 90f, flexibleWidth: 0f);
        head.Text(problem.Title, size: TitleSize).alignment = TextAnchor.MiddleLeft;

        _counts.Add(new KeyValuePair<ErrorReport.Problem, Text>(problem, panel.Text(Meta(problem))));

        if (string.IsNullOrEmpty(problem.Details))
            return;
        bool open = _expanded.Contains(problem);
        if (open)
            panel.Text(StackText.Format(problem.Details, problem.Caught ? MenuPalette.Warn : MenuPalette.Bad), size: DetailSize);
        else
            panel.Text(MenuPalette.Paint(FirstLine(problem.Details), MenuPalette.Muted), size: DetailSize);

        var actions = panel.Row(8f, 24f);
        actions.Button(open ? "HIDE DETAILS" : "SHOW DETAILS", () =>
        {
            if (!_expanded.Remove(problem))
                _expanded.Add(problem);
            Rebuild();
        }, 170f);
    }

    private void Copy()
    {
        GUIUtility.systemCopyBuffer = ErrorReport.Text();
        _status = MenuPalette.Paint("Copied. Paste it into your bug report.", MenuPalette.Good);
        if (_statusText != null)
            _statusText.text = _status;
    }

    private static string Summary(int count) =>
        count == 0 ? MenuPalette.Paint("NO PROBLEMS", MenuPalette.Good)
                   : MenuPalette.Paint(count == 1 ? "1 PROBLEM THIS SESSION" : $"{count} PROBLEMS THIS SESSION", MenuPalette.Bad);

    private static string Meta(ErrorReport.Problem problem)
    {
        string times = problem.Count == 1
            ? "once at " + problem.First.ToString("HH:mm:ss")
            : $"{problem.Count} times, first {problem.First:HH:mm:ss}, last {problem.Last:HH:mm:ss}";
        string where = string.IsNullOrEmpty(problem.Scene) ? "" : "  in " + problem.Scene;
        return MenuPalette.Paint(problem.Source, MenuPalette.Accent) + "  " + MenuPalette.Paint(times + where, MenuPalette.Muted);
    }

    private static string FirstLine(string text)
    {
        string trimmed = text.TrimStart();
        int newline = trimmed.IndexOf('\n');
        return newline < 0 ? trimmed : trimmed.Substring(0, newline).TrimEnd() + "  ...";
    }
}
