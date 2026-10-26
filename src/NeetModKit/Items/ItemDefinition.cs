using System;
using UnityEngine;

namespace NeetModKit.Items;

/// <summary>
/// A new item made by copying an existing one and changing what's needed. Only
/// <see cref="Name"/> and <see cref="BasedOn"/> are required.
/// </summary>
public sealed class ItemDefinition
{
    /// <summary>Unique name. It is the save key, the console "get" name and the store label.</summary>
    public string Name;

    /// <summary>Existing item to copy (any name from the item list, e.g. "Mug").</summary>
    public string BasedOn;

    /// <summary>Store description. Null keeps the original.</summary>
    public string Description;

    /// <summary>Store price. Null keeps the original.</summary>
    public float? Price;

    /// <summary>Which online store sells it. Null keeps the original; Nowhere hides it from stores.</summary>
    public Usable.StoreType? Store;

    /// <summary>Replaces the main texture on every renderer.</summary>
    public Texture2D Texture;

    /// <summary>Multiplies the colour of every renderer's material.</summary>
    public Color? Tint;

    /// <summary>Uniform scale on top of the original size.</summary>
    public float Scale = 1f;

    /// <summary>Store picture. Null uses the copied item's picture.</summary>
    public Sprite StorePicture;

    /// <summary>
    /// Last chance to change the item, e.g. add your own components. Runs once on the template
    /// every spawned copy is made from.
    /// </summary>
    public Action<GameObject> Setup;
}
