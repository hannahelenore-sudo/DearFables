using UnityEngine;

public static class AdvantageHit
{
    public const float DefaultReduction = 0.10f;

    private static bool pending;
    private static float pendingReduction = DefaultReduction;

    public static bool IsActive { get; private set; }

    public static float Reduction { get; private set; } = DefaultReduction;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnLaunch()
    {
        pending = false;
        pendingReduction = DefaultReduction;
        IsActive = false;
        Reduction = DefaultReduction;
    }

    public static void Arm(float reduction = DefaultReduction)
    {
        pending = true;
        pendingReduction = Mathf.Clamp(reduction, 0f, 0.9f);
    }

    public static void Disarm()
    {
        pending = false;
    }

    public static bool BeginBattle()
    {
        IsActive = pending;
        Reduction = pendingReduction;
        pending = false;
        return IsActive;
    }

    public static int Apply(int score)
    {
        return Mathf.Max(1, Mathf.RoundToInt(score * (1f - Reduction)));
    }

    public static void EndBattle()
    {
        IsActive = false;
        pending = false;
    }
}