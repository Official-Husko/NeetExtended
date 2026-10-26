namespace NeetModKit.Ui.Toolkit;

/// <summary>Sizes the builder uses, per theme: the computer screen is far smaller than a menu.</summary>
public sealed class UiMetrics
{
    public float ButtonHeight = 26f;
    public float RowHeight = 22f;
    public float ButtonWidth = 140f;
    public float LabelWidth = 190f;
    public float ReadoutWidth = 60f;
    public float Spacing = 6f;
    public float ScrollbarWidth = 8f;

    public static readonly UiMetrics Menu = new UiMetrics();

    public static readonly UiMetrics Computer = new UiMetrics
    {
        ButtonHeight = 12f,
        RowHeight = 10f,
        ButtonWidth = 40f,
        LabelWidth = 50f,
        ReadoutWidth = 18f,
        Spacing = 2f,
        ScrollbarWidth = 3f,
    };
}
