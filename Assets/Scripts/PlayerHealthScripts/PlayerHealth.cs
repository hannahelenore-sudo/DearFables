using System;
using UnityEngine;

public static class PlayerHealth
{
    public const int DefaultMax = 4;

    private static int max = DefaultMax;
    private static int current = DefaultMax;
    private static int displayed = DefaultMax;

    public static event Action Changed;
    public static event Action Died;

    public static int Max { get { return max; } }
    public static int Current { get { return current; } }
    public static bool IsDead { get { return current <= 0; } }

    public static int DisplayedHealth
    {
        get { return Mathf.Clamp(displayed, 0, max); }
        set { displayed = Mathf.Clamp(value, 0, max); }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnLaunch()
    {
        ResetAll();
    }

    public static void ResetAll()
    {
        max = DefaultMax;
        current = DefaultMax;
        displayed = DefaultMax;
        if (Changed != null) Changed();
    }

    public static void Lose(int amount = 1)
    {
        if (amount <= 0 || current <= 0) return;

        current = Mathf.Max(0, current - amount);
        if (current == 1) MoodManager.Set(MoodManager.Angry);
        if (Changed != null) Changed();
        if (current <= 0 && Died != null) Died();
    }

    public static void Heal(int amount = 1)
    {
        if (amount <= 0) return;
        current = Mathf.Min(max, current + amount);
        if (Changed != null) Changed();
    }

    public static void SetCurrent(int value)
    {
        int before = current;
        current = Mathf.Clamp(value, 0, max);
        if (current == 1) MoodManager.Set(MoodManager.Angry);
        if (Changed != null) Changed();
        if (current <= 0 && before > 0 && Died != null) Died();
    }

    public static void SetMax(int newMax, bool fill = true)
    {
        max = Mathf.Max(1, newMax);
        current = fill ? max : Mathf.Min(current, max);
        displayed = Mathf.Clamp(displayed, 0, max);
        if (Changed != null) Changed();
    }
}