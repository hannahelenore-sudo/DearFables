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
    [Tooltip("Price in batteries. 0 = free. Balatro style: cheap utility 2-3, normal 4-5, strong 6-7.")]
    public int price = 4;

    /// <summary>What you get for selling it (half the price, at least 1 - like Balatro).</summary>
    public int SellValue { get { return BatteryManager.SellValueFor(price); } }

    public static TrinketData CreateRuntime(string name, string desc, TrinketType type, int price = 4)
    {
        TrinketData data = CreateInstance<TrinketData>();
        data.hideFlags = HideFlags.DontSave;
        data.trinketName = name;
        data.description = desc;
        data.type = type;
        data.price = price;
        return data;
    }

    public static List<TrinketData> BuildDefaults()
    {
        return new List<TrinketData>
        {
            // Prices follow Balatro's jokers: a flat +Mult joker is cheap, a "per scored card" one costs more,
            // a chance-based X Mult is the most expensive. A fight pays about 3-6 batteries.
            CreateRuntime("Candy", "+2 Mult.\nGather 6 candies to start Sugar Rush (X2 Mult).", TrinketType.Candy, 3),
            CreateRuntime("Tsinelas", "Stuns enemies and rerolls enemy defence.", TrinketType.Tsinelas, 5),
            CreateRuntime("Wind Charm", "Played cards with Wind Powered give +3 Mult when scored.", TrinketType.WindCharm, 5),
            CreateRuntime("Wind Fan", "+Dash.\n+10 Mult whenever the played hand contains 5 Wind Powered cards.", TrinketType.WindFan, 6),
            CreateRuntime("Sun Charm", "Played cards with Sun Powered give +3 Mult when scored.", TrinketType.SunCharm, 5),
            CreateRuntime("Sun Plushie", "1 in 2 chance for played Sun Powered cards to give X1.5 Mult when scored.", TrinketType.SunPlushie, 7),
            CreateRuntime("Uncle's Business Card", "Lets you build another Structure.", TrinketType.UnclesBusinessCard, 6),
            CreateRuntime("Small Rock", "Throw it at an enemy to remove any Moods.", TrinketType.SmallRock, 3)
        };
    }
}