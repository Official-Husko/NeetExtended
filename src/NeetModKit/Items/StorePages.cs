using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace NeetModKit.Items;

/// <summary>
/// The computer's store pages. Their buttons are built into the scene with the price written in
/// their label, and the game maps button i to item i of the store's PickupBank list. A mod that
/// changes prices (<c>Usable.price</c>) calls <see cref="RefreshPriceLabels"/> so the labels match;
/// the confirmation text and the charge read the live price anyway.
/// </summary>
public static class StorePages
{
    /// <summary>Rewrites the price line of every store button from its item's current price.</summary>
    public static void RefreshPriceLabels()
    {
        foreach (var store in Object.FindObjectsOfType<StoreGenerator>(true))
        {
            var pickups = store.pickupBank != null ? store.pickupBank.pickups : null;
            if (pickups == null)
                continue;
            int index = 0;
            foreach (var button in store.GetComponentsInChildren<Button>(true))
            {
                if (!button.gameObject.activeSelf)
                    continue;
                if (index >= pickups.Count)
                    break;
                var usable = pickups[index].obj != null ? pickups[index].obj.GetComponentInChildren<Usable>() : null;
                index++;
                var label = button.transform.Find("Desc_Price")?.GetComponent<Text>();
                if (usable != null && label != null)
                    label.text = WithPrice(label.text, usable.price);
            }
        }
    }

    /// <summary>"Name\nPrice$" with the price replaced.</summary>
    public static string WithPrice(string label, float price)
    {
        string text = price.ToString("0.##", CultureInfo.InvariantCulture) + "$";
        int line = label?.LastIndexOf('\n') ?? -1;
        return line < 0 ? text : label.Substring(0, line + 1) + text;
    }
}
