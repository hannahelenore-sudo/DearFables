using UnityEngine;

public static class DeckPower
{
    private static SolarCard sunCard;
    private static WindCard windCard;
    private static float nextSearch;

    public static float Sun
    {
        get
        {
            if (sunCard == null && Time.unscaledTime >= nextSearch) Search();
            return sunCard != null ? sunCard.GetEffectiveValue() : TrinketStats.SunPower;
        }
    }

    public static float Wind
    {
        get
        {
            if (windCard == null && Time.unscaledTime >= nextSearch) Search();
            return windCard != null ? windCard.GetEffectiveValue() : TrinketStats.WindPower;
        }
    }

    public static float Total { get { return Sun + Wind; } }

    private static void Search()
    {
        nextSearch = Time.unscaledTime + 1f;
        if (sunCard == null)
        {
            SolarCard[] s = Resources.FindObjectsOfTypeAll<SolarCard>();
            if (s.Length > 0) sunCard = s[0];
        }
        if (windCard == null)
        {
            WindCard[] w = Resources.FindObjectsOfTypeAll<WindCard>();
            if (w.Length > 0) windCard = w[0];
        }
    }
}