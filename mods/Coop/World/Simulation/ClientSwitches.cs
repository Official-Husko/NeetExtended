using HarmonyLib;
using Coop.World.Npcs;
using Coop.World.Objects;
using UnityEngine;

namespace Coop.World.Simulation;

/// <summary>
/// Client: switches off the parts of the house that act on their own (spawning visitors, rats,
/// thieves, dust and coins, tax bills, stock prices, spam, prank calls), so they only happen on the
/// host and reach clients as spawns. Coroutines that can restart themselves are also gated by
/// <see cref="Patches.ClientGatePatches"/>. Nothing is switched back: leaving a session loads the
/// main menu and the switched house goes with it. Safe to run again (after a hot reload).
/// </summary>
internal static class ClientSwitches
{
    private static readonly AccessTools.FieldRef<EmailHandler, Coroutine> SpamRoutine =
        AccessTools.FieldRefAccess<EmailHandler, Coroutine>("sendRandomCoroutine");

    public static void Apply()
    {
        var house = HouseManager._;
        if (house != null)
        {
            house.performStatChecks = false;
            house.noWaterDrying = true;
            Off(house.looseChangeSpawner);
            Off(house.dustSpawner);
            if (house.spawners != null)
            {
                foreach (var spawner in house.spawners)
                    Off(spawner);
            }
        }
        foreach (var spawner in Object.FindObjectsOfType<Spawner>(true))
        {
            if (spawner.name.Contains("Grandpa"))
                Off(spawner);
        }
        foreach (var thief in Object.FindObjectsOfType<ThiefHandler>(true))
            thief.thiefCountdownActive = false;
        if (EncounterSpawner._ != null)
            EncounterSpawner._.enabled = false;
        Disable<TimerSequence>();
        Disable<Mouse_Hole>();
        Disable<CopVan>();
        Disable<PickupPackageArea>();
        Disable<TaxHandler>();
        if (StockMarket._ != null)
            StockMarket._.StopAllCoroutines();
        StopSpam();
        foreach (var dispatch in Object.FindObjectsOfType<Phone_Dispatch>(true))
            dispatch.StopAllCoroutines();

        PuppetizeNpcs();
    }

    private static void PuppetizeNpcs()
    {
        foreach (var o in NetObjects.All)
        {
            if (o.Brain != null)
                NpcPuppets.Puppetize(o);
        }
    }

    private static void Off(Spawner spawner)
    {
        if (spawner == null)
            return;
        spawner.enabled = false;
        spawner.externalCallsOnly = true;
    }

    private static void Disable<T>() where T : Behaviour
    {
        foreach (var behaviour in Object.FindObjectsOfType<T>(true))
            behaviour.enabled = false;
    }

    private static void StopSpam()
    {
        var email = EmailHandler._;
        if (email == null)
            return;
        var routine = SpamRoutine(email);
        if (routine != null)
            email.StopCoroutine(routine);
        SpamRoutine(email) = null;
    }

    /// <summary>For the debug report: anything that should be off here but isn't.</summary>
    public static string Leaks()
    {
        var text = new System.Text.StringBuilder();
        var house = HouseManager._;
        if (house != null && house.performStatChecks)
            text.Append("HouseManager.performStatChecks; ");
        if (EncounterSpawner._ != null && EncounterSpawner._.enabled)
            text.Append("EncounterSpawner; ");
        foreach (var thief in Object.FindObjectsOfType<ThiefHandler>(true))
        {
            if (thief.thiefCountdownActive)
                text.Append("ThiefHandler; ");
        }
        Count<TimerSequence>(text);
        Count<Mouse_Hole>(text);
        Count<CopVan>(text);
        Count<PickupPackageArea>(text);
        Count<TaxHandler>(text);
        foreach (var o in NetObjects.All)
        {
            if (o.Brain != null && o.Brain.enabled)
                text.Append("brain ").Append(o).Append("; ");
        }
        return text.Length == 0 ? "clean" : text.ToString();
    }

    private static void Count<T>(System.Text.StringBuilder text) where T : Behaviour
    {
        foreach (var behaviour in Object.FindObjectsOfType<T>(true))
        {
            if (behaviour.enabled)
                text.Append(typeof(T).Name).Append("; ");
        }
    }
}
