using System;
using UnityEngine;

public static class BatteryManager
{
    public const int StartingBatteries = 4;
    public const int FightBaseReward = 3;
    public const int RewardPerHandLeft = 1;
    public const int RerollBaseCost = 5;
    public const int RerollCostIncrease = 1;
    public const float SellRate = 0.5f;
    public const int MaxDifficultyBonus = 4;

    private static int count = StartingBatteries;
    private static int pendingDrop;
    private static Vector3 lastPlayerPosition;
    private static bool hasLastPlayerPosition;

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

    public static bool TrySpend(int cost)
    {
        if (cost <= 0) return true;
        if (count < cost) return false;

        count -= cost;
        if (Changed != null) Changed(-cost);
        return true;
    }

    public static int CalculateFightReward(int handsLeft)
    {
        return FightBaseReward + RewardPerHandLeft * Mathf.Max(0, handsLeft);
    }

    public static int CalculateFightReward(int handsLeft, float difficulty)
    {
        int bonus = Mathf.RoundToInt(Mathf.Clamp01(difficulty) * MaxDifficultyBonus);
        return CalculateFightReward(handsLeft) + bonus;
    }

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