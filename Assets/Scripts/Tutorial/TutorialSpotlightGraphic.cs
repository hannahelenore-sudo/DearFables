using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Full-screen dim that skips rectangular holes and outlines them.</summary>
public class TutorialSpotlightGraphic : MaskableGraphic
{
    readonly List<Rect> holes = new List<Rect>();
    Color dim = new Color(0, 0, 0, 0.8f), frame = Color.yellow;
    float thickness = 4f;

    public void SetHoles(List<Rect> screenHoles, Color dimColor, Color frameColor, float frameThickness)
    {
        holes.Clear();
        holes.AddRange(screenHoles);
        dim = dimColor; frame = frameColor; thickness = frameThickness;
        SetVerticesDirty();
    }

    void Update()
    {
        // keeps the frame pulsing
        if (holes.Count > 0) SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect full = rectTransform.rect;
        float k = canvas != null && canvas.scaleFactor > 0f ? 1f / canvas.scaleFactor : 1f;

        // screen px -> local units (pivot is bottom-left)
        var local = new List<Rect>(holes.Count);
        foreach (var h in holes)
            local.Add(Rect.MinMaxRect(h.xMin * k + full.xMin, h.yMin * k + full.yMin,
                                      h.xMax * k + full.xMin, h.yMax * k + full.yMin));

        var xs = new List<float> { full.xMin, full.xMax };
        var ys = new List<float> { full.yMin, full.yMax };
        foreach (var h in local)
        {
            xs.Add(Mathf.Clamp(h.xMin, full.xMin, full.xMax)); xs.Add(Mathf.Clamp(h.xMax, full.xMin, full.xMax));
            ys.Add(Mathf.Clamp(h.yMin, full.yMin, full.yMax)); ys.Add(Mathf.Clamp(h.yMax, full.yMin, full.yMax));
        }
        xs.Sort(); ys.Sort();

        Color32 dimC = dim;
        for (int xi = 0; xi < xs.Count - 1; xi++)
        {
            for (int yi = 0; yi < ys.Count - 1; yi++)
            {
                float x0 = xs[xi], x1 = xs[xi + 1], y0 = ys[yi], y1 = ys[yi + 1];
                if (x1 - x0 < 0.01f || y1 - y0 < 0.01f) continue;
                var centre = new Vector2((x0 + x1) * 0.5f, (y0 + y1) * 0.5f);
                bool inHole = false;
                foreach (var h in local) if (h.Contains(centre)) { inHole = true; break; }
                if (!inHole) Quad(vh, x0, y0, x1, y1, dimC);
            }
        }

        float pulse = 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * 4f);
        var fc = frame; fc.a *= pulse;
        Color32 frameC = fc;
        float t = thickness * k;
        foreach (var h in local)
        {
            Quad(vh, h.xMin - t, h.yMax, h.xMax + t, h.yMax + t, frameC);   // top
            Quad(vh, h.xMin - t, h.yMin - t, h.xMax + t, h.yMin, frameC);   // bottom
            Quad(vh, h.xMin - t, h.yMin, h.xMin, h.yMax, frameC);           // left
            Quad(vh, h.xMax, h.yMin, h.xMax + t, h.yMax, frameC);           // right
        }
    }

    static void Quad(VertexHelper vh, float x0, float y0, float x1, float y1, Color32 c)
    {
        int i = vh.currentVertCount;
        var v = UIVertex.simpleVert;
        v.color = c;
        v.position = new Vector3(x0, y0); vh.AddVert(v);
        v.position = new Vector3(x0, y1); vh.AddVert(v);
        v.position = new Vector3(x1, y1); vh.AddVert(v);
        v.position = new Vector3(x1, y0); vh.AddVert(v);
        vh.AddTriangle(i, i + 1, i + 2);
        vh.AddTriangle(i + 2, i + 3, i);
    }
}
