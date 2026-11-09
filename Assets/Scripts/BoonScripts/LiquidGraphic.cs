using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A UI liquid that is drawn as a living mesh instead of a flat image:
/// - waves on the surface (two layers, so it looks deep)
/// - sloshes side to side whenever it is filled or drained
/// - a thin foam line on the surface
/// - bubbles that rise through the liquid
///
/// Add it to an empty child object that is stretched to fill your container.
/// The normal "Color" field is the surface colour, "Deep Color" is the colour at the bottom.
/// Works with Masks, so put it under a Mask to clip it to a vial / bottle shape.
/// </summary>
[AddComponentMenu("UI/Liquid Graphic")]
public class LiquidGraphic : MaskableGraphic
{
    [Header("Fill")]
    [Range(0f, 1f)] public float fill = 0.5f;

    [Header("Colours")]
    [Tooltip("Colour at the bottom of the liquid. The Color field at the top of this component is the surface colour.")]
    public Color deepColor = new Color(0.10f, 0.30f, 0.80f, 1f);
    [Tooltip("Colour of the foam line on the surface. Set alpha to 0 to hide it.")]
    public Color foamColor = new Color(1f, 1f, 1f, 0.55f);
    [Tooltip("How see-through the back wave layer is.")]
    [Range(0f, 1f)] public float backWaveAlpha = 0.55f;

    [Header("Waves")]
    [Tooltip("Height of the waves in pixels.")]
    public float waveAmplitude = 6f;
    [Tooltip("How many waves fit across the width.")]
    public float waveCount = 1.5f;
    public float waveSpeed = 1.6f;
    [Range(8, 96)] public int segments = 32;
    public float foamThickness = 5f;

    [Header("Slosh (when filling / draining)")]
    [Tooltip("How far the surface tilts in pixels at the start of a slosh.")]
    public float sloshStrength = 14f;
    public float sloshSpeed = 7f;
    [Tooltip("Higher = the slosh settles faster.")]
    public float sloshDamping = 2.2f;

    [Header("Bubbles")]
    public int bubbleCount = 8;
    public Vector2 bubbleSizeRange = new Vector2(2.5f, 6f);
    [Tooltip("How fast bubbles rise (fraction of the liquid height per second).")]
    public float bubbleSpeed = 0.25f;
    public Color bubbleColor = new Color(1f, 1f, 1f, 0.35f);

    private class Bubble
    {
        public float x;      // 0..1 across the width
        public float y;      // 0..1 up through the liquid
        public float speed;  // multiplier
        public float size;   // pixels
        public float phase;
    }

    private readonly List<Bubble> bubbles = new List<Bubble>();
    private float time;
    private float sloshAmp;
    private float turbulence;
    private float lastFill = -1f;

    // -------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------

    public void SetFill(float value)
    {
        fill = Mathf.Clamp01(value);
        SetVerticesDirty();
    }

    /// <summary>Makes the liquid slosh. 1 = full strength.</summary>
    public void Slosh(float strength = 1f)
    {
        sloshAmp = Mathf.Max(sloshAmp, strength);
    }

    // -------------------------------------------------------------------
    // Unity
    // -------------------------------------------------------------------

    protected override void OnEnable()
    {
        base.OnEnable();
        raycastTarget = false; // never blocks clicks meant for the container
        EnsureBubbles();
        lastFill = fill;
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        EnsureBubbles();
        SetVerticesDirty();
    }
#endif

    private void Update()
    {
        if (!Application.isPlaying) return;

        float dt = Time.unscaledDeltaTime;
        if (dt <= 0f) return;

        time += dt;
        sloshAmp *= Mathf.Exp(-sloshDamping * dt);

        // The faster the level changes, the choppier the surface
        float speed = lastFill < 0f ? 0f : Mathf.Abs(fill - lastFill) / dt;
        lastFill = fill;
        turbulence = Mathf.Lerp(turbulence, Mathf.Clamp01(speed * 2f), 1f - Mathf.Exp(-10f * dt));

        UpdateBubbles(dt);
        SetVerticesDirty();
    }

    // -------------------------------------------------------------------
    // Bubbles
    // -------------------------------------------------------------------

    private void EnsureBubbles()
    {
        while (bubbles.Count < bubbleCount)
        {
            Bubble b = new Bubble();
            RespawnBubble(b, true);
            bubbles.Add(b);
        }

        while (bubbles.Count > bubbleCount && bubbles.Count > 0)
            bubbles.RemoveAt(bubbles.Count - 1);
    }

    private void RespawnBubble(Bubble b, bool randomHeight)
    {
        b.x = Random.value;
        b.y = randomHeight ? Random.value * 0.9f : 0f;
        b.speed = Random.Range(0.6f, 1.4f);
        b.size = Random.Range(bubbleSizeRange.x, bubbleSizeRange.y);
        b.phase = Random.value * 6.28f;
    }

