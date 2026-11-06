using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "WindCard", menuName = "Custom Card System/Cards/Wind Card")]
public class WindCard : CardData
{
    [Header("Set Bonus")]
    [Tooltip("How many Wind cards must be selected to trigger the bonus.")]
    public int requiredCards = 5;
    [Tooltip("Mult applied once when the set bonus triggers.")]
    public float multiplier = 2f;

    [Header("Set Bonus Sound")]
    [Tooltip("Played when the set bonus triggers. If empty, DeckManager's fallback Set Bonus Sound is used.")]
    public AudioClip bonusSound;
    [Range(0f, 1f)] public float bonusVolume = 1f;

    public override int EvaluateScore(int currentTotal, List<CardUI> selectedCards)
    {
        if (selectedCards != null)
        {
            int windCount = 0;
            foreach (var card in selectedCards)
            {
                if (card != null && card.cardData is WindCard)
                {
                    windCount++;
                }
            }

            if (windCount >= requiredCards)
            {
                return Mathf.RoundToInt(currentTotal * multiplier);
            }
        }
        return currentTotal;
    }
}