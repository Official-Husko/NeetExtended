using Coop.World.Objects;
using LiteNetLib.Utils;
using UnityEngine;

namespace Coop.World.State.Types;

/// <summary>
/// How cooked and how spoiled a food is. Only the game that simulates the food counts (flames, the
/// microwave and the clock all change it there, see ItemStatePatches), so the others take the numbers
/// from here: cooking in half seconds, spoiling in steps (the clock runs 360 seconds), and whether it
/// reached either end. Foods that turn into another object when cooked or spoiled (a steak, a
/// sandwich) don't get that last step here: the other object arrives as a spawn.
///
/// It also carries what butter and the like did to it: its two food values and how often (and with
/// what colour) it was smeared, which tints it.
///
/// Layout: cooking (ushort, half seconds), flags (bit 0 cooked, bit 1 spoiled), spoil step (byte),
/// raw and cooked food value (floats), smears (byte) and their colour (3 bytes).
/// </summary>
internal sealed class FoodState : StateType<Food>
{
    private const float SpoilStep = 5f;
    private const int SpoilSteps = (int)(360f / SpoilStep);
    private const byte Cooked = 1;
    private const byte Spoiled = 2;

    public override bool OnItems => true;

    protected override void Write(Food food, NetDataWriter w)
    {
        // Foods that can't be cooked still take a flame's heat into their meter; it means nothing.
        float cooking = food.timeToCook > 0f ? food.CookedMeter : 0f;
        w.Put((ushort)Mathf.Clamp(Mathf.FloorToInt(cooking * 2f), 0, ushort.MaxValue));
        byte flags = 0;
        if (food.IsCooked)
            flags |= Cooked;
        if (food.spoiled)
            flags |= Spoiled;
        w.Put(flags);
        w.Put((byte)Mathf.Clamp(Mathf.FloorToInt(food.spoiledMeter / SpoilStep), 0, SpoilSteps));
        w.Put(food.foodValueRaw);
        w.Put(food.foodValueCooked);
        w.Put(ItemMarks.ToppingCount(food));
        var colour = ItemMarks.ToppingColour(food);
        w.Put(colour.r);
        w.Put(colour.g);
        w.Put(colour.b);
    }

    protected override void Apply(Food food, NetDataReader r)
    {
        float meter = r.GetUShort() * 0.5f;
        byte flags = r.GetByte();
        float spoil = r.GetByte() * SpoilStep;

        ApplyCooking(food, meter, (flags & Cooked) != 0);
        ApplySpoiling(food, spoil, (flags & Spoiled) != 0);
        food.foodValueRaw = r.GetFloat();
        food.foodValueCooked = r.GetFloat();
        ApplySmears(food, r.GetByte(), new Color32(r.GetByte(), r.GetByte(), r.GetByte(), 255));
    }

    // Each smear tints the food a bit more towards the colour (the game's own 40 % step).
    private static void ApplySmears(Food food, byte count, Color32 colour)
    {
        byte have = ItemMarks.ToppingCount(food);
        if (count <= have)
            return;
        var renderer = food.GetComponentInChildren<Renderer>();
        for (int n = have; n < count && renderer != null; n++)
        {
            foreach (var material in renderer.materials)
            {
                if (material.HasProperty("_Color"))
                    material.SetColor("_Color", Color.Lerp(material.GetColor("_Color"), colour, 0.4f));
            }
        }
        ItemMarks.SetTopping(food, count, colour);
    }

    private static void ApplyCooking(Food food, float meter, bool cooked)
    {
        if (food.timeToCook <= 0f)
            return;
        // A cooked food stays cooked here, and a rounded meter never cooks one on its own.
        if (cooked)
            meter = Mathf.Max(meter, food.timeToCook);
        else
            meter = Mathf.Min(meter, food.timeToCook - 0.01f);
        if (cooked ? food.IsCooked : Mathf.Abs(food.CookedMeter - meter) < 0.4f)
            return;
        if (cooked && food.swapOnCookedObj != null)
            return; // The cooked version comes as a spawn.

        // Cooking a Seafood Delight past its limit makes it pop, and that happens where it's simulated.
        var seafood = food.GetComponent<Seafood_Delight>();
        bool popped = seafood != null && seafood.exploded;
        if (seafood != null)
            seafood.exploded = true;
        try
        {
            food.CookedMeter = meter;
        }
        finally
        {
            if (seafood != null)
                seafood.exploded = popped;
        }
    }

    private static void ApplySpoiling(Food food, float spoil, bool spoiled)
    {
        if (spoiled)
        {
            if (food.spoiled || food.swapOnSpoil != null)
                return; // Or the spoiled version comes as a spawn.
            food.spoiledMeter = 360f;
            food.Spoil();
        }
        else if (!food.spoiled && Mathf.Abs(food.spoiledMeter - spoil) >= SpoilStep)
        {
            food.spoiledMeter = spoil;
        }
    }
}
