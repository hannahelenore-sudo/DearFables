using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Liquid-style difficulty meter (sideways). Put this on a UI object in your GAME scene (not the card game scene,
/// so it never shows up there).
///
/// SETUP
///  1. Right-click your Canvas > UI > Image (this is the container).
///  2. Drag your bar / tube sprite into that Image's "Source Image".
///  3. Add this DifficultyMeter component to the same object.
///  4. Move / resize / anchor it anywhere with the normal Rect Transform.
///
/// The liquid is clipped to the shape of your sprite (its opaque pixels), so draw the sprite as the
/// inside of the container. For a glass outline or shine on top of the liquid, put it in "Overlay Sprite".
/// The liquid fills sideways, from the left edge of the object's rectangle (or the right if "Fill From Right" is on).
/// It is visible in Play mode.
/// </summary>
[RequireComponent(typeof(Image))]
public class DifficultyMeter : MonoBehaviour
{
    [Header("Container")]
    [Tooltip("Optional. Drawn on top of the liquid (glass outline, shine, etc). Leave empty if the main sprite is enough.")]
    public Sprite overlaySprite;

    [Header("Direction")]
    [Tooltip("Off = fills left to right. On = fills right to left.")]
    public bool fillFromRight = false;

    [Header("Liquid Color")]
    public Color lowColor = new Color(0.3f, 0.85f, 0.3f);
    public Color midColor = new Color(1f, 0.85f, 0.2f);
    public Color highColor = new Color(0.9f, 0.2f, 0.2f);
    [Range(0.3f, 1f)]
    [Tooltip("How dark the liquid is at its back edge (1 = flat color, lower = deeper look).")]
    public float bottomShade = 0.7f;

    [Header("Liquid Motion")]
    [Tooltip("How fast the liquid fills, as a fraction of the whole meter per second. Low = slow creep.")]
    public float fillSpeed = 0.15f;
    [Tooltip("Size of the waves on the leading edge, in pixels.")]
    public float waveHeight = 6f;
    [Tooltip("Distance between wave peaks, in pixels.")]
    public float waveLength = 90f;
    public float waveSpeed = 2f;
    [Tooltip("Extra splashing while the liquid is moving.")]
    public float sloshAmount = 1.5f;

    [Header("Optional")]
    [Tooltip("Shows the difficulty as a percentage, e.g. 42%.")]
    public TextMeshProUGUI percentText;

    // Remembered between scenes so you SEE the liquid move after a battle
    private static float lastShown = -1f;

    private SidewaysLiquidGraphic backWave;
    private SidewaysLiquidGraphic frontWave;
    private float shown;
    private float slosh;
    private float phase;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        lastShown = -1f;
    }

    private void Awake()
    {
        // The container sprite doubles as the mask, so the liquid never leaves the shape
        Mask mask = GetComponent<Mask>();
        if (mask == null) mask = gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = true;

        backWave = CreateLiquid("LiquidBack");
        frontWave = CreateLiquid("LiquidFront");

        if (overlaySprite != null)
        {
            GameObject go = new GameObject("Overlay", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
            Stretch(go.GetComponent<RectTransform>());

            Image img = go.GetComponent<Image>();
            img.sprite = overlaySprite;
            img.raycastTarget = false;
        }

        float target = DifficultyProgress.Meter;
        shown = lastShown >= 0f ? lastShown : target;
    }

    private void OnDisable()
    {
        lastShown = shown;
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        float target = DifficultyProgress.Meter;

        // Splash more while the level is changing, settle down when it stops
        slosh = Mathf.MoveTowards(slosh, Mathf.Clamp01(Mathf.Abs(target - shown) * 6f), dt * 1.5f);
        shown = Mathf.MoveTowards(shown, target, fillSpeed * dt);
        phase += waveSpeed * dt;

        float amp = waveHeight * (1f + slosh * sloshAmount);

        Color c = shown < 0.5f
            ? Color.Lerp(lowColor, midColor, shown * 2f)
            : Color.Lerp(midColor, highColor, (shown - 0.5f) * 2f);

        // Back wave: lighter, slightly ahead, moving the other way, so the edge looks like it has depth
        Color back = new Color(Mathf.Lerp(c.r, 1f, 0.35f), Mathf.Lerp(c.g, 1f, 0.35f), Mathf.Lerp(c.b, 1f, 0.35f), 0.6f);
        Apply(backWave, Mathf.Min(1f, shown + 0.015f), amp * 0.8f, -phase * 0.7f + 2f, back);
        Apply(frontWave, shown, amp, phase, c);

        if (percentText != null)
            percentText.text = Mathf.RoundToInt(shown * 100f) + "%";
    }

    private void Apply(SidewaysLiquidGraphic g, float level, float amp, float ph, Color c)
    {
        g.level = level;
        g.amplitude = amp;
        g.wavelength = waveLength;
        g.phase = ph;
        g.bottomShade = bottomShade;
        g.fillFromRight = fillFromRight;
        g.color = c;
        g.SetVerticesDirty();
    }

    private SidewaysLiquidGraphic CreateLiquid(string objectName)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer));
        go.transform.SetParent(transform, false);
        Stretch(go.GetComponent<RectTransform>());

        SidewaysLiquidGraphic g = go.AddComponent<SidewaysLiquidGraphic>();
        g.raycastTarget = false;
        return g;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}