using UnityEngine;

/// <summary>
/// Difficulty, Balatro style. Static, survives scene loads, no GameObject needed.
///
/// Every enemy you defeat moves you further along BlindScores (3 blinds per Ante). The blinds are spread over
/// all the enemies in the game (the number of EnemyChase objects), so the last enemy is the last blind.
/// With exactly 12 enemies that is one blind per enemy:
///   Ante 1: 300 / 450 / 600      (Small / Big / Boss)
///   Ante 2: 800 / 1,200 / 1,600
///   Ante 3: 2,800 / 4,200 / 5,600
///   Ante 4: 6,000 / 9,000 / 12,000   (the last one for now: it stays here after that)
/// The card game tutorial fight is not part of this (BattleManager gives it its own fixed score).
///
/// A STAR enemy (EnemyChase > Drops Star) is a bit tougher than its blind (StarEnemyMultiplier), but it still
/// counts as that same blind: beating it moves you forward exactly one step.
///
/// Meter (0..1) = how far along the blinds you are. The difficulty meter UI and the battery bonus use it.
///
/// Debug: DifficultyProgress.SetDebugPoints(12) forces the difficulty (0..maxPoints = first..last blind)
/// for the rest of the session. DifficultyProgress.ClearDebugOverride() goes back to real progress.
/// </summary>
public static class DifficultyProgress
{
    // ---------------- Tweak these ----------------
    /// <summary>Score to beat for each blind, in order. Add more rows to extend past Ante 4.</summary>
    public static readonly int[] BlindScores =
    {
        300,  450,  600,    // Ante 1
        800,  1200, 1600,   // Ante 2
        2800, 4200, 5600,   // Ante 3
        6000, 9000, 12000   // Ante 4
    };

    public const int BlindsPerAnte = 3;
    private static readonly string[] BlindNames = { "Small Blind", "Big Blind", "Boss Blind" };

    /// <summary>A star enemy's score is its blind's score times this (1.1 = 10% higher).</summary>
    public static float StarEnemyMultiplier = 1.1f;

    /// <summary>
    /// ON: the 12 blinds are spread over however many enemies exist (counted from the EnemyChase objects), so the
    /// LAST enemy is the last blind. OFF: one blind per defeated enemy, whatever the enemy count.
    /// </summary>
    public static bool ScaleByEnemyCount = true;

    public static float maxPoints = 30f;       // only used by the debug override (SetDebugPoints)
    public static float pointsPerFight = 0.5f; // no longer used for the score (kept so old code still compiles)
    // ----------------------------------------------

    /// <summary>Enemies defeated this session (the tutorial fight doesn't count).</summary>
    public static int EnemiesDefeated { get; private set; }

    /// <summary>Battles fought this session (wins and losses).</summary>
    public static int BattlesFought { get; private set; }

    private static bool _overridden;
    private static float _debugPoints;

    /// <summary>True while the debug menu is forcing the difficulty.</summary>
    public static bool IsOverridden => _overridden;

    /// <summary>
    /// How many enemies there are in total: the number of EnemyChase objects found so far (beaten ones included).
    /// If none are known yet (e.g. the card game was opened directly) it falls back to one enemy per blind.
    /// </summary>
    public static int TotalEnemies
    {
        get
        {
            int n = EnemyChase.KnownEnemyCount;
            return n > 0 ? n : BlindScores.Length;
        }
    }

    /// <summary>Which blind you are on: 0 = Ante 1 Small Blind ... (BlindScores.Length - 1) = the last blind.</summary>
    public static int BlindIndex
    {
        get
        {
            int last = BlindScores.Length - 1;
            if (_overridden)
                return Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(_debugPoints / Mathf.Max(0.01f, maxPoints)) * last), 0, last);

            if (!ScaleByEnemyCount)
                return Mathf.Clamp(EnemiesDefeated, 0, last);

            // Spread the blinds over all the enemies: the first enemy is blind 0, the last enemy is the last blind
            int total = TotalEnemies;
            if (total <= 1) return 0;
            float t = Mathf.Clamp01(EnemiesDefeated / (float)(total - 1));
            return Mathf.Clamp(Mathf.RoundToInt(t * last), 0, last);
        }
    }

    public static int Ante => BlindIndex / BlindsPerAnte + 1;
    public static string BlindName => BlindNames[BlindIndex % BlindsPerAnte];
    public static string BlindLabel => "Ante " + Ante + " - " + BlindName;

    /// <summary>Score to beat of the blind you are on (normal enemy).</summary>
    public static int CurrentBlindScore => BlindScores[BlindIndex];

    /// <summary>
    /// Score to beat for the next enemy. A star enemy is slightly higher (rounded to 10) but it is still the same blind.
    /// </summary>
    public static int GetEnemyScore(bool isStarEnemy)
    {
        int score = CurrentBlindScore;
        if (!isStarEnemy) return score;
        return Mathf.Max(score, Mathf.RoundToInt(score * StarEnemyMultiplier / 10f) * 10);
    }

    /// <summary>Difficulty from 0 (first blind) to 1 (last blind).</summary>
    public static float Meter
    {
        get
        {
            int last = BlindScores.Length - 1;
            if (last <= 0) return 1f;
            if (_overridden) return Mathf.Clamp01(_debugPoints / Mathf.Max(0.01f, maxPoints));
            return Mathf.Clamp01(BlindIndex / (float)last);
        }
    }

    /// <summary>Difficulty as points, 0 to maxPoints (same scale the debug menu uses).</summary>
    public static float Points => Meter * maxPoints;

    /// <summary>Call after every battle ends (BattleManager does this).</summary>
    public static void AddBattle()
    {
        BattlesFought++;
    }

    /// <summary>Call when an enemy is defeated (won fight, or it exploded). Moves to the next blind.</summary>
    public static void AddDefeat()
    {
        EnemiesDefeated++;
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

    /// <summary>Wipe fights, defeats and the debug override (stars are reset in PlayerProgress).</summary>
    public static void ResetAll()
    {
        BattlesFought = 0;
        EnemiesDefeated = 0;
        _overridden = false;
    }

    /// <summary>
    /// Old formula (baseScore at meter 0, baseScore * maxMultiplier at meter 1). No longer used by the game.
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
        EnemiesDefeated = 0;
        _overridden = false;
        _debugPoints = 0f;
    }
}