using System;
using UnityEngine;

public enum StructureType { None, Solar, Wind }

public enum StructureUpgrade { Power, Stability, Mult }

/// <summary>
/// The structure built at the Blueprint Shop. You can only own ONE: a Solar Panel or a Windmill.
/// It buffs the matching card type (Solar cards or Wind cards). To switch you must pay to destroy it
/// (which also resets its upgrades). Session only, like BoonProgress / TrinketInventory: survives scene loads,
/// wiped when the game closes.
/// All the numbers are here - tweak freely.
/// </summary>
public static class StructureProgress
{
    // ---------------- Tuning ----------------
    public const int BuildCost = 5;                       // build a Solar Panel / Windmill
    public const int DestroyCost = 6;                     // pay this to tear it down and pick again
    public const int MaxLevel = 3;                        // levels per upgrade

    public const int PowerPerLevel = 1;                   // +Power on every card of the type, per level
    public const float LowPowerReductionPerLevel = 0.3f;  // -30% of the Low Power chance, per level (3 levels = -90%)
    public const float MultPerLevel = 0.25f;              // +Mult added to the set bonus multiplier, per level

    // Cost of going from level 0->1, 1->2, 2->3 (same for all three upgrades)
    private static readonly int[] upgradeCosts = { 4, 6, 8 };

    // ---------------- State ----------------
    private static StructureType current = StructureType.None;
    private static readonly int[] levels = new int[3];

    /// <summary>Fires whenever the structure is built / upgraded / destroyed.</summary>
    public static event Action Changed;

    public static StructureType Current { get { return current; } }
    public static bool IsBuilt { get { return current != StructureType.None; } }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnLaunch()
    {
        current = StructureType.None;
        Array.Clear(levels, 0, levels.Length);
    }

    // ---------------- Changing things (the shop spends the batteries, these just apply) ----------------

    public static bool Build(StructureType type)
    {
        if (type == StructureType.None || IsBuilt) return false;
        current = type;
        Array.Clear(levels, 0, levels.Length);
        Fire();
        return true;
    }

    public static bool Upgrade(StructureUpgrade upgrade)
    {
        if (!IsBuilt || IsMaxed(upgrade)) return false;
        levels[(int)upgrade]++;
        Fire();
        return true;
    }

    public static void Destroy()
    {
        if (!IsBuilt) return;
        current = StructureType.None;
        Array.Clear(levels, 0, levels.Length);
        Fire();
    }

    private static void Fire() { if (Changed != null) Changed(); }

    // ---------------- Reading upgrades ----------------

    public static int Level(StructureUpgrade upgrade) { return levels[(int)upgrade]; }

    public static bool IsMaxed(StructureUpgrade upgrade) { return levels[(int)upgrade] >= MaxLevel; }

    /// <summary>Cost of the next level, or 0 if maxed.</summary>
    public static int NextCost(StructureUpgrade upgrade)
    {
        int lvl = levels[(int)upgrade];
        return lvl >= MaxLevel ? 0 : upgradeCosts[Mathf.Min(lvl, upgradeCosts.Length - 1)];
    }

    // ---------------- What the rest of the game reads ----------------

    /// <summary>+Power for every card of this type (0 if the structure is a different type).</summary>
    public static int PowerBonus(StructureType type)
    {
        return current == type ? levels[(int)StructureUpgrade.Power] * PowerPerLevel : 0;
    }

    /// <summary>Multiply the Low Power chance by this (1 = unchanged, 0.1 = 90% less likely).</summary>
    public static float LowPowerChanceFactor(StructureType type)
    {
        if (current != type) return 1f;
        return Mathf.Max(0f, 1f - levels[(int)StructureUpgrade.Stability] * LowPowerReductionPerLevel);
    }

    /// <summary>Extra Mult added onto the set bonus multiplier of this card type.</summary>
    public static float MultBonus(StructureType type)
    {
        return current == type ? levels[(int)StructureUpgrade.Mult] * MultPerLevel : 0f;
    }

    /// <summary>Convenience for DeckManager: the Low Power factor for whatever card was just drawn.</summary>
    public static float LowPowerChanceFactorFor(CardData card)
    {
        if (card is WindCard) return LowPowerChanceFactor(StructureType.Wind);
        if (card is SolarCard) return LowPowerChanceFactor(StructureType.Solar);
        return 1f;
    }

    // ---------------- Names ----------------

    public static string DisplayName(StructureType type)
    {
        switch (type)
        {
            case StructureType.Solar: return "Solar Panel";
            case StructureType.Wind: return "Windmill";
            default: return "Nothing";
        }
    }

    public static string CardName(StructureType type)
    {
        switch (type)
        {
            case StructureType.Solar: return "Solar";
            case StructureType.Wind: return "Wind";
            default: return "";
        }
    }

    public static string UpgradeName(StructureUpgrade upgrade)
    {
        switch (upgrade)
        {
            case StructureUpgrade.Power: return "Power Boost";
            case StructureUpgrade.Stability: return "Stable Output";
            default: return "Mult Boost";
        }
    }
}
