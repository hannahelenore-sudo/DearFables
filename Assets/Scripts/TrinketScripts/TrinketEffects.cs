using UnityEngine;

/// <summary>
/// What each trinket actually does. DeckManager / BattleManager call these; the values are easy to tweak here.
/// The "overworld hooks" at the bottom are for your player / enemy / building scripts to check.
/// </summary>
public static class TrinketEffects
{
    // ---------------- Tuning ----------------
    public const float CandyMult = 2f;               // each Candy: +2 Mult
    public const int SugarRushCandies = 6;           // Sugar Rush: with this many Candies...
    public const float SugarRushXMult = 2f;          // ...your Mult is also multiplied by this

    public const float WindCharmMult = 3f;           // each Wind Charm: +3 Mult per Wind card scored
    public const float WindFanMult = 10f;            // each Wind Fan: +10 Mult...
    public const int WindFanRequiredCards = 5;       // ...if the played hand has this many Wind cards

    public const float SunCharmMult = 3f;            // each Sun Charm: +3 Mult per Sun card scored
    public const float SunPlushieChance = 0.5f;      // each Sun Plushie: 1 in 2 chance per Sun card scored...
    public const float SunPlushieXMult = 1.5f;       // ...to multiply Mult by this

    public const float TsinelasMinDefence = 0.7f;    // Tsinelas rerolls the score to beat to between
    public const float TsinelasMaxDefence = 1.0f;    // these fractions of itself (once per Tsinelas)

    // ---------------- Card game ----------------

    /// <summary>
    /// Called as each played card scores. Adds to / multiplies the mult.
    /// Returns true if any trinket triggered (so the game can play a sound).
    /// </summary>
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

    /// <summary>Wind Fan: bonus Mult if the played hand contains 5 Wind cards.</summary>
    public static float GetWindFanMult(int windCardsInHand)
    {
        if (windCardsInHand < WindFanRequiredCards) return 0f;
        return WindFanMult * TrinketInventory.Count(TrinketType.WindFan);
    }

    /// <summary>Candy: flat bonus Mult for every Candy you own.</summary>
    public static float GetCandyMult()
    {
        return CandyMult * TrinketInventory.Count(TrinketType.Candy);
    }

    /// <summary>Sugar Rush: 6 or more Candies.</summary>
    public static bool SugarRushActive => TrinketInventory.Count(TrinketType.Candy) >= SugarRushCandies;

    /// <summary>Tsinelas: rerolls the enemy's defence (the score to beat) at the start of a battle.</summary>
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

    // ---------------- Overworld hooks (for your other scripts) ----------------

    /// <summary>Wind Fan: the player may dash.</summary>
    public static bool HasDash => TrinketInventory.Has(TrinketType.WindFan);

    /// <summary>Uncle's Business Card: how many extra Structures the player may build.</summary>
    public static int ExtraStructures => TrinketInventory.Count(TrinketType.UnclesBusinessCard);

    /// <summary>Tsinelas: thrown at an enemy it stuns it.</summary>
    public static bool CanStunEnemies => TrinketInventory.Has(TrinketType.Tsinelas);

    /// <summary>Small Rock: thrown at an enemy it removes any Moods.</summary>
    public static bool CanRemoveMoods => TrinketInventory.Has(TrinketType.SmallRock);
}
