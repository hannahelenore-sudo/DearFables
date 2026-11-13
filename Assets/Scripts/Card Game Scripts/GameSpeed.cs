using UnityEngine;

/// <summary>
/// One shared game-speed multiplier for every card-game animation and delay.
/// 1 = normal, 2 = double speed, 4 = quad speed... Set it from FastForwardButton.
/// Nothing here touches Time.timeScale, so the rest of the game, physics and audio are unaffected.
/// </summary>
public static class GameSpeed
{
    public static float Multiplier = 1f;

    public static bool IsFast { get { return Multiplier > 1.01f; } }

    /// <summary>Use instead of Time.unscaledDeltaTime / Time.deltaTime inside animations and timers.</summary>
    public static float DeltaTime { get { return Time.unscaledDeltaTime * Multiplier; } }

    // Keeps things safe when "Enter Play Mode Options > Reload Domain" is turned off
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Multiplier = 1f;
    }
}

/// <summary>
/// Drop-in replacement for WaitForSeconds / WaitForSecondsRealtime that respects GameSpeed.
/// Usage: yield return new GameSpeedWait(0.5f);
/// </summary>
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
