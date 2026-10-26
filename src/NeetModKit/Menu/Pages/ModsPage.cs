using System.IO;
using System.Linq;
using NeetLoader.Mods;
using NeetModKit.Features;
using NeetModKit.Ui.Toolkit;
using UnityEngine;
using UnityEngine.UI;

namespace NeetModKit.Menu.Pages;

/// <summary>Installed mods: list, details, features, recent log and the load/unload/reload controls.</summary>
internal sealed class ModsPage : MenuPage
{
    private const float ListWidth = 250f;

    private readonly ModLogView _log = new ModLogView();
    private ModInfo _selected;
    private string _lastResult;

    private Text _status, _result, _logText, _restartNote;

    public override string Title => "Mods";
    public override int Order => 0;

    public override void Build(UiBuilder ui)
    {
        if (_selected == null || !ModRegistry.Mods.Contains(_selected))
            _selected = ModRegistry.Mods.FirstOrDefault();

        var body = ui.Row(12f);
        UiBuilder.Size(body.Root, flexibleHeight: 1f);
        BuildList(body.Column(ListWidth, flexibleHeight: 1f));
        BuildDetails(body.Column(flexibleWidth: 1f, flexibleHeight: 1f));
        BuildFooter(ui.Row(8f, 26f));
    }

    public override void Refresh()
    {
        if (_selected == null)
            return;
        _status.text = "STATUS: " + StateText(_selected);
        _status.color = StateColor(_selected);
        _result.text = _lastResult ?? "";
        if (_log.TryGet(_selected.Id, out string log))
            _logText.text = log;
        _restartNote.text = ModRegistry.RestartNeeded ? MenuPalette.Paint("Restart the game to apply on/off changes", MenuPalette.Warn) : "";
    }

    private void BuildList(UiBuilder column)
    {
        column.SubHeader($"INSTALLED ({ModRegistry.Mods.Count})");
        var list = column.Scroll(out _);
        foreach (var mod in ModRegistry.Mods)
        {
            var target = mod;
            var button = list.Button(mod.Manifest.DisplayName, () => Select(target), -1f, accent: mod == _selected);
            ColorLabel(button, mod == _selected ? Color.white : StateColor(mod));
        }
    }

    private void BuildDetails(UiBuilder column)
    {
        var details = column.Scroll(out _);
        if (_selected == null)
        {
            details.Text("No mods installed. Drop a mod folder into the game's Mods folder and press FIND NEW MODS.");
            return;
        }

        var mod = _selected;
        var m = mod.Manifest;
        details.Header(m.DisplayName.ToUpperInvariant());
        details.Text(MenuPalette.Paint($"v{m.Version}{(m.Author != null ? "  by " + m.Author : "")}", MenuPalette.Muted));
        if (!string.IsNullOrEmpty(m.Description))
            details.Text(m.Description);

        _status = details.SubHeader("");
        if (mod.Error != null)
            details.Text(mod.Error, MenuPalette.Bad);

        var actions = details.Row(8f, 26f);
        if (mod.State == ModState.Loaded)
        {
            if (mod.CanHotReload)
                actions.Button("RELOAD", () => ModRegistry.Reload(mod.Id, Report("Reloaded.")));
            actions.Button("UNLOAD", () => ModRegistry.Unload(mod.Id, Report("Unloaded.")));
        }
        else
        {
            actions.Button("LOAD NOW", () => ModRegistry.Load(mod.Id, Report("Loaded.")));
        }
        actions.Space(10f);
        actions.Toggle("Load when the game starts", ModRegistry.IsEnabled(mod.Id), on => ModRegistry.SetEnabled(mod.Id, on));
        _result = details.Text("", MenuPalette.Muted);

        BuildFeatures(details, mod);
        BuildInfo(details, mod);

        details.SubHeader("RECENT LOG");
        _logText = details.Text("");
    }

    private void BuildFeatures(UiBuilder details, ModInfo mod)
    {
        var features = ModFeatures.Of(mod);
        if (features.Count == 0)
            return;

        details.SubHeader("FEATURES");
        var panel = details.Panel();
        foreach (var feature in features)
        {
            var target = feature;
            panel.Toggle(feature.Name, ModFeatures.IsWanted(feature), on => ModFeatures.SetEnabled(target, on));
            string note = feature.AppliesWhen != null ? "  " + MenuPalette.Paint("(" + feature.AppliesWhen + ")", MenuPalette.Warn) : "";
            panel.Text(MenuPalette.Paint(feature.Description, MenuPalette.Muted) + note);
        }
    }

    private static void BuildInfo(UiBuilder details, ModInfo mod)
    {
        details.SubHeader("DETAILS");
        var panel = details.Panel();
        string files = Directory.Exists(mod.Directory)
            ? string.Join(", ", Directory.GetFiles(mod.Directory, "*.dll").Select(Path.GetFileName))
            : "(folder missing)";
        string needs = mod.Manifest.Dependencies.Count > 0
            ? string.Join(", ", mod.Manifest.Dependencies.Select(d => $"{d.Key} {d.Value}"))
            : "nothing";
        Line(panel, "Id", mod.Id);
        Line(panel, "Folder", mod.Directory);
        Line(panel, "Files", files);
        Line(panel, "Needs", needs);
        Line(panel, "Hot reload", mod.CanHotReload ? "yes, updates without a restart" : "no, changes need a restart");
    }

    private void BuildFooter(UiBuilder footer)
    {
        footer.Button("RELOAD ALL", () => ModRegistry.ReloadAll(Report("Reloaded every hot-reload mod.")));
        footer.Button("FIND NEW MODS", () =>
        {
            _lastResult = $"Found {ModRegistry.Rescan().Count} new mod(s).";
            Rebuild();
        }, 160f);
        footer.Space(10f);
        footer.Toggle("Reload mods when their files change", ModRegistry.AutoReload, on => ModRegistry.AutoReload = on);
        _restartNote = footer.Text("");
    }

    private void Select(ModInfo mod)
    {
        _selected = mod;
        _lastResult = null;
        Rebuild();
    }

    private System.Action<string> Report(string success) => error =>
    {
        _lastResult = error ?? success;
        Rebuild(); // State and buttons may have changed.
    };

    private static void Line(UiBuilder panel, string label, string value) =>
        panel.Text(MenuPalette.Paint(label + ":", MenuPalette.Muted) + "  " + value);

    private static void ColorLabel(Button button, Color color)
    {
        var text = button.GetComponentInChildren<Text>();
        if (text != null)
            text.color = color;
    }

    private static string StateText(ModInfo mod) => mod.State switch
    {
        ModState.Loaded => mod.LoadCount > 1 ? $"RUNNING (RELOADED {mod.LoadCount - 1}X)" : "RUNNING",
        ModState.Disabled => "NOT LOADED",
        ModState.Failed => "FAILED",
        _ => "WAITING",
    };

    private static Color StateColor(ModInfo mod) => mod.State switch
    {
        ModState.Loaded => MenuPalette.Good,
        ModState.Failed => MenuPalette.Bad,
        ModState.Disabled => MenuPalette.Muted,
        _ => MenuPalette.Warn,
    };
}
