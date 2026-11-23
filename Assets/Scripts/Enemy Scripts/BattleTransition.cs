using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class BattleTransition : MonoBehaviour
{
    public static int flashCount = 3;
    public static float flashHalfDuration = 0.09f;
    public static float pauseAfterFlash = 0.1f;

    public static int barCount = 10;
    public static float barSlideDuration = 0.35f;
    public static float barStagger = 0.04f;

    public static float holdOnBlack = 0.25f;
    public static float revealDuration = 0.5f;

    public static Color flashColor = Color.white;
    public static Color barColor = Color.black;

    public static bool IsPlaying { get; private set; }

    private CanvasGroup group;
    private Image flash;
    private RectTransform[] bars;
    private float[] startSide;

    private bool frozen;
    private float previousTimeScale = 1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        IsPlaying = false;
    }

    public static void Play(Action onScreenCovered)
    {
        if (IsPlaying) return;
        IsPlaying = true;

        GameObject go = new GameObject("BattleTransition");
        DontDestroyOnLoad(go);

        BattleTransition t = go.AddComponent<BattleTransition>();
        t.StartCoroutine(t.Run(onScreenCovered));
    }

    public static void PlayFade(Action onScreenCovered, float fadeToBlackDuration = 0.5f)
    {
        if (IsPlaying) return;
        IsPlaying = true;

        GameObject go = new GameObject("BattleTransition");
        DontDestroyOnLoad(go);

        BattleTransition t = go.AddComponent<BattleTransition>();
        t.StartCoroutine(t.RunFade(onScreenCovered, fadeToBlackDuration));
    }

    private void OnDestroy()
    {
        if (frozen)
            Time.timeScale = previousTimeScale;

        IsPlaying = false;
    }

    private IEnumerator Run(Action onScreenCovered)
    {
        BuildUI();

        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        frozen = true;

        for (int i = 0; i < flashCount; i++)
        {
            yield return FadeImage(flash, 0f, 1f, flashHalfDuration);
            yield return FadeImage(flash, 1f, 0f, flashHalfDuration);
        }

        yield return new WaitForSecondsRealtime(pauseAfterFlash);

        yield return SlideBars();

        Time.timeScale = previousTimeScale;
        frozen = false;

        onScreenCovered?.Invoke();

        yield return HoldAndReveal();
    }

    private IEnumerator RunFade(Action onScreenCovered, float duration)
    {
        BuildUI();

        for (int i = 0; i < barCount; i++)
            SetBarOffset(i, 0f);

        group.alpha = 0f;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Mathf.Min(Time.unscaledDeltaTime, 0.033f);
            float t = Mathf.Clamp01(elapsed / duration);
            group.alpha = t * t * (3f - 2f * t);
            yield return null;
        }

        group.alpha = 1f;

        onScreenCovered?.Invoke();

        yield return HoldAndReveal();
    }

    private IEnumerator HoldAndReveal()
    {
        yield return new WaitForSecondsRealtime(holdOnBlack);

        float elapsed = 0f;
        while (elapsed < revealDuration)
        {
            elapsed += Mathf.Min(Time.unscaledDeltaTime, 0.033f);
            float t = Mathf.Clamp01(elapsed / revealDuration);
            group.alpha = 1f - t * t * (3f - 2f * t);
            yield return null;
        }

        IsPlaying = false;
        Destroy(gameObject);
    }

    private IEnumerator SlideBars()
    {
        float total = barSlideDuration + barStagger * (barCount - 1);
        float elapsed = 0f;

        while (elapsed < total)
        {
            elapsed += Mathf.Min(Time.unscaledDeltaTime, 0.033f);

            for (int i = 0; i < barCount; i++)
            {
                float t = Mathf.Clamp01((elapsed - i * barStagger) / barSlideDuration);
                float eased = 1f - (1f - t) * (1f - t) * (1f - t);
                SetBarOffset(i, startSide[i] * (1f - eased));
            }

            yield return null;
        }

        for (int i = 0; i < barCount; i++)
            SetBarOffset(i, 0f);
    }

    private IEnumerator FadeImage(Image img, float from, float to, float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Mathf.Min(Time.unscaledDeltaTime, 0.033f);
            SetAlpha(img, Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration)));
            yield return null;
        }

        SetAlpha(img, to);
    }

    private void BuildUI()
    {
        GameObject canvasGO = new GameObject("Canvas");
        canvasGO.transform.SetParent(transform, false);

        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32767;

        canvasGO.AddComponent<GraphicRaycaster>();
        group = canvasGO.AddComponent<CanvasGroup>();

        bars = new RectTransform[barCount];
        startSide = new float[barCount];

        for (int i = 0; i < barCount; i++)
        {
            GameObject barGO = new GameObject("Bar" + i, typeof(RectTransform), typeof(Image));
            barGO.transform.SetParent(canvasGO.transform, false);

            Image img = barGO.GetComponent<Image>();
            img.color = barColor;

            bars[i] = barGO.GetComponent<RectTransform>();
            bars[i].offsetMin = Vector2.zero;
            bars[i].offsetMax = Vector2.zero;

            startSide[i] = (i % 2 == 0) ? -1f : 1f;
            SetBarOffset(i, startSide[i]);
        }

        GameObject flashGO = new GameObject("Flash", typeof(RectTransform), typeof(Image));
        flashGO.transform.SetParent(canvasGO.transform, false);

        flash = flashGO.GetComponent<Image>();
        flash.color = new Color(flashColor.r, flashColor.g, flashColor.b, 0f);

        RectTransform flashRect = flashGO.GetComponent<RectTransform>();
        flashRect.anchorMin = Vector2.zero;
        flashRect.anchorMax = Vector2.one;
        flashRect.offsetMin = Vector2.zero;
        flashRect.offsetMax = Vector2.zero;
    }

    private void SetBarOffset(int index, float xOffset)
    {
        float y0 = (float)index / barCount;
        float y1 = (index + 1f) / barCount;
        const float overlap = 0.002f;

        bars[index].anchorMin = new Vector2(xOffset, Mathf.Max(0f, y0 - overlap));
        bars[index].anchorMax = new Vector2(xOffset + 1f, Mathf.Min(1f, y1 + overlap));
        bars[index].offsetMin = Vector2.zero;
        bars[index].offsetMax = Vector2.zero;
    }

    private static void SetAlpha(Image img, float alpha)
    {
        Color c = img.color;
        c.a = alpha;
        img.color = c;
    }
}