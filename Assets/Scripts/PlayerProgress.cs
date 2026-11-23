using UnityEngine;

public static class PlayerProgress
{
    private const string StarsKey = "TotalStars";

    private static int _totalStars;
    private static bool _loaded;

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
        PlayerPrefs.DeleteKey(StarsKey);
        _totalStars = 0;
        _loaded = true;
    }
}