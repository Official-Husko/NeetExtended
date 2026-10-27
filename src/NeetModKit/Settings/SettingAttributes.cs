using System;

namespace NeetModKit.Settings;

/// <summary>Turns a numeric settings field into a slider in the mod menu.</summary>
[AttributeUsage(AttributeTargets.Field)]
public sealed class RangeAttribute : Attribute
{
    public float Min { get; }
    public float Max { get; }

    public RangeAttribute(float min, float max)
    {
        Min = min;
        Max = max;
    }
}

/// <summary>Keeps a settings field out of the mod menu.</summary>
[AttributeUsage(AttributeTargets.Field)]
public sealed class HiddenAttribute : Attribute
{
}

/// <summary>
/// The setting shapes the shared house (prices, chances, timings), so in co-op the host's value counts:
/// clients get it for the session and see it read-only. Leave it off settings that are about your own
/// game (keys, volume, looks, your name).
/// </summary>
[AttributeUsage(AttributeTargets.Field)]
public sealed class SharedAttribute : Attribute
{
}

/// <summary>Label shown in the mod menu instead of the prettified field name.</summary>
[AttributeUsage(AttributeTargets.Field)]
public sealed class LabelAttribute : Attribute
{
    public string Text { get; }

    public LabelAttribute(string text)
    {
        Text = text;
    }
}
