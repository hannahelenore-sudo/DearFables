using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SolarCard", menuName = "Custom Card System/Cards/Solar Card")]
public class SolarCard : CardData
{
    public int requiredCards = 5;
    public float multiplier = 1.5f;

    public override int EvaluateScore(int currentTotal, List<CardUI> selectedCards)
    {
        if (selectedCards != null)
        {
            // Count how many actual Solar cards are in the selection
            int solarCount = 0;
            foreach (var card in selectedCards)
            {
                if (card != null && card.cardData is SolarCard)
                {
                    solarCount++;
                }
            }

            // Apply 1.5x only once if exactly 5 solar cards are selected
            if (solarCount >= requiredCards)
            {
                return Mathf.RoundToInt(currentTotal * multiplier);
            }
        }
        return currentTotal;
    }
}