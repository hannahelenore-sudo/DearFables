using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A UI graphic that draws real animated liquid: two layered waves, a shiny surface line and rising bubbles.
/// - It fills up smoothly to a level (0-1) and can drain away again.
/// - The surface stays level when the card tilts, and sloshes a little as it catches up.
/// Needs no sprites, materials or shaders. CardUI creates and drives it for Low Power cards.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class LowPowerLiquid : MaskableGraphic
{
    [Header("Liquid")]
    public Color liquidColor = new Color(0.32f, 0.55f, 0.5f, 0.7f);
    [Tooltip("How fast the liquid rises / drains.")]
    public float fillEase = 2.5f;

    [Header("Waves")]
    public float waveHeight = 6f;
    [Tooltip("How many waves fit across the card.")]
    public float waveCount = 1.6f;
    public float waveSpeed = 2f;
    public int segments = 28;

    [Header("Bubbles")]
    public int bubbleCount = 5;
    public float bubbleSize = 5f;

    [Header("Slosh")]
    public float sloshStiffness = 60f;
    public float sloshDamping = 7f;
    public float maxTilt = 22f;

    // current state
    private float fill;
    private float targetFill;
    private bool draining;
    private float targetTilt;
    private float tiltAngle;
    private float tiltVel;

    private float[] bx, bProgress, bSpeed, bSize, bPhase;

    public float CurrentFill { get { return fill; } }

    // ------------------------------------------------------------------
    // Control
    // ------------------------------------------------------------------

    /// <summary>Rises (or lowers) the liquid to this level, 0 = empty, 1 = full.</summary>
    public void FillTo(float level)
    {
        draining = false;
        targetFill = Mathf.Clamp01(level);
        if (transform.parent != null && !transform.parent.gameObject.activeSelf)
            transform.parent.gameObject.SetActive(true);
    }

    /// <summary>Drains the liquid away, then hides it.</summary>
    public void Drain()
    {
        targetFill = 0f;
        draining = true;
    }

    /// <summary>Call every frame with the card's rotation (degrees) so the surface stays level.</summary>
    public void SetCardRotation(float rotZ)
    {
        targetTilt = -rotZ;
    }

    /// <summary>Gives the surface a kick so it sloshes (hover, pick up...).</summary>
    public void Slosh(float impulse)
    {
        tiltVel += impulse;
    }

    // ------------------------------------------------------------------
    // Animation
    // ------------------------------------------------------------------

    protected override void OnEnable()
    {
        base.OnEnable();
        color = Color.white;
        raycastTarget = false;
        InitBubbles();
    }

    private void InitBubbles()
    {
        int n = Mathf.Max(0, bubbleCount);
        bx = new float[n];
        bProgress = new float[n];
        bSpeed = new float[n];
        bSize = new float[n];
        bPhase = new float[n];

        for (int i = 0; i < n; i++)
        {
            bx[i] = Random.Range(0.12f, 0.88f);
            bProgress[i] = Random.value;
            bSpeed[i] = Random.Range(0.12f, 0.3f);
            bSize[i] = Random.Range(0.6f, 1.2f);
            bPhase[i] = Random.Range(0f, 6.28f);
        }
    }

    private void Update()
    {
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);

        fill = Mathf.Lerp(fill, targetFill, 1f - Mathf.Exp(-fillEase * dt));
        if (Mathf.Abs(fill - targetFill) < 0.002f) fill = targetFill;

        // the surface chases the level line with a little overshoot
        float accel = (targetTilt - tiltAngle) * sloshStiffness - tiltVel * sloshDamping;
        tiltVel += accel * dt;
        tiltAngle = Mathf.Clamp(tiltAngle + tiltVel * dt, -maxTilt * 1.5f, maxTilt * 1.5f);

        for (int i = 0; i < bProgress.Length; i++)
        {
            bProgress[i] += bSpeed[i] * dt;
            if (bProgress[i] > 1f)
            {
                bProgress[i] = 0f;
                bx[i] = Random.Range(0.12f, 0.88f);
            }
        }

        if (draining && fill <= 0.004f)
        {
            draining = false;
            fill = 0f;
            if (transform.parent != null) transform.parent.gameObject.SetActive(false);
        }

        SetVerticesDirty();
    }

    // ------------------------------------------------------------------
    // Mesh
    // ------------------------------------------------------------------

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (fill <= 0.002f) return;

        Rect r = rectTransform.rect;
        if (r.width <= 1f || r.height <= 1f) return;

        float t = Time.unscaledTime;
        float baseY = Mathf.Lerp(r.yMin, r.yMax, fill);
        float slope = Mathf.Tan(Mathf.Clamp(tiltAngle, -maxTilt, maxTilt) * Mathf.Deg2Rad);

        Color backTop = liquidColor; backTop.a *= 0.55f;
        Color backBottom = backTop; backBottom.a *= 0.8f;

        Color frontTop = liquidColor;
        Color frontBottom = new Color(liquidColor.r * 0.65f, liquidColor.g * 0.65f, liquidColor.b * 0.65f, Mathf.Min(1f, liquidColor.a * 1.1f));

        Color shine = new Color(1f, 1f, 1f, 0.45f);
        Color shineFade = new Color(1f, 1f, 1f, 0f);

        // back wave, front wave, shiny surface line
        AddStrip(vh, r, baseY, slope, waveHeight * 1.15f, 1.7f, 0.8f, t, 0f, backTop, backBottom);
        AddStrip(vh, r, baseY - 3f, slope, waveHeight, 0f, 1f, t, 0f, frontTop, frontBottom);
        AddStrip(vh, r, baseY - 3f, slope, waveHeight, 0f, 1f, t, 5f, shine, shineFade);

        // bubbles
        if (r.height * fill > 24f)
        {
            for (int i = 0; i < bProgress.Length; i++)
            {
                float x01 = bx[i] + 0.04f * Mathf.Sin(t * 1.3f + bPhase[i]);
                float x = Mathf.Lerp(r.xMin, r.xMax, x01);
                float surface = Surface(r, x01, baseY - 3f, slope, waveHeight, 0f, 1f, t);
                float y = Mathf.Lerp(r.yMin + 6f, surface - 5f, bProgress[i]);
                float alpha = 0.4f * Mathf.Sin(bProgress[i] * Mathf.PI);
                AddCircle(vh, new Vector2(x, y), bubbleSize * bSize[i], new Color(1f, 1f, 1f, alpha));
            }
        }
    }

    private float Surface(Rect r, float x01, float baseY, float slope, float amp, float phase, float speedMul, float t)
    {
        float x = (x01 - 0.5f) * r.width;
        float wave = Mathf.Sin(x01 * waveCount * 6.2832f + t * waveSpeed * speedMul + phase)
                   + 0.5f * Mathf.Sin(x01 * waveCount * 1.7f * 6.2832f - t * waveSpeed * 1.3f * speedMul + phase * 2f);
        float y = baseY + x * slope + amp * 0.7f * wave;
        return Mathf.Clamp(y, r.yMin, r.yMax);
    }

    // A ribbon that follows the wave. thickness 0 = reaches down to the bottom of the card.
    private void AddStrip(VertexHelper vh, Rect r, float baseY, float slope, float amp, float phase, float speedMul,
        float t, float thickness, Color topColor, Color bottomColor)
    {
        int start = vh.currentVertCount;
        int seg = Mathf.Max(4, segments);

        for (int i = 0; i <= seg; i++)
        {
            float x01 = i / (float)seg;
            float x = Mathf.Lerp(r.xMin, r.xMax, x01);
            float surface = Surface(r, x01, baseY, slope, amp, phase, speedMul, t);
            float bottom = thickness > 0f ? Mathf.Max(r.yMin, surface - thickness) : r.yMin;

            vh.AddVert(new Vector3(x, surface), topColor, Vector2.zero);
            vh.AddVert(new Vector3(x, bottom), bottomColor, Vector2.zero);
        }

        for (int i = 0; i < seg; i++)
        {
            int a = start + i * 2;
            vh.AddTriangle(a + 1, a, a + 2);
            vh.AddTriangle(a + 2, a + 3, a + 1);
        }
    }

    private void AddCircle(VertexHelper vh, Vector2 center, float radius, Color c)
    {
        const int sides = 10;
        int start = vh.currentVertCount;
        vh.AddVert(new Vector3(center.x, center.y), c, Vector2.zero);

        for (int i = 0; i < sides; i++)
        {
            float a = i / (float)sides * 6.2832f;
            vh.AddVert(new Vector3(center.x + Mathf.Cos(a) * radius, center.y + Mathf.Sin(a) * radius), c, Vector2.zero);
        }

        for (int i = 0; i < sides; i++)
        {
            int next = (i + 1) % sides;
            vh.AddTriangle(start, start + 1 + next, start + 1 + i);
        }
    }
}
