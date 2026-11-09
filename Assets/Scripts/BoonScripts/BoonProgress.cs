using UnityEngine;

/// <summary>
/// Remembers the boon meter and every permanent upgrade. Static, so it survives scene loads,
/// and saved to PlayerPrefs so it survives closing the game.
/// Call BoonProgress.ResetAll() from your "New Game" button to wipe everything.
/// </summary>
public static class BoonProgress
{
    private const string MeterKey = "Boon_Meter";
    private const string DisplayedKey = "Boon_MeterDisplayed";
    private const string HandsKey = "Boon_ExtraHands";
    private const string HandSizeKey = "Boon_ExtraHandSize";
    private const string WindKey = "Boon_BonusWindPower";

    /// <summary>How full the meter is at the start of a new game (0.5 = half).</summary>
    public const float StartingMeter = 0.5f;

    private static bool loaded;
    private static float meter;
    private static float displayedMeter;
    private static int extraHands;
    private static int extraHandSize;
    private static int bonusWindPower;

    /// <summary>Real meter value. 1 = full = a boon is ready. Can go above 1 if several are earned.</summary>
    public static float Meter { get { Load(); return meter; } }

    /// <summary>What the meter UI last showed (0-1). Lets the UI animate from the old value to the new one.</summary>
    public static float DisplayedMeter
    {
        get { Load(); return displayedMeter; }
        set { Load(); displayedMeter = Mathf.Clamp01(value); Save(); }
    }

    public static bool IsMeterFull { get { Load(); return meter >= 1f - 0.001f; } }

    /// <summary>Extra hands (scoring attempts) per battle. Added to BattleManager.maxRounds.</summary>
    public static int ExtraHands { get { Load(); return extraHands; } }

    /// <summary>Extra cards in hand. Added to DeckManager.maxHandSize.</summary>
    public static int ExtraHandSize { get { Load(); return extraHandSize; } }

    /// <summary>Flat Wind Power added to every played hand.</summary>
    public static int BonusWindPower { get { Load(); return bonusWindPower; } }

    private static void Load()
    {
        if (loaded) return;
        loaded = true;

        // First ever launch (nothing saved yet): the meter starts half full
        meter = PlayerPrefs.GetFloat(MeterKey, StartingMeter);
        displayedMeter = PlayerPrefs.GetFloat(DisplayedKey, StartingMeter);
        extraHands = PlayerPrefs.GetInt(HandsKey, 0);
        extraHandSize = PlayerPrefs.GetInt(HandSizeKey, 0);
        bonusWindPower = PlayerPrefs.GetInt(WindKey, 0);
    }

    private static void Save()
    {
        PlayerPrefs.SetFloat(MeterKey, meter);
        PlayerPrefs.SetFloat(DisplayedKey, displayedMeter);
        PlayerPrefs.SetInt(HandsKey, extraHands);
        PlayerPrefs.SetInt(HandSizeKey, extraHandSize);
        PlayerPrefs.SetInt(WindKey, bonusWindPower);
        PlayerPrefs.Save();
    }

    /// <summary>Call after a fight. amount is a fraction of the meter (0.25 = a quarter).</summary>
    public static void AddMeter(float amount)
    {
        Load();
        meter = Mathf.Clamp(meter + Mathf.Max(0f, amount), 0f, 5f);
        Save();
    }

    /// <summary>Takes one full meter away after a boon was picked. Leftover carries over.</summary>
    public static void ConsumeFullMeter()
    {
        Load();
        meter = Mathf.Max(0f, meter - 1f);
        Save();
    }

    public static void ApplyBoon(BoonData boon)
    {
        if (boon == null) return;
        Load();

        switch (boon.type)
        {
            case BoonType.ExtraHandPerRound: extraHands += boon.amount; break;
            case BoonType.HandSize: extraHandSize += boon.amount; break;
            case BoonType.WindPower: bonusWindPower += boon.amount; break;
        }

        Save();
    }

#if UNITY_EDITOR
    [UnityEditor.MenuItem("Tools/Boons/Reset Boon Progress")]
#endif
    public static void ResetAll()
    {
        loaded = true;
        meter = StartingMeter;
        displayedMeter = StartingMeter;
        extraHands = 0;
        extraHandSize = 0;
        bonusWindPower = 0;
        Save();
    }
}