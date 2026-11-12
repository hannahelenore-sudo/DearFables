using UnityEngine;

/// <summary>
/// Stamina shared between the player and the HUD. 0 = empty, 1 = full.
/// PlayerMovement spends and refills it; StaminaBarHUD only reads it.
/// </summary>
public static class PlayerStamina
{
    public static float Value { get; private set; } = 1f;

    /// <summary>True after hitting zero, until it refills past the recover threshold.</summary>
    public static bool Exhausted { get; private set; }

    public static void Spend(float amount)
    {
        Value = Mathf.Clamp01(Value - amount);
        if (Value <= 0f) Exhausted = true;
    }

    public static void Regen(float amount, float recoverAt)
    {
        Value = Mathf.Clamp01(Value + amount);
        if (Exhausted && Value >= recoverAt) Exhausted = false;
    }

    public static void Refill()
    {
        Value = 1f;
        Exhausted = false;
    }

    /// <summary>Debug: jump straight to a value.</summary>
    public static void Set(float value)
    {
        Value = Mathf.Clamp01(value);
        Exhausted = Value <= 0f;
    }

    // Keeps the value from leaking between Play sessions when Domain Reload is turned off
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Value = 1f;
        Exhausted = false;
    }
}
