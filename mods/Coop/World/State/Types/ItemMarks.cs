using System.Collections.Generic;
using UnityEngine;

namespace Coop.World.State.Types;

/// <summary>
/// Things that happened to an item that the game itself doesn't keep as a value: that it was burnt
/// (the burnt look has no flag of its own, only `broken` for burns by flame) and how often butter or
/// the like was smeared on a food (the tint is a one-way change of the material colour). The
/// patches note them where they happen, the state types send and apply them. Cleared when a session ends.
/// </summary>
internal static class ItemMarks
{
    private static readonly HashSet<int> Burnt = new HashSet<int>();
    private static readonly Dictionary<int, Topping> Toppings = new Dictionary<int, Topping>();

    private struct Topping
    {
        public byte Count;
        public Color32 Colour;
    }

    public static void Clear()
    {
        Burnt.Clear();
        Toppings.Clear();
    }

    public static bool IsBurnt(Usable item) => Burnt.Contains(item.GetInstanceID());

    public static void NoteBurnt(Usable item) => Burnt.Add(item.GetInstanceID());

    public static void Forget(Usable item)
    {
        Burnt.Remove(item.GetInstanceID());
        Toppings.Remove(item.GetInstanceID());
    }

    public static byte ToppingCount(Food food) =>
        Toppings.TryGetValue(food.GetInstanceID(), out var t) ? t.Count : (byte)0;

    public static Color32 ToppingColour(Food food) =>
        Toppings.TryGetValue(food.GetInstanceID(), out var t) ? t.Colour : default;

    public static void SetTopping(Food food, byte count, Color32 colour) =>
        Toppings[food.GetInstanceID()] = new Topping { Count = count, Colour = colour };

    /// <summary>Something was smeared on the food (before the game does it).</summary>
    public static void NoteTopping(Food food, Color colour) =>
        SetTopping(food, (byte)Mathf.Min(ToppingCount(food) + 1, 255), colour);
}
