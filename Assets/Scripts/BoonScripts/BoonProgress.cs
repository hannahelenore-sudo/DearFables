using UnityEngine;

/// <summary>
/// Remembers the boon meter and every permanent upgrade for the CURRENT PLAY SESSION only.
/// Static, so it survives scene loads (overworld <-> card battle), but it is NOT saved to disk:
/// when the game is closed, everything is gone and the next launch starts fresh
/// (meter half full, no boons).
/// Call BoonProgress.ResetAll() from your "New Game" button to wipe everything mid-session.
/// </summary>
public static class BoonProgress
{
    /// <summary>How full the meter is at the start of a new game (0.5 = half).</summary>
    public const float StartingMeter = 0.5f;

    private static float meter = StartingMeter;
    private static float displayedMeter = StartingMeter;
    private static int extraHands;
    private static int extraHandSize;
    private static int bonusWindPower;

    // Runs every time the game starts (also in the Editor with "Enter Play Mode Options" /
    // disabled domain reload, where statics would otherwise carry over between plays).
    // Also deletes the keys the old PlayerPrefs-based version saved, so nothing stale lingers.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnLaunch()
    {
        ClearOldSavedData();
        ResetAll();
    }

    /// <summary>Real meter value. 1 = full = a boon is ready. Can go above 1 if several are earned.</summary>
    public static float Meter => meter;

    /// <summary>What the meter UI last showed (0-1). Lets the UI animate from the old value to the new one.</summary>
    public static float DisplayedMeter
    {
        get { return displayedMeter; }
        set { displayedMeter = Mathf.Clamp01(value); }
    }

    public static bool IsMeterFull => meter >= 1f - 0.001f;

    /// <summary>Extra hands (scoring attempts) per battle. Added to BattleManager.maxRounds.</summary>
    public static int ExtraHands => extraHands;

    /// <summary>Extra cards in hand. Added to DeckManager.maxHandSize.</summary>
    public static int ExtraHandSize => extraHandSize;

    /// <summary>Flat Wind Power added to every played hand.</summary>
    public static int BonusWindPower => bonusWindPower;

    /// <summary>Call after a fight. amount is a fraction of the meter (0.25 = a quarter).</summary>
    public static void AddMeter(float amount)
    {
        meter = Mathf.Clamp(meter + Mathf.Max(0f, amount), 0f, 5f);
    }

    /// <summary>Takes one full meter away after a boon was picked. Leftover carries over.</summary>
    public static void ConsumeFullMeter()
    {
        meter = Mathf.Max(0f, meter - 1f);
    }

    public static void ApplyBoon(BoonData boon)
    {
        if (boon == null) return;

        switch (boon.type)
        {
            case BoonType.ExtraHandPerRound: extraHands += boon.amount; break;
            case BoonType.HandSize: extraHandSize += boon.amount; break;
            case BoonType.WindPower: bonusWindPower += boon.amount; break;
        }
    }

#if UNITY_EDITOR
    [UnityEditor.MenuItem("Tools/Boons/Reset Boon Progress")]
#endif
    public static void ResetAll()
    {
        meter = StartingMeter;
        displayedMeter = StartingMeter;
        extraHands = 0;
        extraHandSize = 0;
        bonusWindPower = 0;
    }

    private static void ClearOldSavedData()
    {
        string[] oldKeys =
        {
            "Boon_Meter", "Boon_MeterDisplayed", "Boon_ExtraHands",
            "Boon_ExtraHandSize", "Boon_BonusWindPower"
        };

        bool any = false;
        foreach (string key in oldKeys)
        {
            if (PlayerPrefs.HasKey(key)) { PlayerPrefs.DeleteKey(key); any = true; }
        }
        if (any) PlayerPrefs.Save();
    }
}