using TMPro;
using UnityEngine;
using UnityEngine.UI;

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

    private static float lastShown = -1f;

    private const string GeneratedPrefix = "DM_";

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
        UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
    }
#endif

    private void BuildVisuals()
    {
        DestroyVisuals();

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

    private void Update()
    {
        if (!Application.isPlaying)
        {
            UpdatePreview();
            return;
        }

        float dt = Time.unscaledDeltaTime;
        float target = DifficultyProgress.Meter;

        slosh = Mathf.MoveTowards(slosh, Mathf.Clamp01(Mathf.Abs(target - shown) * 6f), dt * 1.5f);
        shown = Mathf.MoveTowards(shown, target, fillSpeed * dt);
        phase += waveSpeed * dt;

        float amp = waveHeight * (1f + slosh * sloshAmount);
        Refresh(shown, amp, phase);
    }

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

    private GameObject NewChild(string objectName, params System.Type[] components)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform));
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