using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Liquid-style difficulty meter (sideways). Put this on a UI object in your GAME scene (not the card game scene,
/// so it never shows up there).
///
/// SETUP
///  1. Right-click your Canvas > UI > Image (or just add this component to an empty UI object: it adds the Image).
///  2. Optional: drag your own bar / tube sprite into that Image's "Source Image". With NO sprite it draws a dark
///     rounded bar for you (it stretches without squashing the corners, so you can resize it freely).
///  3. Move / resize / anchor it anywhere with the normal Rect Transform.
///
/// It is visible in the EDITOR too (not only in Play mode): use "Preview Fill" to see how full it looks.
/// The preview objects are hidden and never saved into your scene.
///
/// The liquid is clipped to the shape of your sprite (its opaque pixels), so draw the sprite as the
/// inside of the container. For a glass outline or shine on top of the liquid, put it in "Overlay Sprite".
/// The liquid fills sideways, from the left edge of the object's rectangle (or the right if "Fill From Right" is on).
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(Image), typeof(Mask))]
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

    [Header("Editor Preview")]
    [Tooltip("How full the meter looks in the editor (not playing). In Play mode the real difficulty is used.")]
    [Range(0f, 1f)] public float previewFill = 0.6f;

    [Header("Optional")]
    [Tooltip("Shows the difficulty as a percentage, e.g. 42%.")]
    public TextMeshProUGUI percentText;

    // Remembered between scenes so you SEE the liquid move after a battle
    private static float lastShown = -1f;

    private const string GeneratedPrefix = "DM_"; // generated children, swept and rebuilt (never saved in edit mode)

    private SidewaysLiquidGraphic backWave;
    private SidewaysLiquidGraphic frontWave;
    private Sprite builtOverlay;
    private float shown;
    private float slosh;
    private float phase;

    private static Sprite defaultSprite;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        lastShown = -1f;
    }

    /// <summary>Call from "New Game" so the meter starts empty instead of draining from the old level.</summary>
    public static void ResetShown()
    {
        lastShown = -1f;
    }

    // Called by Unity when the component is first added: a sensible size so it is visible straight away
    private void Reset()
    {
        RectTransform rt = transform as RectTransform;
        if (rt != null && rt.sizeDelta == new Vector2(100f, 100f)) rt.sizeDelta = new Vector2(300f, 36f);
    }

    private void OnEnable()
    {
        BuildVisuals();

        if (Application.isPlaying)
        {
            slosh = 0f;
            shown = lastShown >= 0f ? lastShown : DifficultyProgress.Meter;
        }
        else
        {
            UpdatePreview();
        }
    }

    private void OnDisable()
    {
        if (Application.isPlaying) lastShown = shown;
        DestroyVisuals();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Redraw the preview when a value is changed in the Inspector
        UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
    }
