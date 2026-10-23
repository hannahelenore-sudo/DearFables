using System.Collections.Generic;
using UnityEngine;

public enum CardType { Attack, Skill, Power, Item }
public enum TargetType { SingleEnemy, AllEnemies, Self }

public class CardData : ScriptableObject
{
    [Header("Basic Info")]
    public string cardName = "New Card";
    [TextArea(2, 4)]
    public string description = "Card description here...";
    public Sprite cardArt;
    public CardType cardType = CardType.Attack;

    [Header("Costs & Targets")]
    public int energyCost = 1;
    public TargetType targetType = TargetType.SingleEnemy;

    [Header("Custom Stats")]
    public int primaryValue = 10;
    public int secondaryValue = 0;
    public Color frameColor = Color.white;

    /// <summary>
    /// Virtual function that each card script overrides to apply its custom logic.
    /// </summary>
    public virtual int EvaluateScore(int currentTotal, List<CardUI> selectedCards)
    {
        return currentTotal; // Default cards do not alter the score
    }
}