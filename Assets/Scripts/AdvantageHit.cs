using UnityEngine;

/// <summary>
/// "Advantage Hit": hitting an enemy with a thrown card before the fight makes the score to beat smaller.
/// EnemyChase arms it, BattleManager uses it when the card game scene starts, and it lasts for that one fight.
/// Static, so it survives the scene load into the card game.
/// </summary>
public static class AdvantageHit
{
    public const float DefaultReduction = 0.10f; // 10% less score to beat

    private static bool pending;
    private static float pendingReduction = DefaultReduction;

    /// <summary>True during the fight that started with a card hit.</summary>
    public static bool IsActive { get; private set; }

    /// <summary>How much of the score to beat is removed in the current fight (0.1 = 10%).</summary>
    public static float Reduction { get; private set; } = DefaultReduction;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnLaunch()
    {
        pending = false;
        pendingReduction = DefaultReduction;
        IsActive = false;
        Reduction = DefaultReduction;
    }

    /// <summary>Call right before the battle starts, when the player hit the enemy with a card.</summary>
    public static void Arm(float reduction = DefaultReduction)
    {
        pending = true;
        pendingReduction = Mathf.Clamp(reduction, 0f, 0.9f);
    }

    public static void Disarm()
    {
        pending = false;
    }

    /// <summary>Called by BattleManager when the card game starts. Returns true if this fight has the advantage.</summary>
    public static bool BeginBattle()
    {
        IsActive = pending;
        Reduction = pendingReduction;
        pending = false;
        return IsActive;
    }

    /// <summary>The score to beat after the reduction (never below 1).</summary>
    public static int Apply(int score)
    {
        return Mathf.Max(1, Mathf.RoundToInt(score * (1f - Reduction)));
    }

    /// <summary>Called when the card game scene is left.</summary>
    public static void EndBattle()
    {
        IsActive = false;
        pending = false;
    }
}
