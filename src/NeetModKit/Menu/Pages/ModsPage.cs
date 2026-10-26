using NeetLoader.Mods;
using UnityEngine;

namespace NeetModKit.Menu.Pages;

/// <summary>Installed mods: state, details and the load/unload/reload/on-off controls.</summary>
internal sealed class ModsPage : MenuPage
{
    private ModInfo _selected;
    private string _lastResult;

    public override string Title => "Mods";
    public override int Order => 0;

    public override void Draw()
    {
        GUILayout.BeginHorizontal();

        GUILayout.BeginVertical(GUILayout.Width(300));
        DrawList();
        GUILayout.EndVertical();

        Widgets.Space(10);

        GUILayout.BeginVertical();
        if (_selected != null)
            DrawDetails(_selected);
        else
            Widgets.Text("Pick a mod on the left.");
        GUILayout.EndVertical();

        GUILayout.EndHorizontal();

        Widgets.Space(10);
        DrawFooter();
    }

    private void DrawList()
    {
        foreach (var mod in ModRegistry.Mods)
        {
            _selected ??= mod;
            string label = $"<color=#{ColorUtility.ToHtmlStringRGB(StateColor(mod))}>●</color>  {mod.Manifest.DisplayName}";
            if (GUILayout.Button(label, mod == _selected ? MenuStyles.ListItemActive : MenuStyles.ListItem))
                _selected = mod;
        }
    }

    private void DrawDetails(ModInfo mod)
    {
        var m = mod.Manifest;
        Widgets.Header($"{m.DisplayName}  <size=12><color=#999999>v{m.Version}{(m.Author != null ? " by " + m.Author : "")}</color></size>");
        if (!string.IsNullOrEmpty(m.Description))
            Widgets.Text(m.Description);
        Widgets.Space();

        GUILayout.Label($"Status: {StateText(mod)}", MenuStyles.Colored(MenuStyles.Text, StateColor(mod)));
        if (mod.Error != null)
            GUILayout.Label(mod.Error, MenuStyles.Colored(MenuStyles.Small, MenuStyles.Bad));
        if (m.Dependencies.Count > 0)
            Widgets.Small("Needs: " + string.Join(", ", m.Dependencies.Keys));
        Widgets.Small(mod.CanHotReload ? "Supports hot reload" : "Changes need a game restart");
        Widgets.Space();

        GUILayout.BeginHorizontal();
        if (mod.State == ModState.Loaded)
        {
            if (mod.CanHotReload && Widgets.Button("Reload", 110))
                ModRegistry.Reload(mod.Id, Report("Reloaded"));
            if (Widgets.Button("Unload", 110))
                ModRegistry.Unload(mod.Id, Report("Unloaded"));
        }
        else if (Widgets.Button("Load now", 110))
        {
            ModRegistry.Load(mod.Id, Report("Loaded"));
        }
        GUILayout.EndHorizontal();

        Widgets.Space();
        bool enabled = ModRegistry.IsEnabled(mod.Id);
        if (Widgets.Toggle("Load when the game starts", enabled) != enabled)
            ModRegistry.SetEnabled(mod.Id, !enabled);

        if (_lastResult != null)
        {
            Widgets.Space();
            Widgets.Small(_lastResult);
        }
    }

    private void DrawFooter()
    {
        GUILayout.BeginHorizontal();
        if (Widgets.Button("Reload all", 120))
            ModRegistry.ReloadAll(Report("Reloaded every hot-reload mod"));
        if (Widgets.Button("Find new mods", 140))
            _lastResult = $"Found {ModRegistry.Rescan().Count} new mod(s).";
        bool auto = ModRegistry.AutoReload;
        if (Widgets.Toggle("Reload mods when their files change", auto) != auto)
            ModRegistry.AutoReload = !auto;
        GUILayout.FlexibleSpace();
        if (ModRegistry.RestartNeeded)
            GUILayout.Label("Restart the game to apply on/off changes", MenuStyles.Colored(MenuStyles.Small, MenuStyles.Warn));
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
