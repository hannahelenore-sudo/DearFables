using UnityEngine;

/// <summary>
/// Static save data that survives scene loads. No GameObject needed.
/// Stars are also written to PlayerPrefs so they persist between play sessions.
/// </summary>
public static class PlayerProgress
{
    private const string StarsKey = "TotalStars";

    private static int _totalStars;
    private static bool _loaded;

    /// <summary>Stars earned in the most recent battle (for "+N" popups). Reset with ClearLastGained().</summary>
    public static int LastGained { get; private set; }

    public static int TotalStars
    {
        get
        {
            EnsureLoaded();
            return _totalStars;
        }
    }

    public static void AddStars(int amount)
    {
        if (amount <= 0) return;
        EnsureLoaded();

        _totalStars += amount;
        LastGained += amount;
    }

    public static void ClearLastGained()
    {
        LastGained = 0;
    }

    /// <summary>Wipe all stars (handy for testing).</summary>
    public static void ResetAll()
    {
        _totalStars = 0;
        LastGained = 0;
        _loaded = true;
        PlayerPrefs.DeleteKey(StarsKey);
    }

    private static void EnsureLoaded()
    {
        if (_loaded) return;
        // Stars are NOT saved between sessions: every launch starts at 0.
        // (Also clears any stars saved by an older version of this script.)
        PlayerPrefs.DeleteKey(StarsKey);
        _totalStars = 0;
        _loaded = true;
    }
}