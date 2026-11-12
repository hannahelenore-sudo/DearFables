using System;
using UnityEngine;

/// <summary>
/// The player's current mood. Static, so it survives scene loads; session only (wiped when the game closes).
/// Mood assets are read from Resources/Moods (Neutral, SugarRush, Angry) so editing the assets changes the game everywhere.
/// If an asset is missing, built-in defaults are used.
///
/// From your own scripts:  MoodManager.Set(MoodManager.Angry);   MoodManager.Clear();
/// </summary>
public static class MoodManager
{
    private static MoodData neutral, sugarRush, angry;
    private static MoodData current;
    private static int fightsLeft;
    private static int candiesGathered;

    /// <summary>Fires whenever the mood or its remaining fights change.</summary>
    public static event Action Changed;

    public static MoodData Neutral { get { return neutral != null ? neutral : (neutral = Load("Neutral")); } }
    public static MoodData SugarRush { get { return sugarRush != null ? sugarRush : (sugarRush = Load("SugarRush")); } }
    public static MoodData Angry { get { return angry != null ? angry : (angry = Load("Angry")); } }

    public static MoodData Current { get { return current != null ? current : Neutral; } }
    public static bool IsNeutral { get { return Current.isNeutral; } }

    /// <summary>Fights left for the current mood. 0 = no limit (or neutral).</summary>
    public static int FightsLeft { get { return fightsLeft; } }

    public static int CandiesGathered { get { return candiesGathered; } }

    // ---- What the current mood does (read by BattleManager / DeckManager / cards) ----
    public static float ScoreMultiplier { get { float m = Current.scoreMultiplier; return m > 0f ? m : 1f; } }
    public static int PowerBonus { get { return Current.powerBonus; } }
    public static int HandsModifier { get { return Current.handsModifier; } }
    public static int ReshufflesModifier { get { return Current.reshufflesModifier; } }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnLaunch()
    {
        neutral = sugarRush = angry = null;
        current = null;
        fightsLeft = 0;
        candiesGathered = 0;
    }

    private static MoodData Load(string key)
    {
        MoodData asset = Resources.Load<MoodData>("Moods/" + key);
        if (asset != null) return asset;

        MoodData fallback = MoodData.Build(key);
        fallback.hideFlags = HideFlags.DontSave;
        return fallback;
    }

    public static void Set(MoodData mood)
    {
        if (mood == null || mood.isNeutral)
        {
            Clear();
            return;
        }

        current = mood;
        fightsLeft = Mathf.Max(0, mood.fights);
        if (Changed != null) Changed();
    }

    public static void Clear()
    {
        bool had = current != null && !current.isNeutral;
        current = null;
        fightsLeft = 0;
        if (had && Changed != null) Changed();
    }

    /// <summary>Call when a fight finishes (win or lose). Counts down timed moods.</summary>
    public static void OnFightEnded()
    {
        if (IsNeutral || fightsLeft <= 0) return;

        fightsLeft--;
        if (fightsLeft <= 0) Clear();
        else if (Changed != null) Changed();
    }

    /// <summary>Call when the player gathers a candy. Returns true when it started Sugar Rush.</summary>
    public static bool AddCandy()
    {
        candiesGathered++;
        if (candiesGathered >= Mathf.Max(1, SugarRush.candiesNeeded))
        {
            candiesGathered = 0;
            Set(SugarRush);
            return true;
        }
        return false;
    }
}
