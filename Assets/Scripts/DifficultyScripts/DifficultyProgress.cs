using UnityEngine;

/// <summary>
/// Difficulty meter. Static, survives scene loads, no GameObject needed.
///
/// Points = stars earned + (battles fought * pointsPerFight), capped at maxPoints.
/// Meter  = Points / maxPoints  (0 = easiest, 1 = hardest, reached at 30 points).
///
/// Debug: DifficultyProgress.SetDebugPoints(12) forces the difficulty for the rest of the
/// session. DifficultyProgress.ClearDebugOverride() goes back to real progress.
/// </summary>
public static class DifficultyProgress
{
    // ---------------- Tweak these ----------------
    public static float maxPoints = 30f;       // points (stars) needed for max difficulty
    public static float pointsPerFight = 0.5f; // every battle (win or lose) adds this much, on top of stars
    // ----------------------------------------------

    /// <summary>Battles fought this session (wins and losses).</summary>
    public static int BattlesFought { get; private set; }

    private static bool _overridden;
    private static float _debugPoints;

    /// <summary>True while the debug menu is forcing the difficulty.</summary>
    public static bool IsOverridden => _overridden;

    /// <summary>Current difficulty points, 0 to maxPoints.</summary>
    public static float Points
    {
        get
        {
            if (_overridden) return Mathf.Clamp(_debugPoints, 0f, maxPoints);

            float real = PlayerProgress.TotalStars + BattlesFought * pointsPerFight;
            return Mathf.Clamp(real, 0f, maxPoints);
        }
    }

    /// <summary>Difficulty from 0 (easiest) to 1 (hardest).</summary>
    public static float Meter => maxPoints <= 0f ? 1f : Mathf.Clamp01(Points / maxPoints);

    /// <summary>Call after every battle ends (BattleManager does this).</summary>
    public static void AddBattle()
    {
        BattlesFought++;
    }

    /// <summary>Debug: force the difficulty to this many points (0 to maxPoints).</summary>
    public static void SetDebugPoints(float points)
    {
        _overridden = true;
        _debugPoints = Mathf.Clamp(points, 0f, maxPoints);
    }

    /// <summary>Debug: stop forcing the difficulty and use real progress again.</summary>
    public static void ClearDebugOverride()
    {
        _overridden = false;
    }

    /// <summary>Wipe fights and the debug override (stars are reset in PlayerProgress).</summary>
    public static void ResetAll()
    {
        BattlesFought = 0;
        _overridden = false;
    }

    /// <summary>
    /// Scales a score by the current difficulty: baseScore at meter 0, baseScore * maxMultiplier at meter 1.
    /// Rounded to the nearest 10.
    /// </summary>
    public static int ScaleScore(int baseScore, float maxMultiplier)
    {
        float scaled = Mathf.Lerp(baseScore, baseScore * maxMultiplier, Meter);
        return Mathf.Max(10, Mathf.RoundToInt(scaled / 10f) * 10);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        BattlesFought = 0;
        _overridden = false;
        _debugPoints = 0f;
    }
}
