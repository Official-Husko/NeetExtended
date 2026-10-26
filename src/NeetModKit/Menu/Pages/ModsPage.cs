using System.IO;
using System.Linq;
using NeetLoader.Mods;
using UnityEngine;

namespace NeetModKit.Menu.Pages;

/// <summary>Installed mods: list, details, recent log and the load/unload/reload/on-off controls.</summary>
internal sealed class ModsPage : MenuPage
{
    private const float ListWidth = 280f;

    private readonly ModLogView _log = new ModLogView();
    private ModInfo _selected;
    private (string Files, string Needs, string Heading) _cached;
    private ModInfo _cachedFor;
    private string _lastResult;
    private Vector2 _listScroll, _detailScroll;

    public override string Title => "Mods";
    public override int Order => 0;
    public override bool FillsHeight => true;

    // Folder contents can change between visits (new builds, new mods).
    public override void OnShow() => _cachedFor = null;

    public override void Draw()
    {
        GUILayout.BeginHorizontal(GUILayout.ExpandHeight(true));

        GUILayout.BeginVertical(MenuStyles.Box, GUILayout.Width(ListWidth), GUILayout.ExpandHeight(true));
        Widgets.Header($"Installed ({ModRegistry.Mods.Count})");
        _listScroll = Widgets.BeginScroll(_listScroll);
        DrawList();
        GUILayout.EndScrollView();
        GUILayout.EndVertical();

        GUILayout.BeginVertical(MenuStyles.Box, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        _detailScroll = Widgets.BeginScroll(_detailScroll);
        if (_selected != null)
            DrawDetails(_selected);
        else
            Widgets.Text("No mods installed. Drop a mod folder into the game's Mods folder and press Find new mods.");
        GUILayout.EndScrollView();
        GUILayout.EndVertical();

        GUILayout.EndHorizontal();
        DrawFooter();
    }

    private void DrawList()
    {
        foreach (var mod in ModRegistry.Mods)
        {
            _selected ??= mod;
            string label = MenuPalette.Paint("●", StateColor(mod)) + "  " + mod.Manifest.DisplayName;
            if (GUILayout.Button(label, mod == _selected ? MenuStyles.ListItemActive : MenuStyles.ListItem))
            {
                _selected = mod;
                _detailScroll = Vector2.zero;
            }
        }
    }

    private void DrawDetails(ModInfo mod)
    {
        var m = mod.Manifest;
        if (_cachedFor != mod)
            Cache(mod);
        Widgets.Header(_cached.Heading);
        if (!string.IsNullOrEmpty(m.Description))
            Widgets.Text(m.Description);
        Widgets.Space();

        GUILayout.Label("Status: " + StateText(mod), MenuStyles.Colored(MenuStyles.Text, StateColor(mod)));
        if (mod.Error != null)
            GUILayout.Label(mod.Error, MenuStyles.Colored(MenuStyles.Text, MenuStyles.Bad));
        Widgets.Space();

        DrawActions(mod);
        Widgets.Space();

        Widgets.Section("Details", () =>
        {
            Row("Id", mod.Id);
            Row("Folder", mod.Directory);
            Row("Files", _cached.Files);
            Row("Needs", _cached.Needs);
            Row("Hot reload", mod.CanHotReload ? "yes, updates without a restart" : "no, changes need a restart");
        });

        Widgets.Section("Recent log", () =>
        {
            var lines = _log.Get(mod.Id);
            if (lines.Count == 0)
                Widgets.Small("Nothing logged yet.");
            for (int i = 0; i < lines.Count; i++)
                GUILayout.Label(lines[i], MenuStyles.Small);
        });
    }

    /// <summary>Builds the strings that need disk access or joins once per selection.</summary>
    private void Cache(ModInfo mod)
    {
        var m = mod.Manifest;
        string by = m.Author != null ? " by " + m.Author : "";
        string files = Directory.Exists(mod.Directory)
            ? string.Join(", ", Directory.GetFiles(mod.Directory, "*.dll").Select(Path.GetFileName))
            : "(folder missing)";
        string needs = m.Dependencies.Count > 0 ? string.Join(", ", m.Dependencies.Select(d => $"{d.Key} {d.Value}")) : "nothing";
        _cached = (files, needs, $"{m.DisplayName}  <size=12>{MenuPalette.Paint($"v{m.Version}{by}", MenuPalette.Muted)}</size>");
        _cachedFor = mod;
    }

    private void DrawActions(ModInfo mod)
    {
        GUILayout.BeginHorizontal();
        if (mod.State == ModState.Loaded)
        {
            if (mod.CanHotReload && Widgets.Button("Reload", 110))
                ModRegistry.Reload(mod.Id, Report("Reloaded."));
            if (Widgets.Button("Unload", 110))
                ModRegistry.Unload(mod.Id, Report("Unloaded."));
        }
        else if (Widgets.Button("Load now", 110))
        {
            ModRegistry.Load(mod.Id, Report("Loaded."));
        }

        Widgets.Space(16);
        bool enabled = ModRegistry.IsEnabled(mod.Id);
        if (Widgets.Toggle("Load when the game starts", enabled) != enabled)
            ModRegistry.SetEnabled(mod.Id, !enabled);
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        if (_lastResult != null)
            Widgets.Small(_lastResult);
    }

    private void DrawFooter()
    {
        GUILayout.BeginHorizontal();
        if (Widgets.Button("Reload all", 120))
            ModRegistry.ReloadAll(Report("Reloaded every hot-reload mod."));
        if (Widgets.Button("Find new mods", 140))
            _lastResult = $"Found {ModRegistry.Rescan().Count} new mod(s).";
        Widgets.Space(16);
        bool auto = ModRegistry.AutoReload;
        if (Widgets.Toggle("Reload mods when their files change", auto) != auto)
            ModRegistry.AutoReload = !auto;
        GUILayout.FlexibleSpace();
        if (ModRegistry.RestartNeeded)
            GUILayout.Label("Restart the game to apply on/off changes", MenuStyles.Colored(MenuStyles.TextLine, MenuStyles.Warn));
        GUILayout.EndHorizontal();
    }

    private static void Row(string label, string value)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, MenuStyles.Colored(MenuStyles.TextLine, MenuStyles.Muted), GUILayout.Width(110));
        GUILayout.Label(value, MenuStyles.Text);
        GUILayout.EndHorizontal();
    }

    private System.Action<string> Report(string success) => error => _lastResult = error ?? success;

    private static string StateText(ModInfo mod) => mod.State switch
    {
        ModState.Loaded => mod.LoadCount > 1 ? $"Running (reloaded {mod.LoadCount - 1}x)" : "Running",
        ModState.Disabled => "Not loaded",
        ModState.Failed => "Failed",
        _ => "Waiting",
    };

    private static Color StateColor(ModInfo mod) => mod.State switch
    {
        ModState.Loaded => MenuStyles.Good,
        ModState.Failed => MenuStyles.Bad,
        ModState.Disabled => MenuStyles.Muted,
        _ => MenuStyles.Warn,
    };
}
