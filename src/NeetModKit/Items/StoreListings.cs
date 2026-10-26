using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace NeetModKit.Items;

/// <summary>
/// Puts modded items on the computer's store pages. Each store page is a grid of pre-built buttons
/// mapped by index into the store's PickupBank list, so a listing adds the item to that list and
/// clones a button next to the others. Buttons are added after the store's own Start ran, so the
/// game's index-based wiring never touches them.
/// </summary>
internal static class StoreListings
{
    private const string ButtonPrefix = "StoreItem_";

    private sealed class Listing
    {
        public string Name;
        public string BasedOn;
        public Spawnable Spawnable;
        public StoreGenerator Store;
        public GameObject Button;
    }

    private static readonly Dictionary<string, Listing> Listings = new Dictionary<string, Listing>();
    private static readonly HashSet<StoreGenerator> Started = new HashSet<StoreGenerator>();
    private const string StartedKey = "NeetModKit.StartedStores";

    /// <summary>Unity has no "Start already ran" flag, so remember it across framework reloads.</summary>
    public static void KeepForReload()
    {
        var ids = new List<int>();
        foreach (var store in Started)
        {
            if (store != null)
                ids.Add(store.GetInstanceID());
        }
        NeetLoader.Runtime.ReloadState.Put(StartedKey, ids);
    }

    public static void RestoreAfterReload()
    {
        if (!NeetLoader.Runtime.ReloadState.Take(StartedKey, out List<int> ids))
            return;
        foreach (var store in Object.FindObjectsOfType<StoreGenerator>(true))
        {
            if (ids.Contains(store.GetInstanceID()))
                Started.Add(store);
        }
    }

    public static void Add(ItemDefinition def, GameObject template)
    {
        var usable = template.GetComponent<Usable>();
        if (usable == null || usable.purchaseableAt == Usable.StoreType.Nowhere)
            return;

        // Same store as before (house wasn't reloaded): nothing to do.
        if (Listings.TryGetValue(def.Name, out var existing) && existing.Store != null)
            return;

        var store = FindStore(usable.purchaseableAt);
        if (store == null)
            return;

        var listing = new Listing { Name = def.Name, BasedOn = def.BasedOn, Spawnable = new Spawnable(template), Store = store };
        Listings[def.Name] = listing;

        // The list is a ScriptableObject that outlives scene loads, so only add once.
        var pickups = store.pickupBank.pickups;
        if (!pickups.Contains(listing.Spawnable))
            pickups.Add(listing.Spawnable);

        if (Started.Contains(store))
            CreateButton(listing);
    }

    public static void Remove(string name)
    {
        if (!Listings.TryGetValue(name, out var listing))
            return;
        Listings.Remove(name);
        if (listing.Store != null)
            listing.Store.pickupBank.pickups.Remove(listing.Spawnable);
        if (listing.Button != null)
            Object.Destroy(listing.Button);
    }

    /// <summary>The store page ran its own Start; now it's safe to add our buttons.</summary>
    public static void OnStoreStarted(StoreGenerator store)
    {
        Started.RemoveWhere(s => s == null);
        Started.Add(store);
        bool added = false;
        foreach (var listing in Listings.Values)
        {
            if (listing.Store == store && listing.Button == null)
            {
                CreateButton(listing);
                added = true;
            }
        }

        // OnEnable already sized the page before Start ran, so size it again for the new rows.
        if (added)
            GrowPage(store);
    }

    /// <summary>The page height is hard-coded by the game; grow it by the rows our buttons need.</summary>
    public static void GrowPage(StoreGenerator store)
    {
        int added = 0;
        foreach (var listing in Listings.Values)
        {
            if (listing.Store == store && listing.Button != null)
                added++;
        }
        if (added == 0)
            return;

        var grid = store.GetComponent<GridLayoutGroup>();
        var rect = (RectTransform)store.transform;
        if (grid == null)
            return;

        float cell = grid.cellSize.x + grid.spacing.x;
        int columns = Mathf.Max(1, Mathf.FloorToInt((rect.rect.width - grid.padding.horizontal + grid.spacing.x) / cell));
        int total = store.transform.childCount;
        int extraRows = Mathf.CeilToInt(total / (float)columns) - Mathf.CeilToInt((total - added) / (float)columns);
        float extra = extraRows * (grid.cellSize.y + grid.spacing.y);

        var page = store.transform.parent.GetComponent<RectTransform>();
        var content = page.parent.GetComponent<RectTransform>();
        page.sizeDelta += new Vector2(0f, extra);
        content.sizeDelta += new Vector2(0f, extra);
    }

    private static void CreateButton(Listing listing)
    {
        var store = listing.Store;
        var parent = store.transform;
        var source = parent.Find(ButtonPrefix + listing.BasedOn) ?? (parent.childCount > 0 ? parent.GetChild(0) : null);
        if (source == null)
            return;

        var button = Object.Instantiate(source.gameObject, parent);
        button.name = ButtonPrefix + listing.Name;

        var usable = listing.Spawnable.obj.GetComponent<Usable>();
        var label = button.transform.Find("Desc_Price")?.GetComponent<Text>();
        if (label != null)
            label.text = listing.Name + "\n" + usable.price.ToString("0.##", CultureInfo.InvariantCulture) + "$";

        var picture = button.transform.Find("Picture")?.GetComponent<Image>();
        if (picture != null && usable.storePicture != null)
            picture.sprite = usable.storePicture;

        // Look the index up at click time: other modded items may have been removed since.
        var spawnable = listing.Spawnable;
        var uiButton = button.GetComponent<Button>();
        uiButton.onClick.AddListener(() => store.CacheItemToOrder(store.pickupBank.pickups.IndexOf(spawnable)));
        store.GetComponentInParent<Computer>(true)?.RegisterButtonClickAudio(uiButton);

        listing.Button = button;
    }

    private static StoreGenerator FindStore(Usable.StoreType type)
    {
        foreach (var store in Object.FindObjectsOfType<StoreGenerator>(true))
        {
            if (store.storeType == type)
                return store;
        }
        return null;
    }
}
