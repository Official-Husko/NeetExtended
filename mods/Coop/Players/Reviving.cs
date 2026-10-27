using Coop.Session;
using UnityEngine;

namespace Coop.Players;

/// <summary>
/// The ways to bring a downed player back, from any game and for any player (<see cref="Downed.Revive"/>
/// carries it to their game):
/// - food: use it while looking at them (or carrying them) and they eat it instead of you; healing
///   pills (any food with a health boost) bring them back stronger, other pills (knockout, laxative,
///   hormones) aren't food for this and stay yours;
/// - a drink: use a cup with water or blood in it the same way, and they take a sip;
/// - bare hands: with an empty hand, hold that hand's button on them for <see cref="HoldSeconds"/>
///   (a tap picks them up instead, see <see cref="Carrying"/>). Looking away or letting go stops it.
/// Anything else in a cup (dirt, cleaner) or any other item is used on yourself as always.
/// </summary>
internal static class Reviving
{
    public const float HoldSeconds = 3f;
    // A press shorter than this is a tap: carry them.
    private const float TapSeconds = 0.35f;

    // How well each way works: the health they stand up with.
    private const float FoodHealth = Downed.ReviveHealth;
    private const float MedicineHealth = 60f;
    private const float WaterHealth = 20f;
    private const float BloodHealth = 35f;
    private const float HandsHealth = 15f;

    private const string PillPrefix = "Pill";
    private const byte None = byte.MaxValue;

    // The hand button being held on a downed player (-1: none).
    private static int _hand = -1;
    private static byte _slot = None;
    private static float _since;

    public static void Init()
    {
        CoopLoop.Update += Tick;
        CoopSession.Ended += _ => Stop();
        CoopLoop.Unloading += Stop;
    }

    /// <summary>Food used by the local player: true when it went to a downed player instead.</summary>
    public static bool TryFeed(Food food)
    {
        if (!IsHeldByLocal(food.gameObject))
            return false;
        bool medicine = Heals(food);
        if (!medicine && food.name.StartsWith(PillPrefix))
            return false;
        byte slot = DownedAim.Patient();
        if (slot == None)
            return false;
        Object.Destroy(food.gameObject);
        Downed.Revive(slot, medicine ? MedicineHealth : FoodHealth);
        return true;
    }

    // A status effect on this food that raises health.
    private static bool Heals(Food food) => Heals(food.rawStatusEffects) || Heals(food.cookedStatusEffects);

    private static bool Heals(System.Collections.Generic.List<StatusEffect> effects)
    {
        if (effects == null)
            return false;
        foreach (var effect in effects)
        {
            if (effect == null || effect.statType == null || effect.effectSize <= 0f)
                continue;
            foreach (var stat in effect.statType)
            {
                if (stat == StatType.Health)
                    return true;
            }
        }
        return false;
    }

    /// <summary>A cup used by the local player: true when a sip went to a downed player instead.</summary>
    public static bool TryGiveDrink(LiquidContainer cup)
    {
        if (!cup.filled || cup.currentDrinkVolume < cup.sipVolume || cup.waterVolume == null || !IsHeldByLocal(cup.gameObject))
            return false;
        var liquid = (Liquid)cup.waterVolume.paintChannel;
        if (liquid != Liquid.Water && liquid != Liquid.Blood)
            return false;
        byte slot = DownedAim.Patient();
        if (slot == None)
            return false;

        // The game's own sip, minus the drinker's gulp.
        cup.currentDrinkVolume -= cup.sipVolume;
        if (cup.currentDrinkVolume < cup.sipVolume)
            cup.Empty(null, noSplash: true);
        cup.GetComponent<Usable>().PreventOtherUse();
        Downed.Revive(slot, liquid == Liquid.Blood ? BloodHealth : WaterHealth);
        return true;
    }

    private static bool IsHeldByLocal(GameObject item) =>
        CoopSession.InWorld && HouseManager._ != null && item.TryGetComponent(out Usable usable) && usable.player == HouseManager._.player;

    private static void Tick()
    {
        var player = CoopSession.InWorld && HouseManager._ != null ? HouseManager._.player : null;
        if (player == null || Downed.LocalDown || !player.TryGetComponent(out BrainPlayer brain) || brain.intents == null
            || !player.TryGetComponent(out Player_Abilities hands))
        {
            Stop();
            return;
        }

        if (_hand < 0)
        {
            if (Carrying.Carried != None)
                return;
            if (brain.intents.LeftHand.Started)
                Begin(0, hands);
            else if (brain.intents.RightHand.Started)
                Begin(1, hands);
            return;
        }

        var button = _hand == 0 ? brain.intents.LeftHand : brain.intents.RightHand;
        float held = Time.unscaledTime - _since;
        if (!button.isHeld)
        {
            // Let go: a tap picks them up.
            int hand = _hand;
            byte slot = _slot;
            Stop();
            if (held < TapSeconds)
                Carrying.PickUp(slot, hand);
            return;
        }
        // Still on them, with the hand still empty?
        var looked = DownedAim.Looked();
        if (hands.pickups[_hand] != null || looked == null || looked.Slot != _slot || !Downed.IsDown(_slot))
        {
            Stop();
            return;
        }
        if (held < TapSeconds)
            return;
        if (held >= HoldSeconds)
        {
            byte slot = _slot;
            Stop();
            Downed.Revive(slot, HandsHealth);
            return;
        }
        DownedHud.ShowPumping(held / HoldSeconds);
    }

    private static void Begin(int hand, Player_Abilities hands)
    {
        if (hands.pickups[hand] != null)
            return;
        var looked = DownedAim.Looked();
        if (looked == null || !Downed.IsDown(looked.Slot))
            return;
        _hand = hand;
        _slot = looked.Slot;
        _since = Time.unscaledTime;
    }

    private static void Stop()
    {
        _hand = -1;
        _slot = None;
        DownedHud.HidePumping();
    }
}
