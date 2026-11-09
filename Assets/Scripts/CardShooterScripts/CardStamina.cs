using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Zelda/Genshin-style stamina wheel for thrown cards. Put it on the Player, or let CardShooter add it automatically.
/// - Each throw uses 1 card. Max 4 by default.
/// - Not fully used: cards come back after a short delay.
/// - Fully used up: locked until the wheel is completely full again.
/// The wheel is built in code, so no prefab or sprite is needed.
/// </summary>
public class CardStamina : MonoBehaviour
{
    [Header("Cards")]
    public int maxCards = 4;
    [Tooltip("Seconds after a throw before cards start coming back.")]
    public float regenDelay = 0.8f;
    [Tooltip("Seconds to get one card back (normal).")]
    public float secondsPerCard = 1f;
    [Tooltip("Seconds per card when you ran completely dry (slower = a real penalty).")]
    public float exhaustedSecondsPerCard = 1.5f;
    public float exhaustedRegenDelay = 0.5f;

    [Header("Wheel - Shape")]
    public bool showWheel = true;
    [Tooltip("ON = half circle, OFF = full circle. (Right-click the component > Rebuild Wheel after changing.)")]
    public bool halfCircle = true;
    [Tooltip("Width of the wheel in pixels (at 1920x1080). A half circle is this wide and half as tall.")]
    public float wheelSize = 60f;
    [Tooltip("Turns the wheel around its centre point, in degrees. 0 = arc opens downward (a rainbow shape), 90 = arc faces right, 180 = bowl shape, -90 = arc faces left.")]
    [Range(-180f, 180f)] public float rotation = 0f;
    [Tooltip("Fade the wheel out while it's full, like Zelda.")]
    public bool hideWhenFull = true;
    public float fadeSpeed = 5f;

    [Header("Wheel - Position")]
    [Tooltip("OPTIONAL: drag any object here (e.g. an empty child of the Character) and the wheel sits exactly on it. Move that object in the Scene view to place the wheel anywhere. When empty, the sliders below are used.")]
    public Transform wheelAnchor;
    [Tooltip("Height on the character. 0 = feet, 0.5 = chest, 1 = top of head. Can go beyond.")]
    [Range(-0.5f, 1.5f)] public float wheelHeight = 0.5f;
    [Tooltip("Left/right on the character. -1 = left edge, 0 = middle, 1 = right edge. Can go beyond to float next to the character.")]
    [Range(-2f, 2f)] public float wheelSide = 1.2f;
    [Tooltip("Extra pixel offset on top of the position above (X = right, Y = up).")]
    public Vector2 screenOffset = Vector2.zero;

