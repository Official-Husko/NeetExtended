using System;
using NeetLoader;
using NeetLoader.Config;
using NeetModKit.Events;
using NeetModKit.Features;
using NeetModKit.Menu;
using NeetModKit.Settings;
using Randomizer.Features;
using Randomizer.Menu;
using Randomizer.Parts;
using Randomizer.Seeds;

namespace Randomizer;

/// <summary>
/// A seed per save shuffles the night visitors, store prices, deliveries, the house on a new game
/// and how fast needs drain. Every part is a feature on the Mods page.
/// </summary>
public sealed class RandomizerMod : NeetMod
{
    internal static RandomizerMod Instance { get; private set; }
    internal static ConfigFile<RandomizerConfig> Config { get; private set; }

    public override void OnLoad()
    {
        Instance = this;
        Config = LoadConfig<RandomizerConfig>();
        Config.Save();
        Config.Changed += _ => ApplyAll();
        ModSettings.Register(this, Config, "Randomizer");

        SeedState.Init(this);
        GameEvents.WhenHouseReady(this, () =>
        {
            ApplyAll();
            HouseShuffle.ApplyOnce();
        });

        ModFeatures.Register(this, new ShuffledVisitorsFeature());
        ModFeatures.Register(this, new RandomPricesFeature());
        ModFeatures.Register(this, new MysteryDeliveriesFeature());
        ModFeatures.Register(this, new ShuffledHouseFeature());
        ModFeatures.Register(this, new RandomNeedsFeature());
        ModMenu.AddPage(this, new RandomizerPage());
    }

    public override void OnUnload()
    {
        Prices.Restore();
        Needs.Restore();
        Visitors.Enabled = false;
        Visitors.Apply();
        Instance = null;
    }

    /// <summary>Applies everything that follows the seed (the house only changes on a new game).</summary>
    internal static void ApplyAll()
    {
        if (!GameEvents.IsHouseReady)
            return;
        Visitors.Apply();
        Prices.Apply();
        Needs.Apply();
    }

    /// <summary>A new seed for this save.</summary>
    internal static void Reroll()
    {
        SeedState.Current.Seed = Environment.TickCount ^ Guid.NewGuid().GetHashCode();
        Visitors.Reroll();
        ApplyAll();
    }
}