#endif

    // -------------------------------------------------------------------
    // Visuals
    // -------------------------------------------------------------------

    private void BuildVisuals()
    {
        DestroyVisuals();

        // The container sprite doubles as the mask, so the liquid never leaves the shape
        Mask mask = GetComponent<Mask>();
        if (mask == null) mask = gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = true;

        ApplyContainerLook();

        backWave = CreateLiquid(GeneratedPrefix + "LiquidBack");
        frontWave = CreateLiquid(GeneratedPrefix + "LiquidFront");

        if (overlaySprite != null)
        {
            GameObject go = NewChild(GeneratedPrefix + "Overlay", typeof(Image));
            Image img = go.GetComponent<Image>();
            img.sprite = overlaySprite;
            img.raycastTarget = false;
        }

        builtOverlay = overlaySprite;
    }

    private void DestroyVisuals()
    {
        // Also removes anything left over from before a script reload / entering Play mode
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child.name.StartsWith(GeneratedPrefix)) DestroyObject(child.gameObject);
        }

        backWave = null;
        frontWave = null;
    }

    private static void DestroyObject(GameObject go)
    {
        if (Application.isPlaying) Destroy(go);
        else DestroyImmediate(go);
    }

    // No sprite assigned: a dark rounded bar, so the meter can always be seen and resized
    private void ApplyContainerLook()
    {
        Image img = GetComponent<Image>();
        if (img == null) return;

        if (img.sprite == null)
        {
            img.overrideSprite = GetDefaultSprite();
            if (img.type == Image.Type.Simple) img.type = Image.Type.Sliced;
        }
        else
        {
            img.overrideSprite = null;
        }
    }

    private static Sprite GetDefaultSprite()
    {
        if (defaultSprite != null) return defaultSprite;

        const int size = 64;
        const float radius = 16f;
        const int border = 18;

        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.hideFlags = HideFlags.HideAndDontSave;

        Color fill = new Color(0.13f, 0.13f, 0.17f, 1f);
        float half = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Distance to a rounded rectangle, so the corners are smooth
                float qx = Mathf.Abs(x + 0.5f - half) - (half - radius);
                float qy = Mathf.Abs(y + 0.5f - half) - (half - radius);
                float d = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
                Color c = fill;
                c.a = Mathf.Clamp01(0.5f - d);
                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply();

        defaultSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                                      SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        defaultSprite.hideFlags = HideFlags.HideAndDontSave;
        return defaultSprite;
    }

    // -------------------------------------------------------------------
    // Update
    // -------------------------------------------------------------------

    private void Update()
    {
        if (!Application.isPlaying)
        {
            UpdatePreview();
            return;
        }

        float dt = Time.unscaledDeltaTime;
        float target = DifficultyProgress.Meter;

        // Splash more while the level is changing, settle down when it stops
        slosh = Mathf.MoveTowards(slosh, Mathf.Clamp01(Mathf.Abs(target - shown) * 6f), dt * 1.5f);
        shown = Mathf.MoveTowards(shown, target, fillSpeed * dt);
        phase += waveSpeed * dt;

        float amp = waveHeight * (1f + slosh * sloshAmount);
        Refresh(shown, amp, phase);
    }

    // Editor only: a still picture of the meter at "Preview Fill"
    private void UpdatePreview()
    {
        if (backWave == null || frontWave == null || builtOverlay != overlaySprite) BuildVisuals();
        else ApplyContainerLook();

        shown = previewFill;
        Refresh(shown, waveHeight, 0.6f);
    }

    private void Refresh(float level, float amp, float ph)
    {
        if (backWave == null || frontWave == null) return;

        Color c = level < 0.5f
            ? Color.Lerp(lowColor, midColor, level * 2f)
            : Color.Lerp(midColor, highColor, (level - 0.5f) * 2f);

        // Back wave: lighter, slightly ahead, moving the other way, so the edge looks like it has depth
        Color back = new Color(Mathf.Lerp(c.r, 1f, 0.35f), Mathf.Lerp(c.g, 1f, 0.35f), Mathf.Lerp(c.b, 1f, 0.35f), 0.6f);
        Apply(backWave, Mathf.Min(1f, level + 0.015f), amp * 0.8f, -ph * 0.7f + 2f, back);
        Apply(frontWave, level, amp, ph, c);

        if (percentText != null)
            percentText.text = Mathf.RoundToInt(level * 100f) + "%";
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

    // -------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------

    private GameObject NewChild(string objectName, params System.Type[] components)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform));
        // In the editor the preview objects are hidden and never saved into the scene
        go.hideFlags = Application.isPlaying ? HideFlags.None : HideFlags.HideAndDontSave;
        go.transform.SetParent(transform, false);
        Stretch(go.GetComponent<RectTransform>());

        foreach (System.Type t in components)
            go.AddComponent(t);

        return go;
    }

    private SidewaysLiquidGraphic CreateLiquid(string objectName)
    {
        GameObject go = NewChild(objectName, typeof(CanvasRenderer));
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