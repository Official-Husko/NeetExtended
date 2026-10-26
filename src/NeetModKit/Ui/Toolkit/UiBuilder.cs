using UnityEngine;
using UnityEngine.UI;

namespace NeetModKit.Ui.Toolkit;

/// <summary>
/// Adds native-looking UI to a container, top to bottom (or left to right in a row). Every control
/// is a copy of a game element, so it looks and reacts like the game's menus.
/// </summary>
public sealed partial class UiBuilder
{
    public RectTransform Root { get; }

    /// <summary>True for rows (left to right), false for columns (top to bottom).</summary>
    public bool Horizontal { get; }

    public UiBuilder(RectTransform root, bool horizontal = false)
    {
        Root = root;
        Horizontal = horizontal;
    }

    /// <summary>Makes a builder for <paramref name="root"/>, giving it a vertical layout if it has none.</summary>
    public static UiBuilder Vertical(RectTransform root, float spacing = 6f, RectOffset padding = null)
    {
        if (root.GetComponent<HorizontalOrVerticalLayoutGroup>() == null)
            ConfigureLayout(root.gameObject.AddComponent<VerticalLayoutGroup>(), spacing, padding);
        return new UiBuilder(root);
    }

    /// <summary>A horizontal row; returns a builder that adds to it left to right.</summary>
    public UiBuilder Row(float spacing = 8f, float height = -1f)
    {
        var rect = NewChild("Row");
        var layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
        ConfigureLayout(layout, spacing, null);
        layout.childAlignment = TextAnchor.MiddleLeft;
        // A fixed-height row must not grow when the column has spare room.
        Size(rect, height: height, flexibleWidth: 1f, flexibleHeight: height >= 0f ? 0f : -1f);
        return new UiBuilder(rect, horizontal: true);
    }

    /// <summary>A nested vertical group, optionally with a fixed width or taking the remaining width.</summary>
    public UiBuilder Column(float width = -1f, float flexibleWidth = 0f, float flexibleHeight = 0f, float spacing = 6f)
    {
        var rect = NewChild("Column");
        ConfigureLayout(rect.gameObject.AddComponent<VerticalLayoutGroup>(), spacing, null);
        Size(rect, width: width, flexibleWidth: width < 0f && flexibleWidth == 0f ? 1f : flexibleWidth, flexibleHeight: flexibleHeight);
        return new UiBuilder(rect);
    }

    /// <summary>A tinted box with padding, for grouping things.</summary>
    public UiBuilder Panel(Color? tint = null, float flexibleHeight = 0f, int padding = 8)
    {
        var rect = NewChild("Panel");
        var image = rect.gameObject.AddComponent<Image>();
        image.color = tint ?? new Color(1f, 1f, 1f, 0.05f);
        ConfigureLayout(rect.gameObject.AddComponent<VerticalLayoutGroup>(), 6f, new RectOffset(padding, padding, padding, padding));
        Size(rect, flexibleWidth: 1f, flexibleHeight: flexibleHeight);
        return new UiBuilder(rect);
    }

    /// <summary>
    /// A wrapping grid of equal cells (as many columns as fit). Returns a builder adding cells.
    /// </summary>
    public UiBuilder Grid(float cellWidth, float cellHeight, float spacing = 6f)
    {
        var rect = NewChild("Grid");
        var grid = rect.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(cellWidth, cellHeight);
        grid.spacing = new Vector2(spacing, spacing);
        grid.constraint = GridLayoutGroup.Constraint.Flexible;
        grid.childAlignment = TextAnchor.UpperLeft;
        Size(rect, flexibleWidth: 1f);
        return new UiBuilder(rect);
    }

    /// <summary>Empty space; in a row it's horizontal, in a column vertical.</summary>
    public void Space(float pixels = 8f)
    {
        var rect = NewChild("Space");
        if (Horizontal)
            Size(rect, width: pixels);
        else
            Size(rect, height: pixels);
    }

    /// <summary>Pushes the following elements to the far end of a row or column.</summary>
    public void FlexibleSpace()
    {
        var rect = NewChild("Flex");
        if (Horizontal)
            Size(rect, flexibleWidth: 1f);
        else
            Size(rect, flexibleHeight: 1f);
    }

    /// <summary>Removes everything this builder added.</summary>
    public void Clear()
    {
        for (int i = Root.childCount - 1; i >= 0; i--)
            UiCloning.Remove(Root.GetChild(i).gameObject);
    }

    /// <summary>Sets layout sizes; negative means "don't care".</summary>
    public static LayoutElement Size(Component element, float width = -1f, float height = -1f,
        float flexibleWidth = -1f, float flexibleHeight = -1f)
    {
        var layout = element.GetComponent<LayoutElement>() ?? element.gameObject.AddComponent<LayoutElement>();
        if (width >= 0f)
            layout.preferredWidth = layout.minWidth = width;
        if (height >= 0f)
            layout.preferredHeight = layout.minHeight = height;
        if (flexibleWidth >= 0f)
            layout.flexibleWidth = flexibleWidth;
        if (flexibleHeight >= 0f)
            layout.flexibleHeight = flexibleHeight;
        return layout;
    }

    internal RectTransform NewChild(string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = (RectTransform)go.transform;
        rect.SetParent(Root, false);
        return rect;
    }

    internal GameObject Copy(UiTemplate template, string name)
    {
        var go = Object.Instantiate(UiTemplates.Get(template), Root, false);
        go.name = name;
        go.SetActive(true);
        return go;
    }

    private static void ConfigureLayout(HorizontalOrVerticalLayoutGroup layout, float spacing, RectOffset padding)
    {
        layout.spacing = spacing;
        layout.padding = padding ?? new RectOffset(0, 0, 0, 0);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        layout.childAlignment = TextAnchor.UpperLeft;
    }
}
