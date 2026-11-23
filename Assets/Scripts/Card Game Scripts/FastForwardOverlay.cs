using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class FastForwardOverlay : MonoBehaviour
{
    private const string CanvasName = "FastForwardCanvas";
    private const string ButtonName = "FastForwardButton";

    [Header("Size & Position (all live)")]
    [Tooltip("Base size of the button in canvas units (canvas is 1920x1080).")]
    public Vector2 size = new Vector2(110f, 60f);
    [Tooltip("Multiplies the button size and the text size. Drag this to scale everything at once. 1 = normal.")]
    [Range(0.1f, 5f)] public float sizeMultiplier = 1f;
    [Tooltip("Which corner the button sticks to. (1,1) = top-right, (0,1) = top-left, (1,0) = bottom-right, (0,0) = bottom-left. Values in between work too.")]
    public Vector2 anchor = new Vector2(1f, 1f);
    [Tooltip("Distance from that corner. Positive moves the button further INTO the screen.")]
    public Vector2 margin = new Vector2(30f, 30f);
    [Tooltip("Higher than every other canvas in the scene so it is always visible and clickable.")]
    public int sortingOrder = 999;

    [Header("Container Sprite")]
    [Tooltip("Optional. Your own picture for the button container. Empty = plain colored box.")]
    public Sprite containerSprite;
    [Tooltip("ON (and a sprite is set): the button takes the sprite's own pixel size instead of 'Size' above.")]
    public bool matchSpriteSize = false;
    [Tooltip("Keep the sprite's proportions instead of stretching it to the button size.")]
    public bool preserveAspect = true;

    [Header("Hotkey")]
    public bool useHotkey = true;
    public Key hotkey = Key.Q;

    [Header("Look")]
    [Tooltip("Tint of the plain box. When a Container Sprite is set, the sprite is shown in its normal colors instead.")]
    public Color backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.75f);
    public Color activeTint = new Color(1f, 0.85f, 0.3f, 1f);
    public float fontSize = 32f;

    [Header("Speeds / Sound")]
    public float[] speeds = { 1f, 2f, 4f };
    public AudioClip clickSound;
    [Range(0f, 1f)] public float clickVolume = 1f;

    [Header("Testing")]
    [Tooltip("ON: values above are re-applied every frame while playing, so you can tweak them live.")]
    public bool liveAdjust = true;

    private Canvas canvas;
    private RectTransform rt;
    private Image img;
    private TextMeshProUGUI label;
    private FastForwardButton ff;
    private Color appliedNormal = new Color(-1f, -1f, -1f, -1f);

    private void Awake()
    {
        if (!FindExisting()) Build();
        Apply();
    }

    private void Update()
    {
        if (liveAdjust) Apply();
    }

    private void OnValidate()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorApplication.delayCall += () =>
            {
                if (this != null && FindExisting()) Apply();
            };
            return;
        }
#endif
        if (rt != null) Apply();
    }

    private bool FindExisting()
    {
        Transform c = transform.Find(CanvasName);
        if (c == null) return false;
        Transform b = c.Find(ButtonName);
        if (b == null) return false;

        canvas = c.GetComponent<Canvas>();
        rt = b as RectTransform;
        img = b.GetComponent<Image>();
        ff = b.GetComponent<FastForwardButton>();
        label = b.GetComponentInChildren<TextMeshProUGUI>();
        return canvas != null && rt != null && img != null && label != null && ff != null;
    }

    private void Build()
    {
        RemoveExisting();

        GameObject canvasGO = new GameObject(CanvasName);
        canvasGO.transform.SetParent(transform, false);
        canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        GameObject btnGO = new GameObject(ButtonName, typeof(RectTransform));
        btnGO.SetActive(false);
        btnGO.transform.SetParent(canvasGO.transform, false);
        rt = (RectTransform)btnGO.transform;

        img = btnGO.AddComponent<Image>();
        Button btn = btnGO.AddComponent<Button>();
        btn.targetGraphic = img;

        GameObject textGO = new GameObject("Label", typeof(RectTransform));
        textGO.transform.SetParent(btnGO.transform, false);
        RectTransform trt = (RectTransform)textGO.transform;
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;
        label = textGO.AddComponent<TextMeshProUGUI>();
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.raycastTarget = false;
        label.text = ">>";

        btnGO.AddComponent<InteractiveUIButton>().enableWiggleOnUnsupportedClick = false;

        ff = btnGO.AddComponent<FastForwardButton>();
        ff.label = label;
        btnGO.SetActive(true);

#if UNITY_EDITOR
        if (!Application.isPlaying)
            Undo.RegisterCreatedObjectUndo(canvasGO, "Build Fast Forward Preview");
#endif
    }

    private void RemoveExisting()
    {
        Transform old = transform.Find(CanvasName);
        if (old == null) return;
        if (Application.isPlaying) Destroy(old.gameObject);
        else DestroyImmediate(old.gameObject);
    }

    private void Apply()
    {
        if (canvas == null || rt == null || img == null || label == null) return;

        canvas.sortingOrder = sortingOrder;

        Vector2 a = new Vector2(Mathf.Clamp01(anchor.x), Mathf.Clamp01(anchor.y));
        rt.anchorMin = rt.anchorMax = rt.pivot = a;
        rt.anchoredPosition = new Vector2(a.x >= 0.5f ? -margin.x : margin.x,
                                          a.y >= 0.5f ? -margin.y : margin.y);

        bool hasSprite = containerSprite != null;
        Vector2 baseSize = (hasSprite && matchSpriteSize) ? containerSprite.rect.size : size;
        rt.sizeDelta = baseSize * sizeMultiplier;
        label.fontSize = fontSize * sizeMultiplier;

        if (img.sprite != containerSprite) img.sprite = containerSprite;
        img.preserveAspect = preserveAspect;
        img.type = (hasSprite && containerSprite.border != Vector4.zero) ? Image.Type.Sliced : Image.Type.Simple;

        Color normal = hasSprite ? Color.white : backgroundColor;
        if (Application.isPlaying && ff != null)
        {
            if (normal != appliedNormal)
            {
                appliedNormal = normal;
                ff.SetNormalColor(normal);
            }
        }
        else
        {
            img.color = normal;
            if (!Application.isPlaying) label.text = ">>";
        }

        if (ff != null)
        {
            ff.speeds = speeds;
            ff.activeTint = activeTint;
            ff.useHotkey = useHotkey;
            ff.hotkey = hotkey;
            ff.clickSound = clickSound;
            ff.clickVolume = clickVolume;
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorUtility.SetDirty(canvas);
            EditorUtility.SetDirty(rt);
            EditorUtility.SetDirty(img);
            EditorUtility.SetDirty(label);
        }
#endif
    }

    [ContextMenu("Build Preview In Editor")]
    private void BuildPreview()
    {
        if (Application.isPlaying) return;
        Build();
        Apply();
    }

    [ContextMenu("Remove Preview")]
    private void RemovePreview()
    {
        if (Application.isPlaying) return;
        RemoveExisting();
        canvas = null; rt = null; img = null; label = null; ff = null;
    }
}