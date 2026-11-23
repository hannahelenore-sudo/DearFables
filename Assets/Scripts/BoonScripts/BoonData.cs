using UnityEngine;

public enum BoonType
{
    ExtraHandPerRound,
    HandSize,
    WindPower
}

[CreateAssetMenu(fileName = "NewBoon", menuName = "Custom Card System/Boon")]
public class BoonData : ScriptableObject
{
    [Header("Display")]
    public string boonName = "New Boon";
    [TextArea(2, 4)]
    [Tooltip("Leave empty to auto-generate text from the type and amount.")]
    public string description;
    [Tooltip("Image shown on the boon card.")]
    public Sprite icon;

    [Header("Effect")]
    public BoonType type = BoonType.HandSize;
    public int amount = 1;

    [Header("Sound")]
    [Tooltip("Played when this boon is picked. Falls back to the Select Sound on BoonSelectionUI if empty.")]
    public AudioClip pickSound;
    [Range(0f, 1f)] public float pickVolume = 1f;

    public string GetDescription()
    {
        if (!string.IsNullOrEmpty(description)) return description;

        switch (type)
        {
            case BoonType.ExtraHandPerRound: return "Permanently gain +" + amount + " hand per round";
            case BoonType.HandSize: return "+" + amount + " Hand Size";
            case BoonType.WindPower: return "+" + amount + " Wind Power";
        }
        return "";
    }

    public static BoonData CreateRuntime(string name, string desc, BoonType type, int amount)
    {
        BoonData data = CreateInstance<BoonData>();
        data.hideFlags = HideFlags.DontSave;
        data.boonName = name;
        data.description = desc;
        data.type = type;
        data.amount = amount;
        return data;
    }
}