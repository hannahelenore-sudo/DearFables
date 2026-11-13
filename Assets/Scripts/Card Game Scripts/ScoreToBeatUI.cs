using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ScoreToBeatUI : MonoBehaviour
{
    [Header("UI References")]
    public RectTransform container;
    public TextMeshProUGUI headerText;
    public TextMeshProUGUI scoreText;

    [Header("Elements to Hide")]
    public GameObject[] gameplayUIElements;

    [Header("Advantage Hit (card hit on the enemy)")]
    [Tooltip("Optional. The 'ADVANTAGE HIT!' text. If empty, one is made automatically from the Header Text.")]
    public TextMeshProUGUI advantageText;
    [Tooltip("Optional. The big '-10%' text. If empty, one is made automatically from the Header Text.")]
    public TextMeshProUGUI advantagePercentText;
    public Color advantageColor = new Color(1f, 0.85f, 0.25f);
    [Tooltip("Colour of the score while it drops.")]
    public Color reductionColor = new Color(0.45f, 1f, 0.45f);
    [Tooltip("The colour the screen is tinted with while the advantage text shows.")]
    public Color dimTint = new Color(0.04f, 0.14f, 0.42f, 1f);
    [Range(0f, 1f)]
    [Tooltip("How dark the dim gets (0.9 = 90%).")]
    public float dimAmount = 0.9f;
    [Tooltip("How much bigger than the header the '-10%' text is.")]
    public float percentTextScale = 2f;
    [Range(0f, 1f)]
    [Tooltip("How visible the Score To Beat text stays while the screen is dimmed (0.08 = 8%).")]
    public float scoreDimmedAlpha = 0.08f;
    public float advantageDelay = 0.3f;
    [Tooltip("Time the screen takes to dim down.")]
    public float dimFadeInDuration = 0.45f;
    [Tooltip("How long the '-10%' stays on screen before it fades away.")]
    public float advantageHoldDuration = 1.0f;
    [Tooltip("Time the dim and the text take to fade away (the score starts dropping at the same time).")]
    public float advantageFadeOutDuration = 0.5f;
    [Tooltip("Time the score takes to drop to the reduced number.")]
    public float advantageCountDuration = 0.9f;
    public AudioClip advantageSound;
    [Range(0f, 1f)] public float advantageVolume = 1f;

    [Header("Animation Settings")]
    public float reelDuration = 2.0f;
    public float popScaleMultiplier = 1.35f;
    public float wiggleAngle = 10f;
    public float wiggleDuration = 0.35f;
    public float postAnimationPause = 1.0f;

    [Header("Finish")]
    [Tooltip("OFF (recommended): the sequence leaves the Score To Beat on screen and the caller fades it out, so it can cross-fade into the next screen. ON: it hides itself instantly when done.")]
    public bool hideWhenDone = false;

    private Coroutine currentRoutine;
    private CanvasGroup canvasGroup;
    private Image dimOverlay;

    void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    /// <param name="originalScore">If bigger than targetScore, the Advantage Hit animation plays: the score reels up to this number, the screen dims with the -10% text, then the score drops to targetScore.</param>
    public void StartSequence(int targetScore, System.Action onComplete = null, int originalScore = 0)
    {
        if (currentRoutine != null) StopCoroutine(currentRoutine);
        gameObject.SetActive(true);

        foreach (GameObject ui in gameplayUIElements)
        {
            if (ui != null) ui.SetActive(false);
        }

        ResetAdvantageVisuals();

        currentRoutine = StartCoroutine(PlaySequenceRoutine(targetScore, onComplete, originalScore));
    }

    private void ResetAdvantageVisuals()
    {
        if (advantageText != null) advantageText.gameObject.SetActive(false);
        if (advantagePercentText != null) advantagePercentText.gameObject.SetActive(false);
        if (dimOverlay != null) dimOverlay.gameObject.SetActive(false);
        if (scoreText != null)
        {
            scoreText.rectTransform.localRotation = Quaternion.identity;
            scoreText.rectTransform.localScale = Vector3.one;
        }
    }

    private IEnumerator PlaySequenceRoutine(int targetScore, System.Action onComplete, int originalScore)
    {
        bool advantage = originalScore > targetScore;
        int reelTarget = advantage ? originalScore : targetScore; // with the advantage it reels up to the full score first

        if (canvasGroup != null) canvasGroup.alpha = 1f;

        if (headerText != null) headerText.text = "SCORE TO BEAT"; // Keep the header text during the animation
        if (scoreText != null) scoreText.text = "1";

        float elapsed = 0f;
        while (elapsed < reelDuration)
        {
            elapsed += GameSpeed.DeltaTime;
            float progress = Mathf.Clamp01(elapsed / reelDuration);

            if (scoreText != null)
            {
                int currentVal = Mathf.RoundToInt(Mathf.Lerp(1, reelTarget, progress));
                scoreText.text = currentVal.ToString();
            }
            yield return null;
        }

        if (scoreText != null) scoreText.text = reelTarget.ToString();

        yield return StartCoroutine(PopAndWiggleRoutine());

        if (advantage)
            yield return StartCoroutine(AdvantageRoutine(originalScore, targetScore));

        yield return new GameSpeedWait(postAnimationPause);

        if (hideWhenDone)
        {
            if (canvasGroup != null) canvasGroup.alpha = 0f;
            gameObject.SetActive(false);
        }
        currentRoutine = null;
        onComplete?.Invoke();
    }

    // The screen dims down (blue tint), "ADVANTAGE HIT!  -10%" pops in and holds,
    // then the dim and the text fade away while the score drops from the full number to the reduced one.
    private IEnumerator AdvantageRoutine(int from, int to)
    {
        yield return new GameSpeedWait(advantageDelay);

        Image dim = GetDimOverlay();
        TextMeshProUGUI label = GetAdvantageText();
        TextMeshProUGUI percentLabel = GetPercentText(label);
        int percent = Mathf.Max(1, Mathf.RoundToInt((1f - to / (float)from) * 100f));

        Color headerBase = headerText != null ? headerText.color : Color.white;
        Color scoreBase = scoreText != null ? scoreText.color : Color.white;

        if (advantageSound != null) GameSFX.Play(advantageSound, advantageVolume, 1f);

        // 1. Dim down with the blue tint
        if (dim != null)
        {
            dim.gameObject.SetActive(true);
            SetDimAlpha(dim, 0f);

            float e = 0f;
            float dur = Mathf.Max(0.01f, dimFadeInDuration);
            while (e < dur)
            {
                e += GameSpeed.DeltaTime;
                float s = Smooth(e / dur);
                SetDimAlpha(dim, dimAmount * s);
                SetScoreAreaAlpha(headerBase, scoreBase, Mathf.Lerp(1f, scoreDimmedAlpha, s)); // the Score To Beat text dims with the screen
                yield return null;
            }
            SetDimAlpha(dim, dimAmount);
            SetScoreAreaAlpha(headerBase, scoreBase, scoreDimmedAlpha);
        }

        // 2. "ADVANTAGE HIT!" then the big "-10%" pop in
        if (label != null)
        {
            label.text = "ADVANTAGE HIT!";
            label.color = advantageColor;
            label.gameObject.SetActive(true);
            yield return StartCoroutine(PopIn(label.rectTransform, 0.28f));
        }

        yield return new GameSpeedWait(0.1f);

        if (percentLabel != null)
        {
            percentLabel.text = "-" + percent + "%";
            percentLabel.color = advantageColor;
            percentLabel.gameObject.SetActive(true);
            yield return StartCoroutine(PopIn(percentLabel.rectTransform, 0.35f));
        }

        yield return new GameSpeedWait(advantageHoldDuration);

        // 3. Fade everything away and drop the score at the same time
        RectTransform scoreRect = scoreText != null ? scoreText.rectTransform : null;

        float fadeDur = Mathf.Max(0.01f, advantageFadeOutDuration);
        float countDur = Mathf.Max(0.01f, advantageCountDuration);
        float total = Mathf.Max(fadeDur, countDur);
        float t = 0f;

        while (t < total)
        {
            t += GameSpeed.DeltaTime;

            float f = Smooth(t / fadeDur);
            float fade = 1f - f;
            if (dim != null) SetDimAlpha(dim, dimAmount * fade);
            FadeLabel(label, advantageColor, fade, 1f + 0.08f * f);
            FadeLabel(percentLabel, advantageColor, fade, 1f + 0.25f * f);

            // the Score To Beat text comes back up (in the green drop colour) as the dim goes away
            SetScoreAreaAlpha(headerBase, reductionColor, Mathf.Lerp(scoreDimmedAlpha, 1f, f));

            if (scoreText != null)
            {
                float p = Mathf.Clamp01(t / countDur);
                float eased = 1f - (1f - p) * (1f - p) * (1f - p); // fast at first, settles at the end
                scoreText.text = Mathf.RoundToInt(Mathf.Lerp(from, to, eased)).ToString();
                scoreRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 60f) * 4f * (1f - p)); // shakes while it drops
            }
            yield return null;
        }

        if (headerText != null) headerText.color = headerBase;
        if (dim != null) dim.gameObject.SetActive(false);
        if (label != null) label.gameObject.SetActive(false);
        if (percentLabel != null) percentLabel.gameObject.SetActive(false);

        if (scoreText != null)
        {
            scoreText.text = to.ToString();
            scoreRect.localRotation = Quaternion.identity;

            yield return StartCoroutine(PopAndWiggleRoutine());

            // colour settles back to normal instead of snapping
            float c = 0f;
            while (c < 0.25f)
            {
                c += GameSpeed.DeltaTime;
                scoreText.color = Color.Lerp(reductionColor, scoreBase, Smooth(c / 0.25f));
                yield return null;
            }
            scoreText.color = scoreBase;
        }
    }

    private static float Smooth(float x)
    {
        x = Mathf.Clamp01(x);
        return x * x * (3f - 2f * x);
    }

    // Header and score at a fraction of their normal opacity
    private void SetScoreAreaAlpha(Color headerColor, Color scoreColor, float multiplier)
    {
        if (headerText != null)
        {
            Color c = headerColor;
            c.a = headerColor.a * multiplier;
            headerText.color = c;
        }
        if (scoreText != null)
        {
            Color c = scoreColor;
            c.a = scoreColor.a * multiplier;
            scoreText.color = c;
        }
    }

    private void SetDimAlpha(Image img, float alpha)
    {
        Color c = dimTint;
        c.a = alpha;
        img.color = c;
    }

    private static void FadeLabel(TextMeshProUGUI label, Color baseColor, float alpha, float scale)
    {
        if (label == null) return;
        Color c = baseColor;
        c.a = alpha;
        label.color = c;
        label.rectTransform.localScale = Vector3.one * scale;
    }

    // Scales from 0 with a little overshoot
    private IEnumerator PopIn(RectTransform r, float duration)
    {
        r.localScale = Vector3.zero;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += GameSpeed.DeltaTime;
            float x = Mathf.Clamp01(elapsed / duration) - 1f;
            const float c1 = 1.70158f;
            r.localScale = Vector3.one * (1f + (c1 + 1f) * x * x * x + c1 * x * x);
            yield return null;
        }
        r.localScale = Vector3.one;
    }

    // A full-screen tinted image behind the score. Made automatically the first time it is needed.
    private Image GetDimOverlay()
    {
        if (dimOverlay != null) return dimOverlay;

        GameObject go = new GameObject("AdvantageDim", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        go.transform.SetParent(transform, false);
        go.transform.SetAsFirstSibling(); // behind the score and the texts

        go.GetComponent<LayoutElement>().ignoreLayout = true;

        RectTransform r = go.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = Vector2.zero;
        r.sizeDelta = new Vector2(8000f, 8000f); // bigger than any screen, so it always covers everything

        dimOverlay = go.GetComponent<Image>();
        dimOverlay.raycastTarget = false;
        SetDimAlpha(dimOverlay, 0f);

        go.SetActive(false);
        return dimOverlay;
    }

    // Uses the assigned text, or makes one under the score from a copy of the header
    private TextMeshProUGUI GetAdvantageText()
    {
        if (advantageText != null) return advantageText;
        if (headerText == null || scoreText == null) return null;

        // A fresh text (NOT a copy of the header: the header object may have the score as a child)
        float labelSize = headerText.fontSize * 0.9f;
        float percentSize = headerText.fontSize * percentTextScale;
        advantageText = MakeText("AdvantageText", labelSize);

        // centred on the Score To Beat, just above the big "-10%"
        float y = percentSize * 0.5f + labelSize * 0.5f;
        advantageText.rectTransform.localPosition = ScoreCentre(advantageText.rectTransform) + new Vector3(0f, y, 0f);
        return advantageText;
    }

    // Centre of the score, in the local space of the texts' parent
    private Vector3 ScoreCentre(RectTransform r)
    {
        RectTransform s = scoreText.rectTransform;
        return r.parent.InverseTransformPoint(s.TransformPoint(s.rect.center));
    }

    // Same look as the header (font, material, style), but its own object
    private TextMeshProUGUI MakeText(string objectName, float size)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        go.transform.SetParent(scoreText.transform.parent, false);
        go.GetComponent<LayoutElement>().ignoreLayout = true;

        TextMeshProUGUI t = go.GetComponent<TextMeshProUGUI>();
        t.font = headerText.font;
        t.fontSharedMaterial = headerText.fontSharedMaterial;
        t.fontStyle = headerText.fontStyle;
        t.alignment = TextAlignmentOptions.Center;
        t.enableAutoSizing = false;
        t.fontSize = size;
        t.color = advantageColor;
        t.raycastTarget = false;
#pragma warning disable 618
        t.enableWordWrapping = false;
#pragma warning restore 618
        t.overflowMode = TextOverflowModes.Overflow;

        RectTransform r = t.rectTransform;
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
        r.sizeDelta = new Vector2(1400f, size * 1.3f);
        r.localScale = Vector3.one;

        go.SetActive(false);
        return t;
    }

    // Uses the assigned text, or makes a big one under the "ADVANTAGE HIT!" text
    private TextMeshProUGUI GetPercentText(TextMeshProUGUI label)
    {
        if (advantagePercentText != null) return advantagePercentText;
        if (headerText == null || label == null) return null;

        advantagePercentText = MakeText("AdvantagePercentText", headerText.fontSize * percentTextScale);

        // centred on the Score To Beat (the "ADVANTAGE HIT!" text sits just above it)
        advantagePercentText.rectTransform.localPosition = ScoreCentre(advantagePercentText.rectTransform);
        return advantagePercentText;
    }

    private IEnumerator PopAndWiggleRoutine()
    {
        if (scoreText == null) yield break;
        RectTransform textRect = scoreText.rectTransform;
        Vector3 baseScale = Vector3.one;
        Vector3 maxPop = baseScale * popScaleMultiplier;

        float elapsed = 0f;
        while (elapsed < 0.1f)
        {
            elapsed += GameSpeed.DeltaTime;
            textRect.localScale = Vector3.Lerp(baseScale, maxPop, elapsed / 0.1f);
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < wiggleDuration)
        {
            elapsed += GameSpeed.DeltaTime;
            float progress = Mathf.Clamp01(elapsed / wiggleDuration);
            float currentWiggle = Mathf.Sin(progress * Mathf.PI * 8f) * wiggleAngle * (1f - progress);

            textRect.localRotation = Quaternion.Euler(0, 0, currentWiggle);
            textRect.localScale = Vector3.Lerp(maxPop, baseScale, progress);
            yield return null;
        }

        textRect.localRotation = Quaternion.identity;
        textRect.localScale = baseScale;
    }
}