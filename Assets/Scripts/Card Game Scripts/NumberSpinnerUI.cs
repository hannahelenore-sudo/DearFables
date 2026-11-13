using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class NumberSpinnerUI : MonoBehaviour
{
    [Header("UI References")]
    public RectTransform container;
    [Tooltip("The single background image that stays active through BOTH animations and fades when shrinking.")]
    public Image spinnerBackground;
    public TextMeshProUGUI headerText;
    public TextMeshProUGUI spinnerNumberText;

    [Header("Elements to Reveal")]
    public GameObject[] gameplayUIElements;

    [Header("Animation Settings")]
    public float spinDuration = 2.0f;
    public float spinInterval = 0.08f;
    public int minNumber = 1;
    public int maxNumber = 10;

    [Header("Pop & Wiggle Settings")]
    public float popScaleMultiplier = 1.35f;
    public float wiggleAngle = 10f;
    public float wiggleDuration = 0.35f;

    [Header("Background Fade & Corner Settings")]
    public float postSpinPauseDuration = 0.2f;
    public float backgroundFadeOutDuration = 0.3f;
    public Vector2 targetDestinationCorner = new Vector2(400f, 200f);
    public Vector3 targetScale = new Vector3(0.5f, 0.5f, 0.5f);
    public float moveToCornerDuration = 0.6f;

    [Header("Tsinelas Offer (pause after the roll)")]
    [Tooltip("Popup shown after the roll when the player owns Tsinelas. Needs a Button (on this object or assigned below). Keep it OUTSIDE the moving container.")]
    public GameObject tsinelasPopup;
    [Tooltip("The button to tap. If empty, the Button on the popup is used.")]
    public Button tsinelasButton;
    [Tooltip("Optional. Image on the popup that shows the Tsinelas sprite (taken from the trinket you own).")]
    public Image tsinelasIcon;
    [Tooltip("Optional circle Image that drains like a timer (set to Filled / Radial 360 - done automatically).")]
    public Image tsinelasTimerCircle;
    [Tooltip("Optional text that shows the new enemy defence after a reroll.")]
    public TextMeshProUGUI tsinelasResultText;
    public float tsinelasPopupDuration = 5f;
    public float tsinelasPopInDuration = 0.25f;
    [Tooltip("How long the result stays visible after tapping, before the spinner continues.")]
    public float tsinelasResultHoldDuration = 0.8f;
    [Tooltip("How long the popup takes to fade away when the timer runs out.")]
    public float tsinelasFadeOutDuration = 0.25f;

    [Tooltip("Where the auto-built popup sits, Pos X / Pos Y measured from the CENTER of this object (x positive = right, y negative = down). Only used when you don't assign your own popup.")]
    public Vector2 tsinelasPopupPosition = new Vector2(637f, -374f);
    public float tsinelasPopupSize = 220f;
    [Tooltip("How many times Tsinelas can reroll the spinner number per roll.")]
    public int maxTsinelasRerolls = 1;

    private System.Func<int> tsinelasOffer;
    private bool tsinelasClicked;
    private TextMeshProUGUI tsinelasFallbackLabel;
    private TextMeshProUGUI tsinelasHintLabel;
    private static Sprite circleSprite;

    private Coroutine sequenceRoutine;
    private CanvasGroup canvasGroup;
    private Vector3 initialScale = Vector3.one;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        if (container != null) initialScale = container.localScale;
    }

    public void SetNumberLimits(int min, int max)
    {
        minNumber = min;
        maxNumber = Mathf.Max(min, max);
    }

    /// <param name="keepCurrentAlpha">True when the caller is fading this screen in itself (cross-fade): the spin starts without forcing the alpha to 1.</param>
    /// <param name="onTsinelasUsed">Pass this only when the player owns Tsinelas. The spinner pauses after the roll and shows the popup; if tapped, the spinner number is rolled again. The callback is only used to tell the spinner that Tsinelas is owned.</param>
    public void StartSequence(System.Action<int> onComplete, bool keepCurrentAlpha = false, System.Func<int> onTsinelasUsed = null)
    {
        if (sequenceRoutine != null) StopCoroutine(sequenceRoutine);
        gameObject.SetActive(true);

        tsinelasOffer = onTsinelasUsed;
        if (tsinelasOffer != null) EnsureTsinelasPopup(); // builds the popup by itself if you didn't assign one
        if (tsinelasPopup != null) tsinelasPopup.SetActive(false);

        if (canvasGroup != null && !keepCurrentAlpha) canvasGroup.alpha = 1f;
        if (container != null)
        {
            container.localScale = initialScale;
            container.localRotation = Quaternion.identity;
            container.anchoredPosition = Vector2.zero;
        }

        if (spinnerBackground != null)
        {
            spinnerBackground.gameObject.SetActive(true);
            Color c = spinnerBackground.color;
            c.a = 1f;
            spinnerBackground.color = c;
        }

        foreach (GameObject ui in gameplayUIElements)
        {
            if (ui != null) ui.SetActive(false);
        }

        sequenceRoutine = StartCoroutine(RunSpinnerSequence(onComplete));
    }

    private IEnumerator RunSpinnerSequence(System.Action<int> onComplete)
    {
        int finalNumber = minNumber;
        int rerollsUsed = 0;

        while (true)
        {
            float elapsed = 0f;
            float nextSpinTime = 0f;

            while (elapsed < spinDuration)
            {
                elapsed += GameSpeed.DeltaTime;
                if (elapsed >= nextSpinTime)
                {
                    nextSpinTime = elapsed + spinInterval;
                    finalNumber = Random.Range(minNumber, maxNumber + 1);
                    if (spinnerNumberText != null) spinnerNumberText.text = finalNumber.ToString();
                }
                yield return null;
            }

            if (spinnerNumberText != null) spinnerNumberText.text = finalNumber.ToString();

            yield return StartCoroutine(PopAndWiggleRoutine());
            yield return new GameSpeedWait(postSpinPauseDuration);

            // Tsinelas: pause here (before the minimizing). If the player taps it, spin the number again.
            if (tsinelasOffer != null && tsinelasPopup != null && rerollsUsed < maxTsinelasRerolls)
            {
                yield return StartCoroutine(TsinelasOfferRoutine());
                if (tsinelasClicked)
                {
                    rerollsUsed++;
                    continue; // roll the number again
                }
            }
            break;
        }
        tsinelasOffer = null;

        if (container != null)
        {
            Vector2 startPos = container.anchoredPosition;
            Vector3 startScale = container.localScale;
            float moveElapsed = 0f;
            Color startBgColor = (spinnerBackground != null) ? spinnerBackground.color : Color.white;

            while (moveElapsed < moveToCornerDuration)
            {
                moveElapsed += GameSpeed.DeltaTime;
                float t = Mathf.Clamp01(moveElapsed / moveToCornerDuration);
                float smoothT = t * t * (3f - 2f * t);

                container.anchoredPosition = Vector2.Lerp(startPos, targetDestinationCorner, smoothT);
                container.localScale = Vector3.Lerp(startScale, targetScale, smoothT);

                if (spinnerBackground != null)
                {
                    float fadeProgress = Mathf.Clamp01(moveElapsed / backgroundFadeOutDuration);
                    Color c = startBgColor;
                    c.a = Mathf.Lerp(1f, 0f, fadeProgress);
                    spinnerBackground.color = c;
                }
                yield return null;
            }

            container.anchoredPosition = targetDestinationCorner;
            container.localScale = targetScale;
        }

        if (spinnerBackground != null) spinnerBackground.gameObject.SetActive(false);

        foreach (GameObject ui in gameplayUIElements)
        {
            if (ui != null) ui.SetActive(true);
        }

        sequenceRoutine = null;
        onComplete?.Invoke(finalNumber);
    }

    private IEnumerator TsinelasOfferRoutine()
    {
        Button btn = tsinelasButton != null ? tsinelasButton : tsinelasPopup.GetComponent<Button>();
        tsinelasClicked = false;
        if (btn != null) btn.onClick.AddListener(OnTsinelasClicked);

        // Show the Tsinelas sprite from the trinket the player owns
        bool foundSprite = false;
        if (tsinelasIcon != null)
        {
            foreach (TrinketData t in TrinketInventory.Owned)
            {
                if (t != null && t.type == TrinketType.Tsinelas && t.icon != null)
                {
                    tsinelasIcon.sprite = t.icon;
                    tsinelasIcon.enabled = true;
                    foundSprite = true;
                    break;
                }
            }
        }
        // No sprite available (e.g. the default runtime trinket): show the word instead
        if (tsinelasFallbackLabel != null) tsinelasFallbackLabel.gameObject.SetActive(!foundSprite);
        if (tsinelasHintLabel != null) tsinelasHintLabel.gameObject.SetActive(true);

        // CanvasGroup so the popup can fade away on timeout
        CanvasGroup popGroup = tsinelasPopup.GetComponent<CanvasGroup>();
        if (popGroup == null) popGroup = tsinelasPopup.AddComponent<CanvasGroup>();
        popGroup.alpha = 1f;

        if (tsinelasResultText != null) tsinelasResultText.gameObject.SetActive(false);
        if (tsinelasTimerCircle != null)
        {
            tsinelasTimerCircle.type = Image.Type.Filled;
            tsinelasTimerCircle.fillMethod = Image.FillMethod.Radial360;
            tsinelasTimerCircle.fillAmount = 1f;
        }

        // Pop in with a little overshoot
        Transform popT = tsinelasPopup.transform;
        Vector3 baseScale = popT.localScale;
        tsinelasPopup.SetActive(true);
        popT.localScale = Vector3.zero;

        float elapsed = 0f;
        while (elapsed < tsinelasPopInDuration)
        {
            elapsed += GameSpeed.DeltaTime;
            float t = Mathf.Clamp01(elapsed / tsinelasPopInDuration);
            float overshoot = 1f + 1.70158f * Mathf.Pow(t - 1f, 3f) + 1.70158f * Mathf.Pow(t - 1f, 2f); // easeOutBack
            popT.localScale = baseScale * overshoot;
            yield return null;
        }
        popT.localScale = baseScale;

        // Wait for a tap or the timer running out
        float timeLeft = tsinelasPopupDuration;
        while (timeLeft > 0f && !tsinelasClicked)
        {
            timeLeft -= GameSpeed.DeltaTime;
            if (tsinelasTimerCircle != null)
                tsinelasTimerCircle.fillAmount = Mathf.Clamp01(timeLeft / tsinelasPopupDuration);
            yield return null;
        }

        if (btn != null) btn.onClick.RemoveListener(OnTsinelasClicked);

        if (tsinelasClicked)
        {
            // Tapped: don't wait for anything. The popup shrinks away in the background so the spin starts right now.
            if (tsinelasHintLabel != null) tsinelasHintLabel.gameObject.SetActive(false);
            StartCoroutine(TsinelasDismissRoutine(popT, popGroup, baseScale, true));
            yield break;
        }

        // Timed out: fade away, then continue with the normal minimizing.
        yield return StartCoroutine(TsinelasDismissRoutine(popT, popGroup, baseScale, false));
    }

    private IEnumerator TsinelasDismissRoutine(Transform popT, CanvasGroup popGroup, Vector3 baseScale, bool shrink)
    {
        float elapsed = 0f;
        float outDuration = Mathf.Max(0.01f, tsinelasFadeOutDuration);
        while (elapsed < outDuration)
        {
            elapsed += GameSpeed.DeltaTime;
            float p = Mathf.Clamp01(elapsed / outDuration);
            p = p * p * (3f - 2f * p);
            if (shrink) popT.localScale = Vector3.Lerp(baseScale, Vector3.zero, p);
            else popGroup.alpha = 1f - p;
            yield return null;
        }
        tsinelasPopup.SetActive(false);
        popT.localScale = baseScale;
        popGroup.alpha = 1f;
    }

    private void OnTsinelasClicked()
    {
        tsinelasClicked = true;
    }

    // -------------------------------------------------------------------
    // Auto-built Tsinelas popup (only when you didn't assign your own)
    // -------------------------------------------------------------------

    private void EnsureTsinelasPopup()
    {
        if (tsinelasPopup != null) return;

        Sprite circle = GetCircleSprite();
        float size = tsinelasPopupSize;

        // Root: the clickable circle, sibling of the container so it doesn't move with the spinner
        GameObject root = new GameObject("TsinelasPopup", typeof(RectTransform));
        root.transform.SetParent(transform, false);
        RectTransform rootRt = (RectTransform)root.transform;
        rootRt.anchorMin = rootRt.anchorMax = new Vector2(0.5f, 0.5f); // center
        rootRt.pivot = new Vector2(0.5f, 0.5f);
        rootRt.anchoredPosition = tsinelasPopupPosition;
        rootRt.sizeDelta = new Vector2(size, size);
        root.transform.SetAsLastSibling();

        // Timer ring (drains)
        Image ring = NewImage("TimerRing", rootRt, circle, new Color(1f, 0.85f, 0.29f, 1f));
        Stretch(ring.rectTransform, 0f);
        ring.raycastTarget = false;

        // Dark inner disc (this is also the button's graphic)
        Image disc = NewImage("Disc", rootRt, circle, new Color(0.12f, 0.1f, 0.16f, 0.95f));
        Stretch(disc.rectTransform, size * 0.06f);

        // Tsinelas sprite
        Image icon = NewImage("Icon", rootRt, null, Color.white);
        Stretch(icon.rectTransform, size * 0.2f);
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        icon.enabled = false;

        // Word shown when there's no sprite
        tsinelasFallbackLabel = NewText("Fallback", rootRt, "TSINELAS", size * 0.16f, Color.white);
        Stretch(tsinelasFallbackLabel.rectTransform, size * 0.1f);

        // "Tap to reroll!" above the circle
        tsinelasHintLabel = NewText("Hint", rootRt, "Tap to reroll!", size * 0.13f, new Color(1f, 0.85f, 0.29f, 1f));
        tsinelasHintLabel.rectTransform.anchorMin = tsinelasHintLabel.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        tsinelasHintLabel.rectTransform.pivot = new Vector2(0.5f, 0f);
        tsinelasHintLabel.rectTransform.anchoredPosition = new Vector2(0f, 8f);
        tsinelasHintLabel.rectTransform.sizeDelta = new Vector2(size * 1.6f, size * 0.25f);

        // New enemy defence after a tap
        TextMeshProUGUI result = NewText("Result", rootRt, "", size * 0.13f, Color.white);
        result.rectTransform.anchorMin = result.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        result.rectTransform.pivot = new Vector2(0.5f, 0f);
        result.rectTransform.anchoredPosition = new Vector2(0f, 8f);
        result.rectTransform.sizeDelta = new Vector2(size * 1.8f, size * 0.25f);
        result.gameObject.SetActive(false);

        Button btn = root.AddComponent<Button>();
        btn.targetGraphic = disc;
        btn.transition = Selectable.Transition.ColorTint;

        tsinelasPopup = root;
        tsinelasButton = btn;
        tsinelasIcon = icon;
        tsinelasTimerCircle = ring;
        tsinelasResultText = result;

        root.SetActive(false);
    }

    private static Image NewImage(string name, RectTransform parent, Sprite sprite, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        return img;
    }

    private static TextMeshProUGUI NewText(string name, RectTransform parent, string text, float fontSize, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI t = go.GetComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = fontSize;
        t.color = color;
        t.fontStyle = FontStyles.Bold;
        t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false;
        return t;
    }

    private static void Stretch(RectTransform rt, float inset)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
    }

    private static Sprite GetCircleSprite()
    {
        if (circleSprite != null) return circleSprite;

        const int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.hideFlags = HideFlags.HideAndDontSave;

        float r = size * 0.5f;
        Color32[] px = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                float a = Mathf.Clamp01(r - d); // 1px soft edge
                px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        }
        tex.SetPixels32(px);
        tex.Apply();

        circleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        circleSprite.hideFlags = HideFlags.HideAndDontSave;
        return circleSprite;
    }

    private IEnumerator PopAndWiggleRoutine()
    {
        if (container == null) yield break;
        Vector3 baseScale = initialScale;
        Vector3 maxPop = baseScale * popScaleMultiplier;

        float elapsed = 0f;
        while (elapsed < 0.1f)
        {
            elapsed += GameSpeed.DeltaTime;
            container.localScale = Vector3.Lerp(baseScale, maxPop, elapsed / 0.1f);
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < wiggleDuration)
        {
            elapsed += GameSpeed.DeltaTime;
            float progress = Mathf.Clamp01(elapsed / wiggleDuration);
            float currentWiggle = Mathf.Sin(progress * Mathf.PI * 8f) * wiggleAngle * (1f - progress);

            container.localRotation = Quaternion.Euler(0, 0, currentWiggle);
            container.localScale = Vector3.Lerp(maxPop, baseScale, progress);
            yield return null;
        }
        container.localRotation = Quaternion.identity;
        container.localScale = baseScale;
    }
}