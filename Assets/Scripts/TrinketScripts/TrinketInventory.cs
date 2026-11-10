using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Every trinket the player has taken from the shop. Session only (like BoonProgress):
/// it survives scene loads but is wiped when the game is closed.
/// </summary>
public static class TrinketInventory
{
    private static readonly List<TrinketData> owned = new List<TrinketData>();

    public static IReadOnlyList<TrinketData> Owned => owned;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnLaunch()
    {
        owned.Clear();
    }

    public static void Add(TrinketData trinket)
    {
        if (trinket != null) owned.Add(trinket);
    }

    public static int Count(TrinketType type)
    {
        int n = 0;
        foreach (TrinketData t in owned)
            if (t != null && t.type == type) n++;
        return n;
    }

    public static bool Has(TrinketType type)
    {
        return Count(type) > 0;
    }

    public static void Clear()
    {
        owned.Clear();
    }
}
