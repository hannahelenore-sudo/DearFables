using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Vertical stamina bar for thrown items and the Wind Card. Put it on the Player, or let CardShooter add it automatically.
/// - Each throw uses cards from the bar (1 each by default). Max 4 by default.
/// - Not fully used: cards come back after a short delay.
/// - Fully used up: locked until the bar is completely full again.
/// - The Wind Card drains the bar smoothly while you run.
/// The bar is built in code, so no prefab or sprite is needed.
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

    [Header("Bar - Shape")]
    public bool showBar = true;
    [Tooltip("Width of the bar in pixels (at 1920x1080).")]
    public float barWidth = 16f;
    [Tooltip("Height of the bar in pixels (at 1920x1080).")]
    public float barHeight = 90f;
    [Tooltip("Thickness of the dark outline in pixels.")]
    public float borderSize = 2f;
    [Tooltip("Fade the bar out while it's full, like Zelda.")]
    public bool hideWhenFull = true;
    public float fadeSpeed = 5f;

    [Header("Bar - Position")]
    [Tooltip("OPTIONAL: drag any object here (e.g. an empty child of the Character) and the bar sits exactly on it. When empty, the sliders below are used.")]
    public Transform barAnchor;
    [Tooltip("Height on the character. 0 = feet, 0.5 = chest, 1 = top of head. Can go beyond.")]
    [Range(-0.5f, 1.5f)] public float barHeightOnBody = 0.5f;
    [Tooltip("Left/right on the character. -1 = left edge, 0 = middle, 1 = right edge. Can go beyond to float next to the character.")]
    [Range(-2f, 2f)] public float barSide = 1.2f;
    [Tooltip("Extra pixel offset on top of the position above (X = right, Y = up).")]
    public Vector2 screenOffset = Vector2.zero;

    [Header("Bar - Colors (change live while playing)")]
    public Color normalColor = new Color(0.4f, 0.95f, 0.35f, 1f);
    public Color exhaustedColor = new Color(1f, 0.3f, 0.25f, 1f);
    public Color backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.65f);
    public Color dividerColor = new Color(0f, 0f, 0f, 0.85f);
    public Color borderColor = new Color(0f, 0f, 0f, 0.85f);

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
    private RectTransform barRoot;
    private Image borderImage;
    private Image bgImage;
    private Image fillImage;
    private readonly List<Image> dividers = new List<Image>();

    private static Sprite whiteSprite;

    public bool IsExhausted => exhausted;
    public float CardsLeft => value;

    private void Awake()
    {
        value = maxCards;
        displayValue = maxCards;
    }

    private void Start()
    {
        // Measure the character so the bar can be placed on the body
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

        if (showBar) BuildUI();
    }

    // ---------------- Used by CardShooter / WindBoost ----------------

    public bool CanThrow(float cost = 1f)
    {
        return !exhausted && value >= cost - 0.0001f;
    }

    public void Use(float cost = 1f)
    {
        value = Mathf.Max(0f, value - cost);
        sinceUse = 0f;

        // Can't throw another one -> dried up, must refill completely
        if (value < 1f - 0.0001f) exhausted = true;
    }

    /// <summary>
    /// Smoothly takes stamina (Wind Card). Returns false once the bar is empty,
    /// which also locks the bar until it's full again, just like running out of cards.
    /// </summary>
    public bool Drain(float amount)
    {
        if (exhausted || value <= 0f) return false;

        value = Mathf.Max(0f, value - amount);
        sinceUse = 0f;

        if (value <= 0.0001f)
        {
            value = 0f;
            exhausted = true;
            return false;
        }
        return true;
    }

    /// <summary>Call when the player tries to throw but can't (bar shakes).</summary>
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
        if (!showBar || barRoot == null) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 sp = cam.WorldToScreenPoint(GetAnchorWorld(cam));
        bool inFront = sp.z > 0f;

        float scale = canvas.scaleFactor;
        barRoot.position = new Vector2(sp.x, sp.y) + screenOffset * scale;

        // Colours (live, so you can tweak them while playing)
        borderImage.color = borderColor;
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

        // Little punch when you try to throw with no cards
        float punch = flashTimer > 0f ? 1f + 0.25f * (flashTimer / 0.25f) : 1f;
        barRoot.localScale = Vector3.one * punch;

        // Fade out while full
        bool full = value >= maxCards - 0.0001f && displayValue >= maxCards - 0.01f;
        float target = (hideWhenFull && full && flashTimer <= 0f) || !inFront ? 0f : 1f;
        group.alpha = Mathf.MoveTowards(group.alpha, target, fadeSpeed * Time.deltaTime);
    }

    private Vector3 GetAnchorWorld(Camera cam)
    {
        if (barAnchor != null) return barAnchor.position;

        // Left/right follows the screen, so "right" always looks right
        Vector3 right = cam.transform.right;
        right.y = 0f;
        right = right.sqrMagnitude < 0.0001f ? Vector3.right : right.normalized;

        return transform.position
               + bodyCenterOffset
               + Vector3.up * (bodyBottom + bodyHeight * barHeightOnBody)
               + right * (bodyHalfWidth * barSide);
    }

    /// <summary>Right-click the component header > Rebuild Bar after changing Bar Width/Height or Max Cards.</summary>
    [ContextMenu("Rebuild Bar")]
    public void RebuildBar()
    {
        if (canvasGO != null) Destroy(canvasGO);
        dividers.Clear();
        if (showBar && Application.isPlaying) BuildUI();
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

        GameObject rootGO = new GameObject("Bar", typeof(RectTransform));
        rootGO.transform.SetParent(canvasGO.transform, false);
        barRoot = rootGO.GetComponent<RectTransform>();
        barRoot.sizeDelta = new Vector2(barWidth, barHeight);
        barRoot.pivot = new Vector2(0.5f, 0.5f);

        Sprite white = GetWhiteSprite();

        // Dark outline (a bit bigger than the bar)
        borderImage = CreateImage("Border", barRoot, white);
        borderImage.color = borderColor;
        Stretch(borderImage.rectTransform);
        borderImage.rectTransform.offsetMin = new Vector2(-borderSize, -borderSize);
        borderImage.rectTransform.offsetMax = new Vector2(borderSize, borderSize);

        // Dark background
        bgImage = CreateImage("Background", barRoot, white);
        bgImage.color = backgroundColor;
        Stretch(bgImage.rectTransform);

        // The coloured fill, grows from the bottom up
        fillImage = CreateImage("Fill", barRoot, white);
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Vertical;
        fillImage.fillOrigin = (int)Image.OriginVertical.Bottom;
        fillImage.fillAmount = 1f;
        fillImage.color = normalColor;
        Stretch(fillImage.rectTransform);

        // Small dividers so you can see the separate cards
        for (int i = 1; i < maxCards; i++)
        {
            Image div = CreateImage("Divider" + i, barRoot, white);
            div.color = dividerColor;
            RectTransform rt = div.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(0f, 2f);
            rt.anchoredPosition = new Vector2(0f, barHeight * i / maxCards);
            dividers.Add(div);
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

    private static Sprite GetWhiteSprite()
    {
        if (whiteSprite != null) return whiteSprite;

        Texture2D tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        Color[] px = new Color[16];
        for (int i = 0; i < px.Length; i++) px[i] = Color.white;
        tex.SetPixels(px);
        tex.Apply();

        whiteSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f);
        return whiteSprite;
    }

    private void OnDestroy()
    {
        if (canvasGO != null) Destroy(canvasGO);
    }
}