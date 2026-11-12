using UnityEngine;

/// <summary>
/// One mood. The three default moods (Neutral, Sugar Rush, Angry) are created as assets for you by
/// Tools > HUD > Create Mood Assets (in Assets/Resources/Moods). Change the numbers on those assets to retune them,
/// or right click > Create > Custom Card System > Mood to make new ones.
/// </summary>
[CreateAssetMenu(fileName = "NewMood", menuName = "Custom Card System/Mood")]
public class MoodData : ScriptableObject
{
    [Header("Display")]
    public string moodName = "Neutral";
    [TextArea(2, 4)]
    public string description;
    [Tooltip("The face shown in the circle. If empty, the HUD's Default Portrait is used.")]
    public Sprite portrait;
    public Color textColor = Color.white;
    [Tooltip("Tick for the 'no mood' state.")]
    public bool isNeutral = false;

    [Header("Duration")]
    [Tooltip("How many fights it lasts. 0 = until another mood replaces it.")]
    public int fights = 0;

    [Header("Effects")]
    [Tooltip("Multiplies the Mult of every hand you play. 2 = X2 Mult. 1 = no effect.")]
    public float scoreMultiplier = 1f;
    [Tooltip("Added to the Power of every card you play (Wind, Sun, ...).")]
    public int powerBonus = 0;
    [Tooltip("Added to your hands per fight (-1 = one fewer hand).")]
    public int handsModifier = 0;
    [Tooltip("Added to your reshuffles (discards) per fight (-1 = one fewer).")]
    public int reshufflesModifier = 0;

    [Header("Sugar Rush only")]
    [Tooltip("How many candies you must gather (take from the shop) to start Sugar Rush.")]
    public int candiesNeeded = 6;

    public string GetEffectText()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        if (scoreMultiplier > 1.001f) sb.Append("X" + scoreMultiplier.ToString("0.##") + " Mult  ");
        if (powerBonus != 0) sb.Append((powerBonus > 0 ? "+" : "") + powerBonus + " Power  ");
        if (handsModifier != 0) sb.Append((handsModifier > 0 ? "+" : "") + handsModifier + " Hand  ");
        if (reshufflesModifier != 0) sb.Append((reshufflesModifier > 0 ? "+" : "") + reshufflesModifier + " Reshuffle  ");
        return sb.ToString().Trim();
    }

    /// <summary>The built-in values for "Neutral", "SugarRush" and "Angry". Used by the asset creator and as a fallback.</summary>
    public static MoodData Build(string key)
    {
        MoodData m = CreateInstance<MoodData>();
        switch (key)
        {
            case "SugarRush":
                m.name = "SugarRush";
                m.moodName = "Sugar Rush";
                m.description = "X2 Mult to all the cards played.";
                m.textColor = new Color(1f, 0.55f, 0.85f);
                m.fights = 3;
                m.scoreMultiplier = 2f;
                break;
            case "Angry":
                m.name = "Angry";
                m.moodName = "Angry";
                m.description = "+50 Power to all Power types. -1 Hand and Reshuffle. Lasts 3 fights.";
                m.textColor = new Color(1f, 0.35f, 0.3f);
                m.fights = 3;
                m.powerBonus = 50;
                m.handsModifier = -1;
                m.reshufflesModifier = -1;
                break;
            default:
                m.name = "Neutral";
                m.moodName = "Neutral";
                m.description = "Feeling normal.";
                m.isNeutral = true;
                break;
        }
        return m;
    }
}
