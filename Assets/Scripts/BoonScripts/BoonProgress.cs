using UnityEngine;

public static class BoonProgress
{
    public const float StartingMeter = 0.5f;

    private static float meter = StartingMeter;
    private static float displayedMeter = StartingMeter;
    private static int extraHands;
    private static int extraHandSize;
    private static int bonusWindPower;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnLaunch()
    {
        ClearOldSavedData();
        ResetAll();
    }

    public static float Meter => meter;

    public static float DisplayedMeter
    {
        get { return displayedMeter; }
        set { displayedMeter = Mathf.Clamp01(value); }
    }

    public static bool IsMeterFull => meter >= 1f - 0.001f;

    public static int ExtraHands => extraHands;

    public static int ExtraHandSize => extraHandSize;

    public static int BonusWindPower => bonusWindPower;

    public static void AddMeter(float amount)
    {
        meter = Mathf.Clamp(meter + Mathf.Max(0f, amount), 0f, 5f);
    }

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