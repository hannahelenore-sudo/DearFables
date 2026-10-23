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

    [Header("Animation Settings")]
    public float reelDuration = 2.0f;
    public float popScaleMultiplier = 1.35f;
    public float wiggleAngle = 10f;
    public float wiggleDuration = 0.35f;
    public float postAnimationPause = 1.0f;

    private Coroutine currentRoutine;
    private CanvasGroup canvasGroup;

    void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    public void StartSequence(int targetScore, System.Action onComplete = null)
    {
        if (currentRoutine != null) StopCoroutine(currentRoutine);
        gameObject.SetActive(true);

        foreach (GameObject ui in gameplayUIElements)
        {
            if (ui != null) ui.SetActive(false);
        }

        currentRoutine = StartCoroutine(PlaySequenceRoutine(targetScore, onComplete));
    }

    private IEnumerator PlaySequenceRoutine(int targetScore, System.Action onComplete)
    {
        if (canvasGroup != null) canvasGroup.alpha = 1f;

        if (headerText != null) headerText.text = "SCORE TO BEAT";
        if (scoreText != null) scoreText.text = "1";

        float elapsed = 0f;
        while (elapsed < reelDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / reelDuration);

            if (scoreText != null)
            {
                int currentVal = Mathf.RoundToInt(Mathf.Lerp(1, targetScore, progress));
                scoreText.text = currentVal.ToString();
            }
            yield return null;
        }

        if (scoreText != null) scoreText.text = targetScore.ToString();

        yield return StartCoroutine(PopAndWiggleRoutine());

        yield return new WaitForSecondsRealtime(postAnimationPause);

        if (canvasGroup != null) canvasGroup.alpha = 0f;
        gameObject.SetActive(false);
        currentRoutine = null;
        onComplete?.Invoke();
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
            elapsed += Time.unscaledDeltaTime;
            textRect.localScale = Vector3.Lerp(baseScale, maxPop, elapsed / 0.1f);
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < wiggleDuration)
        {
            elapsed += Time.unscaledDeltaTime;
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