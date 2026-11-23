using UnityEngine;

public static class TrinketEffects
{
    public const float CandyMult = 2f;
    public const int SugarRushCandies = 6;
    public const float SugarRushXMult = 2f;

    public const float WindCharmMult = 3f;
    public const float WindFanMult = 10f;
    public const int WindFanRequiredCards = 5;

    public const float SunCharmMult = 3f;
    public const float SunPlushieChance = 0.5f;
    public const float SunPlushieXMult = 1.5f;

    public const float TsinelasMinDefence = 0.7f;
    public const float TsinelasMaxDefence = 1.0f;

    public static bool ApplyOnCardScored(TrinketData trinket, CardData card, ref float mult, out string label)
    {
        label = null;
        if (trinket == null || card == null) return false;

        switch (trinket.type)
        {
            case TrinketType.WindCharm:
                if (card is WindCard)
                {
                    mult += WindCharmMult;
                    label = "+" + Fmt(WindCharmMult) + " Mult";
                    return true;
                }
                break;

            case TrinketType.SunCharm:
                if (card is SolarCard)
                {
                    mult += SunCharmMult;
                    label = "+" + Fmt(SunCharmMult) + " Mult";
                    return true;
                }
                break;

            case TrinketType.SunPlushie:
                if (card is SolarCard)
                {
                    if (Random.value < SunPlushieChance)
                    {
                        mult *= SunPlushieXMult;
                        label = "X" + Fmt(SunPlushieXMult) + " Mult";
                        return true;
                    }
                    label = "Nope!";
                    return false;
                }
                break;
        }
        return false;
    }

    public static bool ApplyOnHandScored(TrinketData trinket, int windCardsInHand, ref float mult, out string label)
    {
        label = null;
        if (trinket == null) return false;

        switch (trinket.type)
        {
            case TrinketType.WindFan:
                if (windCardsInHand >= WindFanRequiredCards)
                {
                    mult += WindFanMult;
                    label = "+" + Fmt(WindFanMult) + " Mult";
                    return true;
                }
                break;

            case TrinketType.Candy:
                mult += CandyMult;
                label = "+" + Fmt(CandyMult) + " Mult";
                return true;
        }
        return false;
    }

    public static bool ApplySugarRush(ref float mult, out string label)
    {
        label = null;
        if (!SugarRushActive) return false;
        mult *= SugarRushXMult;
        label = "X" + Fmt(SugarRushXMult) + " Mult";
        return true;
    }

    public static int LastCandyIndex()
    {
        var owned = TrinketInventory.Owned;
        for (int i = owned.Count - 1; i >= 0; i--)
            if (owned[i] != null && owned[i].type == TrinketType.Candy) return i;
        return -1;
    }

    private static string Fmt(float v)
    {
        return v.ToString("0.##");
    }

    public static bool OnCardScored(CardData card, ref float mult)
    {
        bool triggered = false;

        if (card is WindCard)
        {
            int charms = TrinketInventory.Count(TrinketType.WindCharm);
            if (charms > 0)
            {
                mult += WindCharmMult * charms;
                triggered = true;
            }
        }
        else if (card is SolarCard)
        {
            int charms = TrinketInventory.Count(TrinketType.SunCharm);
            if (charms > 0)
            {
                mult += SunCharmMult * charms;
                triggered = true;
            }

            int plushies = TrinketInventory.Count(TrinketType.SunPlushie);
            for (int i = 0; i < plushies; i++)
            {
                if (Random.value < SunPlushieChance)
                {
                    mult *= SunPlushieXMult;
                    triggered = true;
                }
            }
        }

        return triggered;
    }

    public static float GetWindFanMult(int windCardsInHand)
    {
        if (windCardsInHand < WindFanRequiredCards) return 0f;
        return WindFanMult * TrinketInventory.Count(TrinketType.WindFan);
    }

    public static float GetCandyMult()
    {
        return CandyMult * TrinketInventory.Count(TrinketType.Candy);
    }

    public static bool SugarRushActive => TrinketInventory.Count(TrinketType.Candy) >= SugarRushCandies;

    public static int RerollEnemyDefence(int score)
    {
        int slippers = TrinketInventory.Count(TrinketType.Tsinelas);
        for (int i = 0; i < slippers; i++)
        {
            float roll = Random.Range(TsinelasMinDefence, TsinelasMaxDefence);
            score = Mathf.Max(1, Mathf.RoundToInt(score * roll));
        }
        return score;
    }

    public static bool HasDash => TrinketInventory.Has(TrinketType.WindFan);

    public static int ExtraStructures => TrinketInventory.Count(TrinketType.UnclesBusinessCard);

    public static bool CanStunEnemies => TrinketInventory.Has(TrinketType.Tsinelas);

    public static bool CanRemoveMoods => TrinketInventory.Has(TrinketType.SmallRock);
}