    private void UpdateBubbles(float dt)
    {
        foreach (Bubble b in bubbles)
        {
            b.y += bubbleSpeed * b.speed * (1f + turbulence * 3f) * dt;
            if (b.y > 0.92f) RespawnBubble(b, false);
        }
    }

    // -------------------------------------------------------------------
    // Mesh
    // -------------------------------------------------------------------

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        if (fill <= 0.002f) return;

        Rect r = GetPixelAdjustedRect();
        float baseY = r.yMin + r.height * fill;

        // Waves calm down when almost empty or completely full, and get choppy while the level moves
        float amp = waveAmplitude * (1f + turbulence * 1.5f);
        amp *= Mathf.Clamp01(fill * 12f);
        amp *= Mathf.Clamp01((1f - fill) * 8f + 0.25f);

        Color top = color;
        Color deep = deepColor;

        Color backTop = top; backTop.a *= backWaveAlpha;
        Color backDeep = deep; backDeep.a *= backWaveAlpha;

        // 1. Back wave (different speed and shape, see-through)
        AddLayer(vh, r, baseY, amp * 0.8f, time * waveSpeed * 0.8f + 2f, 1.25f, backTop, backDeep, 0f);

        // 2. Front wave
        AddLayer(vh, r, baseY, amp, time * waveSpeed, 1f, top, deep, 0f);

        // 3. Foam line
        if (foamColor.a > 0.01f && foamThickness > 0.01f)
        {
            Color foamTop = foamColor;
            Color foamBottom = foamColor; foamBottom.a = 0f;
            AddLayer(vh, r, baseY, amp, time * waveSpeed, 1f, foamTop, foamBottom, foamThickness);
        }

        // 4. Bubbles
        if (fill > 0.08f && bubbleColor.a > 0.01f)
        {
            float liquidHeight = r.height * fill;
            foreach (Bubble b in bubbles)
            {
                float cx = r.xMin + b.x * r.width + Mathf.Sin(time * 2f + b.phase) * 3f;
                float cy = r.yMin + b.y * liquidHeight;
                cx = Mathf.Clamp(cx, r.xMin + b.size, r.xMax - b.size);
                AddCircle(vh, cx, cy, b.size, bubbleColor);
            }
        }
    }

    private float SurfaceY(float t, float baseY, float amp, float phase, float freqMul, Rect r)
    {
        float freq = Mathf.PI * 2f * waveCount * freqMul;

        float wave = Mathf.Sin(t * freq + phase) * 0.65f
                   + Mathf.Sin(t * freq * 1.9f + phase * 1.4f) * 0.35f;

        // Slosh tilts the whole surface like liquid rocking in a glass
        float tilt = sloshAmp * sloshStrength * Mathf.Sin(time * sloshSpeed) * (t - 0.5f) * 2f;

        return Mathf.Clamp(baseY + wave * amp + tilt, r.yMin, r.yMax);
    }

    // foam = 0: fill from the bottom of the rect up to the surface. foam > 0: only a thin band under the surface.
    private void AddLayer(VertexHelper vh, Rect r, float baseY, float amp, float phase, float freqMul,
                          Color topColor, Color bottomColor, float foam)
    {
        int start = vh.currentVertCount;

        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            float x = Mathf.Lerp(r.xMin, r.xMax, t);
            float y = SurfaceY(t, baseY, amp, phase, freqMul, r);
            float bottomY = foam > 0f ? Mathf.Max(r.yMin, y - foam) : r.yMin;

            AddVertex(vh, x, bottomY, bottomColor);
            AddVertex(vh, x, y, topColor);
        }

        for (int i = 0; i < segments; i++)
        {
            int a = start + i * 2;
            vh.AddTriangle(a, a + 1, a + 3);
            vh.AddTriangle(a, a + 3, a + 2);
        }
    }

    private void AddCircle(VertexHelper vh, float cx, float cy, float radius, Color col)
    {
        const int sides = 10;
        int start = vh.currentVertCount;

        AddVertex(vh, cx, cy, col);
        for (int i = 0; i < sides; i++)
        {
            float a = i / (float)sides * Mathf.PI * 2f;
            AddVertex(vh, cx + Mathf.Cos(a) * radius, cy + Mathf.Sin(a) * radius, col);
        }

        for (int i = 0; i < sides; i++)
        {
            int next = (i + 1) % sides;
            vh.AddTriangle(start, start + 1 + next, start + 1 + i);
        }
    }

    private static void AddVertex(VertexHelper vh, float x, float y, Color col)
    {
        UIVertex v = UIVertex.simpleVert;
        v.position = new Vector3(x, y, 0f);
        v.color = col;
        vh.AddVert(v);
    }
}
