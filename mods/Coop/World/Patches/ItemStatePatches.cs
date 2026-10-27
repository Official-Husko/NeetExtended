using Coop.World.Objects;
using Coop.World.State;
using HarmonyLib;
using UnityEngine;

namespace Coop.World.Patches;

/// <summary>
/// What an item is like after it appeared (cooked, spoiled, filled, soaked, burnt, how much is left).
/// The game that simulates the item changes it; these patches keep every other game from changing
/// its copy on its own (only the state that arrives may), and mark the item the moment it changes so
/// the new state goes out at the end of the frame, from whichever game made the change.
/// </summary>
[HarmonyPatchCategory(WorldPatches.World)]
[HarmonyPatch]
internal static class ItemStatePatches
{
    // Flames and microwaves touching a copy that is only shown here don't cook it: the cooking
    // happens where the food is simulated (and cooking it through can swap it for another object).
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Food), nameof(Food.CookedMeter), MethodType.Setter)]
    private static bool Cooking(Food __instance) => MayChange(__instance.gameObject);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Food), nameof(Food.Spoil))]
    private static bool Spoiling(Food __instance) => MayChange(__instance.gameObject);

    // A wet floor soaks what rests on it, in the game that simulates the thing.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Soaker), "OnCollisionStay")]
    private static bool FloorSoaks(Soaker __instance) => OwnerGatePatches.Simulates(__instance.gameObject);

    // Turds and water dirty and clean clothes where the piece is simulated.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Wearable), "OnCollisionEnter")]
    private static bool ClothesHit(Wearable __instance) => OwnerGatePatches.Simulates(__instance.gameObject);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Wearable), "OnTriggerEnter")]
    private static bool ClothesWashed(Wearable __instance) => OwnerGatePatches.Simulates(__instance.gameObject);

    // A running vacuum swallows the dust balls it meets where it is simulated.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(VacuumCleaner), "OnTriggerEnter")]
    private static bool VacuumSucks(VacuumCleaner __instance) => OwnerGatePatches.Simulates(__instance.gameObject);

    // Hints: the state changed just now.

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Usable), nameof(Usable.Damage))]
    private static void Damaged(Usable __instance) => StateSync.MarkDirty(__instance);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Usable), nameof(Usable.DamageRaw))]
    private static void DamagedRaw(Usable __instance) => StateSync.MarkDirty(__instance);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Usable), nameof(Usable.Burn))]
    private static void Burnt(Usable __instance) => StateSync.MarkDirty(__instance);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(LiquidContainer), nameof(LiquidContainer.Fill))]
    private static void Filled(LiquidContainer __instance) => StateSync.MarkDirty(__instance);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(LiquidContainer), nameof(LiquidContainer.Empty), typeof(Collision), typeof(bool))]
    private static void Emptied(LiquidContainer __instance) => StateSync.MarkDirty(__instance);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(LiquidContainer), nameof(LiquidContainer.Drink))]
    private static void Sipped(LiquidContainer __instance) => StateSync.MarkDirty(__instance);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Soaker), nameof(Soaker.Soak))]
    private static void Soaked(Soaker __instance) => StateSync.MarkDirty(__instance);

    // A coin or a gib can land in a bank or bowl that another player simulates.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Wearable), nameof(Wearable.MakeDirty))]
    private static void Dirtied(Wearable __instance) => StateSync.MarkDirty(__instance);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Wearable), nameof(Wearable.Clean))]
    private static void Cleaned(Wearable __instance) => StateSync.MarkDirty(__instance);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Piggybank), nameof(Piggybank.CashValue), MethodType.Setter)]
    private static void PiggyChanged(Piggybank __instance) => StateSync.MarkDirty(__instance);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CatFoodBowl), nameof(CatFoodBowl.FoodUnits), MethodType.Setter)]
    private static void BowlChanged(CatFoodBowl __instance) => StateSync.MarkDirty(__instance);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PrefabContainer), nameof(PrefabContainer.TakeOne))]
    private static void Dispensed(PrefabContainer __instance) => StateSync.MarkDirty(__instance);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Toilet_Paper_Roll), nameof(Toilet_Paper_Roll.TakeOne))]
    private static void SheetTaken(Toilet_Paper_Roll __instance) => StateSync.MarkDirty(__instance);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CerealBox), nameof(CerealBox.Open))]
    private static void CerealOpened(CerealBox __instance) => StateSync.MarkDirty(__instance);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Seafood_Delight), nameof(Seafood_Delight.Use))]
    private static void TinOpened(Seafood_Delight __instance) => StateSync.MarkDirty(__instance);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Seafood_Delight), nameof(Seafood_Delight.Explode))]
    private static void TinPopped(Seafood_Delight __instance) => StateSync.MarkDirty(__instance);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(VacuumCleaner), nameof(VacuumCleaner.SetUse), typeof(bool))]
    private static void VacuumSwitched(VacuumCleaner __instance) => StateSync.MarkDirty(__instance);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(VacuumCleaner), nameof(VacuumCleaner.Fuse), MethodType.Setter)]
    private static void VacuumBattery(VacuumCleaner __instance) => StateSync.MarkDirty(__instance);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Gun), nameof(Gun.InsertMag))]
    private static void MagIn(Gun __instance) => StateSync.MarkDirty(__instance);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Gun), nameof(Gun.EjectMag))]
    private static void MagOut(Gun __instance) => StateSync.MarkDirty(__instance);

    /// <summary>The game that simulates the item, or any game while a state is being applied.</summary>
    private static bool MayChange(GameObject go)
    {
        if (!WorldSide.Running || StateSync.Applying)
            return true;
        var o = NetObjects.Find(go);
        return o == null || !o.IsReplica;
    }
}
