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

    public void StartSequence(System.Action<int> onComplete)
    {
        if (sequenceRoutine != null) StopCoroutine(sequenceRoutine);
        gameObject.SetActive(true);

        if (canvasGroup != null) canvasGroup.alpha = 1f;
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
        float elapsed = 0f;
        float nextSpinTime = 0f;
        int finalNumber = minNumber;

        while (elapsed < spinDuration)
        {
            elapsed += Time.unscaledDeltaTime;
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
        yield return new WaitForSecondsRealtime(postSpinPauseDuration);

        if (container != null)
        {
            Vector2 startPos = container.anchoredPosition;
            Vector3 startScale = container.localScale;
            float moveElapsed = 0f;
            Color startBgColor = (spinnerBackground != null) ? spinnerBackground.color : Color.white;

            while (moveElapsed < moveToCornerDuration)
            {
                moveElapsed += Time.unscaledDeltaTime;
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

    private IEnumerator PopAndWiggleRoutine()
    {
        if (container == null) yield break;
        Vector3 baseScale = initialScale;
        Vector3 maxPop = baseScale * popScaleMultiplier;

        float elapsed = 0f;
        while (elapsed < 0.1f)
        {
            elapsed += Time.unscaledDeltaTime;
            container.localScale = Vector3.Lerp(baseScale, maxPop, elapsed / 0.1f);
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < wiggleDuration)
        {
            elapsed += Time.unscaledDeltaTime;
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