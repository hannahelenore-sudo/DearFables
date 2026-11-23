using System;
using UnityEngine;

public enum StructureType { None, Solar, Wind }

public enum StructureUpgrade { Power, Stability, Mult }

public static class StructureProgress
{
    public const int BuildCost = 5;
    public const int DestroyCost = 6;
    public const int MaxLevel = 3;

    public const int PowerPerLevel = 1;
    public const float LowPowerReductionPerLevel = 0.3f;
    public const float MultPerLevel = 0.25f;

    private static readonly int[] upgradeCosts = { 4, 6, 8 };

    private static StructureType current = StructureType.None;
    private static readonly int[] levels = new int[3];

    public static event Action Changed;

    public static StructureType Current { get { return current; } }
    public static bool IsBuilt { get { return current != StructureType.None; } }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnLaunch()
    {
        current = StructureType.None;
        Array.Clear(levels, 0, levels.Length);
    }

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

    public static int Level(StructureUpgrade upgrade) { return levels[(int)upgrade]; }

    public static bool IsMaxed(StructureUpgrade upgrade) { return levels[(int)upgrade] >= MaxLevel; }

    public static int NextCost(StructureUpgrade upgrade)
    {
        int lvl = levels[(int)upgrade];
        return lvl >= MaxLevel ? 0 : upgradeCosts[Mathf.Min(lvl, upgradeCosts.Length - 1)];
    }

    public static int PowerBonus(StructureType type)
    {
        return current == type ? levels[(int)StructureUpgrade.Power] * PowerPerLevel : 0;
    }

    public static float LowPowerChanceFactor(StructureType type)
    {
        if (current != type) return 1f;
        return Mathf.Max(0f, 1f - levels[(int)StructureUpgrade.Stability] * LowPowerReductionPerLevel);
    }

    public static float MultBonus(StructureType type)
    {
        return current == type ? levels[(int)StructureUpgrade.Mult] * MultPerLevel : 0f;
    }

    public static float LowPowerChanceFactorFor(CardData card)
    {
        if (card is WindCard) return LowPowerChanceFactor(StructureType.Wind);
        if (card is SolarCard) return LowPowerChanceFactor(StructureType.Solar);
        return 1f;
    }

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