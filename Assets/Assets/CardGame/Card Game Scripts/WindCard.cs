using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "WindCard", menuName = "Custom Card System/Cards/Wind Card")]
public class WindCard : CardData
{
    public int requiredCards = 5;
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