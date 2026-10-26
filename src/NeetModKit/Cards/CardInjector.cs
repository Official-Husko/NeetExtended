using System.Collections.Generic;
using NeetModKit.Assets;
using UnityEngine;

namespace NeetModKit.Cards;

/// <summary>
/// Clones card objects for the registered extra cards just before the card game collects its
/// cards for the first time.
/// </summary>
internal static class CardInjector
{
    private const string ModdedPrefix = "AnimeCard_Mod_";

    /// <summary>Adds the extra cards once. Safe to call more than once (load and start both do).</summary>
    public static void Inject(AnimeCardGenerator generator)
    {
        if (CardGame.ExtraCards.Count == 0)
            return;

        var existing = generator.GetComponentsInChildren<AnimeCard>(true);
        if (existing.Length == 0 || existing[existing.Length - 1].name.StartsWith(ModdedPrefix))
            return;

        // Cards live inside a layout group; clone the last built-in card next to it.
        var template = existing[existing.Length - 1];
        var parent = template.transform.parent;
        int index = existing.Length;

        foreach (var def in CardGame.ExtraCards)
        {
            var go = Object.Instantiate(template.gameObject, parent);
            go.name = ModdedPrefix + def.Name;
            Configure(go.GetComponent<AnimeCard>(), def, index);
            SetName(generator.animeNames, index, def.Name);
            index++;
        }
    }

    private static void Configure(AnimeCard card, CardDefinition def, int index)
    {
        card.cardId = index;
        card.medalLvl = 0;
        card.exp = 0f;
        card.expToLevel = def.ExpToLevel ?? CardGame.DefaultExpToLevel(index);
        card.income = def.Income ?? CardGame.DefaultIncome(index);
        card.purchasePrice = def.Price ?? CardGame.DefaultPrice(index);

        // Awake copies these into the card's base values the first time it runs.
        card.firstLoad = true;

        var art = def.Art;
        if (art == null && def.ArtProvider != null)
        {
            try { art = def.ArtProvider(); }
            catch (System.Exception e) { KitMod.Instance.Log.Error($"Art for card '{def.Name}' failed", e); }
        }
        if (art != null && card.picture != null)
            card.picture.sprite = ModAssets.ToSprite(KitMod.Instance, art);
    }

    /// <summary>The game names cards from a list by index; make sure our slot holds our name.</summary>
    private static void SetName(List<string> names, int index, string name)
    {
        while (names.Count <= index)
            names.Add("");
        names[index] = name;
    }
}
