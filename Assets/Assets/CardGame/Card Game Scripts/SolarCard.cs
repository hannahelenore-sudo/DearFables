using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SolarCard", menuName = "Custom Card System/Cards/Solar Card")]
public class SolarCard : CardData
{
    public int requiredCards = 3;
    public int multiplier = 2;

    public override int EvaluateScore(int currentTotal, List<CardUI> selectedCards)
    {
        if (selectedCards != null && selectedCards.Count == requiredCards)
        {
            return currentTotal * multiplier;
        }
        return currentTotal;
    }
}