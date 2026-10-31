using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "WindCard", menuName = "Custom Card System/Cards/Wind Card")]
public class WindCard : CardData
{
    public int requiredCards = 5;
    public int multiplier = 2;

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
                return currentTotal * multiplier;
            }
        }
        return currentTotal;
    }
}