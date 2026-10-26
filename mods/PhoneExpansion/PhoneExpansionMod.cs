using System.Collections.Generic;
using NeetLoader;
using NeetLoader.Config;
using NeetModKit.Events;
using NeetModKit.Features;
using NeetModKit.Menu;
using NeetModKit.Settings;
using PhoneExpansion.Calls;
using PhoneExpansion.Features;
using PhoneExpansion.Menu;
using PhoneExpansion.Packs;
using PhoneExpansion.Rules;
using PhoneExpansion.Script;

namespace PhoneExpansion;

/// <summary>
/// New numbers to dial and new calls that ring in. Calls come from the "phone" folder of any mod
/// (JSON or Ink), or from code through <see cref="PhoneBook"/>.
/// </summary>
public sealed class PhoneExpansionMod : NeetMod
{
    internal static PhoneExpansionMod Instance { get; private set; }
    internal static ConfigFile<PhoneConfig> Config { get; private set; }

    public override void OnLoad()
    {
        Instance = this;
        Config = LoadConfig<PhoneConfig>();
        Config.Save();
        ModSettings.Register(this, Config, "Phone Expansion");

        PhoneMemory.Init(this);
        BuiltinConditions.Register(this);
        BuiltinActions.Register(this);
        PhoneInkFunctions.Register(this);
        PackLoader.Init(this);
        PhoneCommands.Register(this);

        ModFeatures.Register(this, new RingingCallsFeature());
        ModFeatures.Register(this, new CallerIdFeature());
        ModMenu.AddPage(this, new PhonePage());

        PhoneBook.Changed += SyncNumbers;
        GameEvents.WhenHouseReady(this, SyncNumbers);
        GameEvents.HouseReady.Subscribe(this, IncomingCalls.OnHouseReady);
        GameEvents.BeforeSave.Subscribe(this, CallRouter.HangUpActive);
    }

    // After every mod has loaded, so their phone folders are there too.
    public override void OnAllLoaded() => PackLoader.Reload();

    public override void OnUpdate()
    {
        PackLoader.Tick();
        IncomingCalls.Tick();
    }

    public override void OnUnload()
    {
        PhoneBook.Changed -= SyncNumbers;
        CallRouter.HangUpActive();
        CallerScreen.ClearAll();
        PhoneLine.SetNumbersEverywhere(new string[0]);
        Instance = null;
    }

    /// <summary>Numbers of all modded calls (the phones need them to connect instead of giving a busy tone).</summary>
    internal static List<string> ModdedNumbers()
    {
        var numbers = new List<string>();
        foreach (var call in PhoneBook.All)
        {
            if (call.Number != null && !numbers.Contains(call.Number))
                numbers.Add(call.Number);
        }
        return numbers;
    }

    private static void SyncNumbers()
    {
        if (GameEvents.IsHouseReady)
            PhoneLine.SetNumbersEverywhere(ModdedNumbers());
    }
}
