using System.Collections.Generic;
using UnityEngine;

public enum TrinketType
{
    Candy,
    Tsinelas,
    WindCharm,
    WindFan,
    SunCharm,
    SunPlushie,
    UnclesBusinessCard,
    SmallRock
}

/// <summary>
/// One shop item. Create these via: Right click > Create > Custom Card System > Trinket
/// (to give them icons). If you don't create any, TrinketShopUI uses the 8 default trinkets (no icons).
/// NOTE: the effects are not implemented yet - the type is only an id for later.
/// </summary>
[CreateAssetMenu(fileName = "NewTrinket", menuName = "Custom Card System/Trinket")]
public class TrinketData : ScriptableObject
{
    public string trinketName = "New Trinket";
    [TextArea(2, 4)]
    public string description;
    public Sprite icon;
    public TrinketType type;

    public static TrinketData CreateRuntime(string name, string desc, TrinketType type)
    {
        TrinketData data = CreateInstance<TrinketData>();
        data.hideFlags = HideFlags.DontSave;
        data.trinketName = name;
        data.description = desc;
        data.type = type;
        return data;
    }

    public static List<TrinketData> BuildDefaults()
    {
        return new List<TrinketData>
        {
            CreateRuntime("Candy", "+2 Mult.\nSugar Rush: X2 Mult if you have gathered 6 candies.", TrinketType.Candy),
            CreateRuntime("Tsinelas", "Stuns enemies and rerolls enemy defence.", TrinketType.Tsinelas),
            CreateRuntime("Wind Charm", "Played cards with Wind Powered give +3 Mult when scored.", TrinketType.WindCharm),
            CreateRuntime("Wind Fan", "+Dash.\n+10 Mult whenever the played hand contains 5 Wind Powered cards.", TrinketType.WindFan),
            CreateRuntime("Sun Charm", "Played cards with Sun Powered give +3 Mult when scored.", TrinketType.SunCharm),
            CreateRuntime("Sun Plushie", "1 in 2 chance for played Sun Powered cards to give X1.5 Mult when scored.", TrinketType.SunPlushie),
            CreateRuntime("Uncle's Business Card", "Lets you build another Structure.", TrinketType.UnclesBusinessCard),
            CreateRuntime("Small Rock", "Throw it at an enemy to remove any Moods.", TrinketType.SmallRock)
        };
    }
}
