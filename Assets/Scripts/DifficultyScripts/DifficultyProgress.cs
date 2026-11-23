using UnityEngine;

public static class DifficultyProgress
{
    public static readonly int[] BlindScores =
    {
        300,  450,  600,
        800,  1200, 1600,
        2800, 4200, 5600,
        6000, 9000, 12000
    };

    public const int BlindsPerAnte = 3;
    private static readonly string[] BlindNames = { "Small Blind", "Big Blind", "Boss Blind" };

    public static float StarEnemyMultiplier = 1.1f;

    public static bool ScaleByEnemyCount = true;

    public static float maxPoints = 30f;
    public static float pointsPerFight = 0.5f;

    public static int EnemiesDefeated { get; private set; }

    public static int BattlesFought { get; private set; }

    private static bool _overridden;
    private static float _debugPoints;

    public static bool IsOverridden => _overridden;

    public static int TotalEnemies
    {
        get
        {
            int n = EnemyChase.KnownEnemyCount;
            return n > 0 ? n : BlindScores.Length;
        }
    }

    public static int BlindIndex
    {
        get
        {
            int last = BlindScores.Length - 1;
            if (_overridden)
                return Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(_debugPoints / Mathf.Max(0.01f, maxPoints)) * last), 0, last);

            if (!ScaleByEnemyCount)
                return Mathf.Clamp(EnemiesDefeated, 0, last);

            int total = TotalEnemies;
            if (total <= 1) return 0;
            float t = Mathf.Clamp01(EnemiesDefeated / (float)(total - 1));
            return Mathf.Clamp(Mathf.RoundToInt(t * last), 0, last);
        }
    }

    public static int Ante => BlindIndex / BlindsPerAnte + 1;
    public static string BlindName => BlindNames[BlindIndex % BlindsPerAnte];
    public static string BlindLabel => "Ante " + Ante + " - " + BlindName;

    public static int CurrentBlindScore => BlindScores[BlindIndex];

    public static int GetEnemyScore(bool isStarEnemy)
    {
        int score = CurrentBlindScore;
        if (!isStarEnemy) return score;
        return Mathf.Max(score, Mathf.RoundToInt(score * StarEnemyMultiplier / 10f) * 10);
    }

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

    public static float Points => Meter * maxPoints;

    public static void AddBattle()
    {
        BattlesFought++;
    }

    public static void AddDefeat()
    {
        EnemiesDefeated++;
    }

    public static void SetDebugPoints(float points)
    {
        _overridden = true;
        _debugPoints = Mathf.Clamp(points, 0f, maxPoints);
    }

    public static void ClearDebugOverride()
    {
        _overridden = false;
    }

    public static void ResetAll()
    {
        BattlesFought = 0;
        EnemiesDefeated = 0;
        _overridden = false;
    }

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