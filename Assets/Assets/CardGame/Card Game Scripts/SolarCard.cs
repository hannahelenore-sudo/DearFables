using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SolarCard", menuName = "Custom Card System/Cards/Solar Card")]
public class SolarCard : CardData
{
    public int requiredCards = 5;
    public float multiplier = 1.5f;

    public override int EvaluateScore(int currentTotal, List<CardUI> selectedCards)
    {
        if (selectedCards != null && selectedCards.Count == requiredCards)
        {
            // Applies a 1.5x multiplier for the current score calculation
            return Mathf.RoundToInt(currentTotal * multiplier);
        }
        return currentTotal;
    }
}