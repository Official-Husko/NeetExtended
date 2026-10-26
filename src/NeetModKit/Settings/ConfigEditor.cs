using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Text;
using NeetModKit.Menu;
using UnityEngine;

namespace NeetModKit.Settings;

/// <summary>Draws editable controls for the public fields of a settings object.</summary>
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

    /// <summary>Draws the fields; returns true if anything changed.</summary>
    public static bool Draw(object settings)
    {
        bool changed = false;
        foreach (var view in Views(settings.GetType()))
        {
            object current = view.Field.GetValue(settings);
            object updated = DrawField(view, current);
            if (!Equals(current, updated))
            {
                view.Field.SetValue(settings, updated);
                changed = true;
            }
            if (view.Help != null)
                Widgets.Small(view.Help);
            Widgets.Space(4);
        }
        return changed;
    }

    private static object DrawField(FieldView view, object value)
    {
        var type = view.Field.FieldType;
        if (type == typeof(bool))
            return Widgets.Toggle(view.Label, (bool)value);
        if (type.IsEnum)
            return Enum.Parse(type, view.EnumNames[Widgets.Choice(view.Label, Array.IndexOf(view.EnumNames, value.ToString()), view.EnumNames)]);
        if (type == typeof(float) && view.Range != null)
            return Widgets.Slider(view.Label, (float)value, view.Range.Min, view.Range.Max);
        if (type == typeof(int) && view.Range != null)
            return Mathf.RoundToInt(Widgets.Slider(view.Label, (int)value, view.Range.Min, view.Range.Max, "0"));
        if (type == typeof(string))
            return Widgets.TextField(view.Label, (string)value);
        if (type == typeof(float) || type == typeof(int) || type == typeof(double))
            return ParseOr(Widgets.TextField(view.Label, Convert.ToString(value, CultureInfo.InvariantCulture)), type, value);

        Widgets.Small($"{view.Label}: edit in the config file");
        return value;
    }

    private static object ParseOr(string text, Type type, object fallback)
    {
        try { return Convert.ChangeType(text, type, CultureInfo.InvariantCulture); }
        catch (FormatException) { return fallback; }
        catch (OverflowException) { return fallback; }
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
