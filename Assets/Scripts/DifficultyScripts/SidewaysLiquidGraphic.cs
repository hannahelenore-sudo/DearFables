using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Draws a block of liquid that fills SIDEWAYS: from the left edge of its rectangle
/// (or the right edge if fillFromRight is on) up to "level" (0-1), with a wavy leading edge.
/// Maskable, so a parent Mask clips it to the container sprite.
/// </summary>
public class SidewaysLiquidGraphic : MaskableGraphic
{
    public float level;
    public float amplitude = 6f;
    public float wavelength = 90f;
    public float phase;
    public float bottomShade = 0.7f; // how dark the liquid is at its back (the filled-from side)
    public bool fillFromRight = false;

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (level <= 0.0001f) return;

        Rect r = GetPixelAdjustedRect();

        float dir = fillFromRight ? -1f : 1f;
        float backX = fillFromRight ? r.xMax : r.xMin;
        float edgeX = backX + dir * r.width * level;

        // No waves when empty or full, so nothing pokes out at the extremes
        float envelope = Mathf.Clamp01(Mathf.Min(level, 1f - level) * 10f);
        float amp = amplitude * envelope;

        int segments = Mathf.Clamp(Mathf.CeilToInt(r.height / 6f), 8, 160);

        Color32 frontColor = color;
        Color32 backColor = new Color(color.r * bottomShade, color.g * bottomShade, color.b * bottomShade, color.a);

        for (int i = 0; i <= segments; i++)
        {
            float y = r.yMin + r.height * i / segments;
            float wave = Mathf.Sin(y / Mathf.Max(1f, wavelength) * Mathf.PI * 2f + phase);
            float front = Mathf.Clamp(edgeX + amp * wave, r.xMin, r.xMax);

            UIVertex v = UIVertex.simpleVert;

            v.position = new Vector3(backX, y);
            v.color = backColor;
            vh.AddVert(v);

            v.position = new Vector3(front, y);
            v.color = frontColor;
            vh.AddVert(v);
        }

        for (int i = 0; i < segments; i++)
        {
            int b0 = i * 2, f0 = b0 + 1, b1 = b0 + 2, f1 = b0 + 3;
            if (fillFromRight)
            {
                vh.AddTriangle(b0, f1, f0);
                vh.AddTriangle(b0, b1, f1);
            }
            else
            {
                vh.AddTriangle(b0, f0, f1);
                vh.AddTriangle(b0, f1, b1);
            }
        }
    }
}
