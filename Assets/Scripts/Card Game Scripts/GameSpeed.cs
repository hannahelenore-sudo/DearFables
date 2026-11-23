using UnityEngine;

public static class GameSpeed
{
    public static float Multiplier = 1f;

    public static bool Paused;

    public static bool IsFast { get { return Multiplier > 1.01f; } }

    public static float DeltaTime { get { return Paused ? 0f : Time.unscaledDeltaTime * Multiplier; } }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Multiplier = 1f;
        Paused = false;
    }
}

public class GameSpeedWait : CustomYieldInstruction
{
    private float remaining;

    public GameSpeedWait(float seconds)
    {
        remaining = seconds;
    }

    public override bool keepWaiting
    {
        get
        {
            remaining -= GameSpeed.DeltaTime;
            return remaining > 0f;
        }
    }
}