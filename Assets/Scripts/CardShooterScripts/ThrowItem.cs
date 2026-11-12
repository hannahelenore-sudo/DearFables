using System;
using UnityEngine;

/// <summary>Everything the player can hold. Press E to cycle through the ones you own.</summary>
public enum ThrowItem
{
    Card,
    Tsinelas,
    SmallRock,
    WindCard
}

/// <summary>
/// Generates simple fallback sprites in code, so everything works before you have real art.
/// If you assign real sprites (in CardShooter or in the Trinket assets) those are used instead.
/// </summary>
public static class ItemSprites
{
    private static Sprite card, tsinelas, rock, wind, star, streak;

    public static Sprite Card { get { if (card == null) card = Make(64, 64, CardPixel); return card; } }
    public static Sprite Tsinelas { get { if (tsinelas == null) tsinelas = Make(64, 64, TsinelasPixel); return tsinelas; } }
    public static Sprite Rock { get { if (rock == null) rock = Make(64, 64, RockPixel); return rock; } }
    public static Sprite Wind { get { if (wind == null) wind = Make(64, 64, WindPixel); return wind; } }
    public static Sprite Star { get { if (star == null) star = Make(64, 64, StarPixel); return star; } }
    public static Sprite Streak { get { if (streak == null) streak = Make(96, 12, StreakPixel); return streak; } }

    private static Sprite Make(int w, int h, Func<float, float, Color> pixel)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Color[] px = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                px[y * w + x] = pixel((x + 0.5f) / w, (y + 0.5f) / h);

        tex.SetPixels(px);
        tex.Apply();

        // pixelsPerUnit = width -> the sprite is exactly 1 unit wide
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), w);
    }

    // d = signed distance (negative = inside). Gives a soft 2px edge.
    private static float Aa(float d)
    {
        return Mathf.Clamp01(0.5f - d / 0.03f);
    }

    private static float SegDist(float px, float py, float ax, float ay, float bx, float by)
    {
        float abx = bx - ax, aby = by - ay;
        float t = Mathf.Clamp01(((px - ax) * abx + (py - ay) * aby) / (abx * abx + aby * aby));
        float cx = ax + abx * t, cy = ay + aby * t;
        return Mathf.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy));
    }

    private static Color CardPixel(float u, float v)
    {
        float dx = Mathf.Abs(u - 0.5f) - (0.3f - 0.07f);
        float dy = Mathf.Abs(v - 0.5f) - (0.42f - 0.07f);
        float d = new Vector2(Mathf.Max(dx, 0f), Mathf.Max(dy, 0f)).magnitude + Mathf.Min(Mathf.Max(dx, dy), 0f) - 0.07f;
        Color c = d > -0.035f ? new Color(0.6f, 0.1f, 0.1f) : new Color(0.98f, 0.95f, 0.85f);
        return new Color(c.r, c.g, c.b, Aa(d));
    }

    private static Color TsinelasPixel(float u, float v)
    {
        float ex = (u - 0.5f) / 0.26f;
        float ey = (v - 0.5f) / 0.44f;
        float d = (Mathf.Sqrt(ex * ex + ey * ey) - 1f) * 0.26f;
        float soleA = Aa(d);
        Color sole = d > -0.03f ? new Color(0.45f, 0.28f, 0.14f) : new Color(0.88f, 0.68f, 0.42f);

        float sd = Mathf.Min(SegDist(u, v, 0.5f, 0.68f, 0.33f, 0.48f), SegDist(u, v, 0.5f, 0.68f, 0.67f, 0.48f));
        float strapA = Aa(sd - 0.045f);

        Color c = Color.Lerp(sole, new Color(0.85f, 0.2f, 0.2f), strapA);
        return new Color(c.r, c.g, c.b, Mathf.Max(soleA, strapA));
    }

    private static Color RockPixel(float u, float v)
    {
        float dx = u - 0.5f, dy = v - 0.5f;
        float r = Mathf.Sqrt(dx * dx + dy * dy);
        float th = Mathf.Atan2(dy, dx);
        float R = 0.36f * (1f + 0.1f * Mathf.Sin(5f * th + 1f) + 0.05f * Mathf.Sin(9f * th));
        float d = r - R;

        float shade = Mathf.Clamp01(0.55f + (-dx * 0.6f + dy * 0.8f));
        float g = Mathf.Lerp(0.35f, 0.75f, shade);
        if (d > -0.035f) g *= 0.6f;
        return new Color(g, g, g * 1.05f, Aa(d));
    }

    private static Color WindPixel(float u, float v)
    {
        float dx = u - 0.5f, dy = v - 0.5f;
        float r = Mathf.Sqrt(dx * dx + dy * dy);
        float th = Mathf.Atan2(dy, dx);

        float t = Mathf.Repeat(th / (2f * Mathf.PI) - r * 4f, 1f);
        float line = Mathf.Min(t, 1f - t); // 0 right on the spiral
        float a = Mathf.Clamp01((0.14f - line) / 0.05f);
        a *= Mathf.Clamp01((0.44f - r) / 0.05f) * Mathf.Clamp01((r - 0.06f) / 0.04f);
        return new Color(0.75f, 0.93f, 1f, a);
    }

    private static Color StarPixel(float u, float v)
    {
        float dx = u - 0.5f, dy = v - 0.5f;
        float r = Mathf.Sqrt(dx * dx + dy * dy);
        float th = Mathf.Atan2(dx, dy); // angle from straight up
        float k = Mathf.Abs(Mathf.Repeat(th * 5f / (2f * Mathf.PI), 1f) - 0.5f) * 2f; // 1 at the points
        float R = Mathf.Lerp(0.2f, 0.47f, k);
        float d = r - R;
        Color c = d > -0.04f ? new Color(1f, 0.6f, 0.1f) : new Color(1f, 0.93f, 0.3f);
        return new Color(c.r, c.g, c.b, Aa(d));
    }

    // Wind streak: the head is on the right (u = 1), the tail fades out on the left
    private static Color StreakPixel(float u, float v)
    {
        float w = Mathf.Pow(u, 0.7f) * Mathf.Clamp01((1f - u) * 8f);
        float d = Mathf.Abs(v - 0.5f) * 2f;
        float a = Mathf.Clamp01((w - d) / 0.3f) * Mathf.Pow(u, 0.5f);
        return new Color(1f, 1f, 1f, a);
    }
}