    [Header("Wheel - Colors (change live while playing)")]
    public Color normalColor = new Color(0.4f, 0.95f, 0.35f, 1f);
    public Color exhaustedColor = new Color(1f, 0.3f, 0.25f, 1f);
    public Color backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.65f);
    public Color dividerColor = new Color(0f, 0f, 0f, 0.85f);

    private const float InnerRatio = 0.62f;

    private float value;
    private float displayValue;
    private bool exhausted;
    private float sinceUse = 999f;
    private float flashTimer;

    // Measured from the character's sprites
    private float bodyBottom = 0f;
    private float bodyHeight = 2f;
    private float bodyHalfWidth = 0.5f;
    private Vector3 bodyCenterOffset = Vector3.zero;

    private GameObject canvasGO;
    private Canvas canvas;
    private CanvasGroup group;
    private RectTransform wheelRoot;
    private Image bgImage;
    private Image fillImage;
    private readonly List<Image> dividers = new List<Image>();

    private static Sprite ringSprite;
    private static Sprite halfRingSprite;

    public bool IsExhausted => exhausted;
    public float CardsLeft => value;

    private void Awake()
    {
        value = maxCards;
        displayValue = maxCards;
    }

    private void Start()
    {
        // Measure the character so the wheel can be placed on the body
        Bounds b = new Bounds(transform.position, Vector3.zero);
        bool any = false;
        foreach (SpriteRenderer sr in GetComponentsInChildren<SpriteRenderer>())
        {
            if (!any) { b = sr.bounds; any = true; }
            else b.Encapsulate(sr.bounds);
        }
        if (any)
        {
            bodyBottom = b.min.y - transform.position.y;
            bodyHeight = Mathf.Max(0.5f, b.size.y);
            bodyHalfWidth = Mathf.Max(0.2f, b.size.x * 0.5f);
            bodyCenterOffset = b.center - transform.position;
            bodyCenterOffset.y = 0f;
        }

        if (showWheel) BuildUI();
    }

    // ---------------- Used by CardShooter ----------------

    public bool CanThrow()
    {
        return !exhausted && value >= 1f - 0.0001f;
    }

    public void Use()
    {
        value = Mathf.Max(0f, value - 1f);
        sinceUse = 0f;

        // Can't throw another one -> dried up, must refill completely
        if (value < 1f - 0.0001f) exhausted = true;
    }

    /// <summary>Call when the player tries to throw but can't (wheel shakes).</summary>
    public void NotifyDenied()
    {
        flashTimer = 0.25f;
    }

    // ---------------- Logic ----------------

    private void Update()
    {
        sinceUse += Time.deltaTime;

        float delay = exhausted ? exhaustedRegenDelay : regenDelay;
        if (value < maxCards && sinceUse >= delay)
        {
            float spc = exhausted ? exhaustedSecondsPerCard : secondsPerCard;
            value = Mathf.Min(maxCards, value + Time.deltaTime / Mathf.Max(0.01f, spc));
            if (value >= maxCards - 0.0001f)
            {
                value = maxCards;
                exhausted = false;
            }
        }

        if (flashTimer > 0f) flashTimer -= Time.deltaTime;

        displayValue = Mathf.Lerp(displayValue, value, 1f - Mathf.Exp(-15f * Time.deltaTime));
    }

    // ---------------- UI ----------------

    private void LateUpdate()
    {
        if (!showWheel || wheelRoot == null) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 sp = cam.WorldToScreenPoint(GetAnchorWorld(cam));
        bool inFront = sp.z > 0f;

        float scale = canvas.scaleFactor;
        wheelRoot.position = new Vector2(sp.x, sp.y) + screenOffset * scale;

        // Colours (live, so you can tweak them while playing)
        bgImage.color = backgroundColor;
        foreach (Image d in dividers) d.color = dividerColor;

        fillImage.fillAmount = Mathf.Clamp01(displayValue / maxCards);
        if (exhausted)
        {
            float pulse = 0.65f + 0.35f * Mathf.Sin(Time.time * 10f);
            fillImage.color = Color.Lerp(exhaustedColor * 0.6f, exhaustedColor, pulse);
        }
        else
        {
            fillImage.color = normalColor;
        }

        // Rotation + little punch when you try to throw with no cards
        wheelRoot.localRotation = Quaternion.Euler(0f, 0f, -rotation);
        float punch = flashTimer > 0f ? 1f + 0.25f * (flashTimer / 0.25f) : 1f;
        wheelRoot.localScale = Vector3.one * punch;

        // Fade out while full
        bool full = value >= maxCards - 0.0001f && displayValue >= maxCards - 0.01f;
        float target = (hideWhenFull && full && flashTimer <= 0f) || !inFront ? 0f : 1f;
        group.alpha = Mathf.MoveTowards(group.alpha, target, fadeSpeed * Time.deltaTime);
    }

    private Vector3 GetAnchorWorld(Camera cam)
    {
        if (wheelAnchor != null) return wheelAnchor.position;

        // Left/right follows the screen, so "right" always looks right
        Vector3 right = cam.transform.right;
        right.y = 0f;
        right = right.sqrMagnitude < 0.0001f ? Vector3.right : right.normalized;

        return transform.position
               + bodyCenterOffset
               + Vector3.up * (bodyBottom + bodyHeight * wheelHeight)
               + right * (bodyHalfWidth * wheelSide);
    }

    /// <summary>Right-click the component header > Rebuild Wheel after changing Half Circle, Wheel Size or Max Cards.</summary>
    [ContextMenu("Rebuild Wheel")]
    public void RebuildWheel()
    {
        if (canvasGO != null) Destroy(canvasGO);
        dividers.Clear();
        if (showWheel && Application.isPlaying) BuildUI();
    }

    private void BuildUI()
    {
        canvasGO = new GameObject("CardStaminaUI");
        canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        group = canvasGO.AddComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;
        group.alpha = 0f;

        // For a half circle the pivot is the bottom-centre = the centre of the arc,
        // so positioning and rotation both happen around the arc's centre.
        GameObject rootGO = new GameObject("Wheel", typeof(RectTransform));
        rootGO.transform.SetParent(canvasGO.transform, false);
        wheelRoot = rootGO.GetComponent<RectTransform>();
        wheelRoot.sizeDelta = halfCircle ? new Vector2(wheelSize, wheelSize * 0.5f) : new Vector2(wheelSize, wheelSize);
        wheelRoot.pivot = halfCircle ? new Vector2(0.5f, 0f) : new Vector2(0.5f, 0.5f);

        Sprite sprite = halfCircle ? GetHalfRingSprite() : GetRingSprite();

        // Dark ring behind
        bgImage = CreateImage("Background", wheelRoot, sprite);
        bgImage.color = backgroundColor;
        Stretch(bgImage.rectTransform);

        // The coloured fill
        fillImage = CreateImage("Fill", wheelRoot, sprite);
        fillImage.type = Image.Type.Filled;
        if (halfCircle)
        {
            fillImage.fillMethod = Image.FillMethod.Radial180;
            fillImage.fillOrigin = (int)Image.Origin180.Bottom;
        }
        else
        {
            fillImage.fillMethod = Image.FillMethod.Radial360;
            fillImage.fillOrigin = (int)Image.Origin360.Top;
        }
        fillImage.fillClockwise = true;
        fillImage.fillAmount = 1f;
        fillImage.color = normalColor;
        Stretch(fillImage.rectTransform);

        // Small dividers so you can see the separate cards
        if (maxCards > 1)
        {
            float outerR = wheelSize * 0.5f;
            float innerR = outerR * InnerRatio;
            float midR = (outerR + innerR) * 0.5f;
            float length = (outerR - innerR) + 2f;

            for (int i = 0; i < maxCards; i++)
            {
                float angle; // degrees clockwise from straight up
                if (halfCircle)
                {
                    if (i == 0) continue; // the flat ends don't need a divider
                    angle = -90f + 180f / maxCards * i;
                }
                else
                {
                    angle = 360f / maxCards * i;
                }
                float rad = angle * Mathf.Deg2Rad;

                Image div = CreateImage("Divider" + i, wheelRoot, null);
                div.color = dividerColor;
                RectTransform rt = div.rectTransform;
                Vector2 anchor = halfCircle ? new Vector2(0.5f, 0f) : new Vector2(0.5f, 0.5f);
                rt.anchorMin = rt.anchorMax = anchor;
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(2f, length);
                rt.anchoredPosition = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad)) * midR;
                rt.localRotation = Quaternion.Euler(0f, 0f, -angle);
                dividers.Add(div);
            }
        }
    }

    private static Image CreateImage(string name, Transform parent, Sprite sprite)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        Image img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        return img;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    // Generates smooth ring textures so no image asset is needed
    private static Sprite GetRingSprite()
    {
        if (ringSprite != null) return ringSprite;

        const int size = 256;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        float c = (size - 1) * 0.5f;
        float outerR = size * 0.5f - 1f;
        float innerR = outerR * InnerRatio;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c));
                float a = Mathf.Clamp01(outerR - d + 0.5f) * Mathf.Clamp01(d - innerR + 0.5f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();

        ringSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        return ringSprite;
    }

    // Upper half of a ring: the arc's centre is the bottom-middle of the texture
    private static Sprite GetHalfRingSprite()
    {
        if (halfRingSprite != null) return halfRingSprite;

        const int w = 256;
        const int h = 128;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Vector2 center = new Vector2((w - 1) * 0.5f, 0f);
        float outerR = w * 0.5f - 1f;
        float innerR = outerR * InnerRatio;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), center);
                float a = Mathf.Clamp01(outerR - d + 0.5f) * Mathf.Clamp01(d - innerR + 0.5f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();

        halfRingSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        return halfRingSprite;
    }

    private void OnDestroy()
    {
        if (canvasGO != null) Destroy(canvasGO);
    }
}