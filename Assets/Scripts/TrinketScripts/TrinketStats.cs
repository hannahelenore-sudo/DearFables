using System;
using System.Reflection;
using UnityEngine;

public static class TrinketStats
{
    public static float WindPower
    {
        get { return BoonProgress.BonusWindPower + CardData.DebugWindPower; }
    }

    public static float SunPower
    {
        get { return ReadBoon("BonusSunPower", "BonusSolarPower", "BonusSunnyPower") + CardData.DebugSunPower; }
    }

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