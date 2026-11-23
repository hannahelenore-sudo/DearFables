using UnityEngine;

public static class PlayerStamina
{
    public static float Value { get; private set; } = 1f;

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

    public static void Set(float value)
    {
        Value = Mathf.Clamp01(value);
        Exhausted = Value <= 0f;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Value = 1f;
        Exhausted = false;
    }
}