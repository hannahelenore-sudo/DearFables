using System;
using UnityEngine;

/// <summary>
/// Batteries: the money of the game (trinkets cost batteries). Static, survives scene loads, wiped when the game closes.
/// The numbers below follow Balatro's economy (start $4, a fight pays $3 + $1 per hand left, a reroll costs $5 and goes
/// up by $1 each time, selling gives half). Change them here.
/// </summary>
public static class BatteryManager
{
    // ---------------- Economy (Balatro style) ----------------
    public const int StartingBatteries = 4;
    public const int FightBaseReward = 3;      // Balatro: Small Blind pays $3
    public const int RewardPerHandLeft = 1;    // Balatro: +$1 for every hand you didn't need
    public const int RerollBaseCost = 5;       // Balatro: the first reroll costs $5
    public const int RerollCostIncrease = 1;   // ...and each reroll in the same shop costs $1 more
    public const float SellRate = 0.5f;        // Balatro: items sell for half of their price

    private static int count = StartingBatteries;
    private static int pendingDrop;
    private static Vector3 lastPlayerPosition;
    private static bool hasLastPlayerPosition;

    /// <summary>Fires when the amount changes. The number is how much it changed by (negative = spent).</summary>
    public static event Action<int> Changed;

    public static int Count { get { return count; } }
    public static int PendingDrop { get { return pendingDrop; } }

    public static Vector3 LastPlayerPosition { get { return lastPlayerPosition; } }
    public static bool HasLastPlayerPosition { get { return hasLastPlayerPosition; } }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnLaunch()
    {
        count = StartingBatteries;
        pendingDrop = 0;
        hasLastPlayerPosition = false;
    }

    public static bool CanAfford(int cost) { return count >= cost; }

    public static void Add(int amount)
    {
        if (amount == 0) return;
        count = Mathf.Max(0, count + amount);
        if (Changed != null) Changed(amount);
    }

    /// <summary>Takes the batteries and returns true, or returns false (and takes nothing) if you can't afford it.</summary>
    public static bool TrySpend(int cost)
    {
        if (cost <= 0) return true;
        if (count < cost) return false;

        count -= cost;
        if (Changed != null) Changed(-cost);
        return true;
    }

    /// <summary>What a won fight pays: base + a bonus for every hand you had left.</summary>
    public static int CalculateFightReward(int handsLeft)
    {
        return FightBaseReward + RewardPerHandLeft * Mathf.Max(0, handsLeft);
    }

    /// <summary>Batteries won in a fight that haven't dropped in the world yet (BatteryDropper spawns them).</summary>
    public static void QueueDrop(int amount)
    {
        pendingDrop += Mathf.Max(0, amount);
    }

    public static int TakePendingDrop()
    {
        int n = pendingDrop;
        pendingDrop = 0;
        return n;
    }

    public static void RememberPlayerPosition(Vector3 position)
    {
        lastPlayerPosition = position;
        hasLastPlayerPosition = true;
    }

    public static int SellValueFor(int price)
    {
        return price <= 0 ? 0 : Mathf.Max(1, Mathf.FloorToInt(price * SellRate));
    }
}
