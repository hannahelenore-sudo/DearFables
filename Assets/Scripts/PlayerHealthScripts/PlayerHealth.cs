using System;
using UnityEngine;

/// <summary>
/// The player's health cards. The only way to lose one is to lose the card game.
/// Static, survives scene loads, wiped when the game closes.
/// </summary>
public static class PlayerHealth
{
    public const int DefaultMax = 4;

    private static int max = DefaultMax;
    private static int current = DefaultMax;
    private static int displayed = DefaultMax;

    public static event Action Changed;
    /// <summary>Fires once when health reaches 0.</summary>
    public static event Action Died;

    public static int Max { get { return max; } }
    public static int Current { get { return current; } }
    public static bool IsDead { get { return current <= 0; } }

    /// <summary>What the health HUD last showed. Lets it play the "lose a card" animation after the scene changes.</summary>
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
        if (current == 1) MoodManager.Set(MoodManager.Angry); // one card left: the player gets angry
        if (Changed != null) Changed();
        if (current <= 0 && Died != null) Died();
    }

    public static void Heal(int amount = 1)
    {
        if (amount <= 0) return;
        current = Mathf.Min(max, current + amount);
        if (Changed != null) Changed();
    }

    /// <summary>Sets health to an exact value (debug / scripted events). Setting 1 makes the player Angry, setting 0 ends the game.</summary>
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