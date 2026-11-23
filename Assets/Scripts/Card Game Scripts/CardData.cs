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

    [Header("Hover UI Settings")]
    [Tooltip("Icon image displayed inside the hover bubble tooltip text.")]
    public Sprite hoverIcon;

    [HideInInspector]
    [Tooltip("Set at runtime by DeckManager: a drawn copy that only scores half. Never set this on the asset.")]
    public bool isLowPower;

    [System.NonSerialized] public CardData lowPowerSource;

    public static int DebugWindPower;
    public static int DebugSunPower;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetDebugPower()
    {
        DebugWindPower = 0;
        DebugSunPower = 0;
    }

    protected int StructurePower()
    {
        if (this is SolarCard) return StructureProgress.PowerBonus(StructureType.Solar);
        if (this is WindCard) return StructureProgress.PowerBonus(StructureType.Wind);
        return 0;
    }

    protected int ApplyLowPower(int value)
    {
        if (!isLowPower || value <= 0) return value;
        return Mathf.Max(1, value / 2);
    }

    public virtual int GetEffectiveValue()
    {
        int sun = this is SolarCard ? DebugSunPower : 0;
        return ApplyLowPower(primaryValue + MoodManager.PowerBonus + sun + StructurePower());
    }

    public virtual int EvaluateScore(int currentTotal, List<CardUI> selectedCards)
    {
        return currentTotal;
    }
}