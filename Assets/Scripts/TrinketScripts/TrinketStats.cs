using System;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Where the sidebar reads the Sun Power / Wind Power numbers from.
/// If your boons store them somewhere else, change the two properties below - nothing else needs touching.
/// </summary>
public static class TrinketStats
{
    /// <summary>Permanent Wind Power (from the Choose Your Boon screen).</summary>
    public static float WindPower
    {
        get { return BoonProgress.BonusWindPower + CardData.DebugWindPower; }
    }

    /// <summary>
    /// Permanent Sun Power. I couldn't see a Sun boon in the scripts I was given, so this looks for a
    /// BoonProgress member called BonusSunPower / BonusSolarPower and shows 0 if there is none.
    /// Replace this with your real value if it lives elsewhere.
    /// </summary>
    public static float SunPower
    {
        get { return ReadBoon("BonusSunPower", "BonusSolarPower", "BonusSunnyPower") + CardData.DebugSunPower; }
    }

    // ---------------------------------------------------------------

    private static MemberInfo sunMember;
    private static bool searched;

    private static float ReadBoon(params string[] names)
    {
        if (!searched)
        {
            searched = true;
            Type t = typeof(BoonProgress);
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            foreach (string n in names)
            {
                MemberInfo m = (MemberInfo)t.GetProperty(n, flags) ?? t.GetField(n, flags);
                if (m != null) { sunMember = m; break; }
            }
        }

        if (sunMember == null) return 0f;

        object v = sunMember is PropertyInfo
            ? ((PropertyInfo)sunMember).GetValue(null, null)
            : ((FieldInfo)sunMember).GetValue(null);
        return v == null ? 0f : Convert.ToSingle(v);
    }
}