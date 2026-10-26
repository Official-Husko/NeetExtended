using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Text;
using NeetModKit.Menu;
using NeetModKit.Ui.Toolkit;
using UnityEngine;

namespace NeetModKit.Settings;

/// <summary>Builds controls for the public fields of a settings object and writes edits back.</summary>
internal static class ConfigEditor
{
    private sealed class FieldView
    {
        public FieldInfo Field;
        public string Label;
        public string Help;
        public RangeAttribute Range;
        public string[] EnumNames;
    }

    private static readonly Dictionary<Type, List<FieldView>> Cache = new Dictionary<Type, List<FieldView>>();

    /// <summary>Adds a control per field. <paramref name="changed"/> runs after every edit.</summary>
    public static void Build(UiBuilder ui, object settings, Action changed)
    {
        foreach (var view in Views(settings.GetType()))
        {
            BuildField(ui, view, settings, changed);
            if (view.Help != null)
                ui.Text(MenuPalette.Paint(view.Help, MenuPalette.Muted));
        }
    }

    private static void BuildField(UiBuilder ui, FieldView view, object settings, Action changed)
    {
        var field = view.Field;
        var type = field.FieldType;
        object value = field.GetValue(settings);

        void Set(object v)
        {
            field.SetValue(settings, v);
            changed();
        }

        if (type == typeof(bool))
            ui.Toggle(view.Label, (bool)value, v => Set(v));
        else if (type.IsEnum)
            BuildChoice(ui, view, value.ToString(), name => Set(Enum.Parse(type, name)));
        else if (type == typeof(float) && view.Range != null)
            ui.Slider(view.Label.ToUpperInvariant(), (float)value, view.Range.Min, view.Range.Max, v => Set(v));
        else if (type == typeof(int) && view.Range != null)
            ui.Slider(view.Label.ToUpperInvariant(), (int)value, view.Range.Min, view.Range.Max, v => Set(Mathf.RoundToInt(v)), wholeNumbers: true);
        else if (type == typeof(string))
            ui.LabeledInput(view.Label.ToUpperInvariant(), (string)value, v => Set(v));
        else if (type == typeof(float) || type == typeof(int) || type == typeof(double))
            ui.LabeledInput(view.Label.ToUpperInvariant(), Convert.ToString(value, CultureInfo.InvariantCulture), text =>
            {
                if (TryParse(text, type, out object parsed))
                    Set(parsed);
            });
        else
            ui.Text(MenuPalette.Paint($"{view.Label}: edit in the config file", MenuPalette.Muted));
    }

    private static void BuildChoice(UiBuilder ui, FieldView view, string current, Action<string> pick)
    {
        ui.SubHeader(view.Label.ToUpperInvariant());
        var row = ui.Row(6f, 26f);
        foreach (string name in view.EnumNames)
        {
            string option = name;
            row.Button(name.ToUpperInvariant(), () => pick(option), 120f, accent: name == current);
        }
    }

    private static bool TryParse(string text, Type type, out object value)
    {
        try
        {
            value = Convert.ChangeType(text, type, CultureInfo.InvariantCulture);
            return true;
        }
        catch (Exception e) when (e is FormatException || e is OverflowException)
        {
            value = null;
            return false;
        }
    }

    private static List<FieldView> Views(Type type)
    {
        if (Cache.TryGetValue(type, out var views))
            return views;

        views = new List<FieldView>();
        foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public))
        {
            if (field.IsDefined(typeof(HiddenAttribute), false))
                continue;
            views.Add(new FieldView
            {
                Field = field,
                Label = field.GetCustomAttribute<LabelAttribute>()?.Text ?? Prettify(field.Name),
                Help = field.GetCustomAttribute<DescriptionAttribute>()?.Description,
                Range = field.GetCustomAttribute<RangeAttribute>(),
                EnumNames = field.FieldType.IsEnum ? Enum.GetNames(field.FieldType) : null,
            });
        }
        Cache[type] = views;
        return views;
    }

    /// <summary>"PauseWhileOpen" -> "Pause while open".</summary>
    private static string Prettify(string name)
    {
        var sb = new StringBuilder(name.Length + 8);
        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            if (i > 0 && char.IsUpper(c) && !char.IsUpper(name[i - 1]))
                sb.Append(' ').Append(char.ToLowerInvariant(c));
            else
                sb.Append(i == 0 ? char.ToUpperInvariant(c) : c);
        }
        return sb.ToString();
    }
}
