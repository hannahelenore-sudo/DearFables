using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SolarCard", menuName = "Custom Card System/Cards/Solar Card")]
public class SolarCard : CardData
{
    [Header("Set Bonus")]
    [Tooltip("How many Solar cards must be selected to trigger the bonus.")]
    public int requiredCards = 5;
    [Tooltip("Mult applied once when the set bonus triggers.")]
    public float multiplier = 1.5f;

    [Header("Set Bonus Sound")]
    [Tooltip("Played when the set bonus triggers. If empty, DeckManager's fallback Set Bonus Sound is used.")]
    public AudioClip bonusSound;
    [Range(0f, 1f)] public float bonusVolume = 1f;

    /// <summary>The set bonus Mult including the Solar Panel's Mult upgrades.</summary>
    public float EffectiveMultiplier
    {
        get { return multiplier + StructureProgress.MultBonus(StructureType.Solar); }
    }

    public override int GetEffectiveValue()
    {
        // Keeps whatever the base class already adds, then the Solar Panel's Power upgrade (halved by Low Power like the rest)
        return base.GetEffectiveValue() + ApplyLowPower(StructureProgress.PowerBonus(StructureType.Solar));
    }

    public override int EvaluateScore(int currentTotal, List<CardUI> selectedCards)
    {
        if (selectedCards != null)
        {
            int solarCount = 0;
            foreach (var card in selectedCards)
            {
                if (card != null && card.cardData is SolarCard)
                {
                    solarCount++;
                }
            }

            if (solarCount >= requiredCards)
            {
                return Mathf.RoundToInt(currentTotal * EffectiveMultiplier);
            }
        }
        return currentTotal;
    }
}