using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Every trinket the player owns (max 5). The ORDER matters: effects trigger from the top (index 0)
/// to the bottom, like Balatro jokers. Session only (like BoonProgress): it survives scene loads
/// but is wiped when the game is closed.
/// </summary>
public static class TrinketInventory
{
    public const int MaxTrinkets = 5;

    private static readonly List<TrinketData> owned = new List<TrinketData>();

    public static IReadOnlyList<TrinketData> Owned => owned;

    public static bool IsFull => owned.Count >= MaxTrinkets;

    // The sidebar listens to these to animate things
    public static event Action<int> Added;        // index it was added at
    public static event Action<int> Removed;      // index it was removed from
    public static event Action<int, int> Moved;   // from, to
    public static event Action Cleared;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnLaunch()
    {
        owned.Clear();
    }

    /// <summary>Returns false (and adds nothing) when the inventory is full.</summary>
    public static bool Add(TrinketData trinket)
    {
        if (trinket == null || IsFull) return false;
        owned.Add(trinket);
        if (Added != null) Added(owned.Count - 1);
        return true;
    }

    public static void RemoveAt(int index)
    {
        if (index < 0 || index >= owned.Count) return;
        owned.RemoveAt(index);
        if (Removed != null) Removed(index);
    }

    /// <summary>Changes the trigger order: takes the trinket at 'from' and puts it at 'to'.</summary>
    public static void Move(int from, int to)
    {
        if (from < 0 || from >= owned.Count) return;
        to = Mathf.Clamp(to, 0, owned.Count - 1);
        if (from == to) return;

        TrinketData t = owned[from];
        owned.RemoveAt(from);
        owned.Insert(to, t);
        if (Moved != null) Moved(from, to);
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
        if (Cleared != null) Cleared();
    }
}