using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pokemon-style battle intro. No scene setup needed, it builds its own UI.
/// Usage:  BattleTransition.Play(() => SceneManager.LoadScene("CardGame"));
///
/// Sequence: game freezes -> screen flashes white a few times -> black bars slide in
/// from alternating sides until the screen is covered -> your callback runs (load the scene)
/// -> the black stays through the load, then fades away in the new scene.
/// </summary>
public class BattleTransition : MonoBehaviour
{
    // ---------------- Tweak these ----------------
    public static int flashCount = 3;
    public static float flashHalfDuration = 0.09f;   // time to flash in (and again to flash out)
    public static float pauseAfterFlash = 0.1f;

    public static int barCount = 10;                 // how many horizontal stripes
    public static float barSlideDuration = 0.35f;    // how long each stripe takes to slide in
    public static float barStagger = 0.04f;          // delay between each stripe starting

    public static float holdOnBlack = 0.25f;         // stays black after the scene load is triggered
    public static float revealDuration = 0.5f;      // fade-out in the new scene

    public static Color flashColor = Color.white;
    public static Color barColor = Color.black;
    // ----------------------------------------------

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

    /// <summary>Plays the transition. onScreenCovered runs once the screen is fully black.</summary>
    public static void Play(Action onScreenCovered)
    {
        if (IsPlaying) return;
        IsPlaying = true;

        GameObject go = new GameObject("BattleTransition");
        DontDestroyOnLoad(go); // survives the scene load so the black doesn't pop away

        BattleTransition t = go.AddComponent<BattleTransition>();
        t.StartCoroutine(t.Run(onScreenCovered));
    }

    /// <summary>
    /// Simple fade to black (no flashes or bars). Use it when leaving the card game:
    /// BattleTransition.PlayFade(() => SceneManager.LoadScene("GameScene"));
    /// The black stays through the scene load, then fades away in the new scene.
    /// </summary>
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
        // Safety: never leave the game frozen
        if (frozen)
            Time.timeScale = previousTimeScale;

        IsPlaying = false;
    }

    // -------------------------------------------------------------------

    private IEnumerator Run(Action onScreenCovered)
    {
        BuildUI();

        // Freeze the game behind the transition
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        frozen = true;

        // 1. White flashes
        for (int i = 0; i < flashCount; i++)
        {
            yield return FadeImage(flash, 0f, 1f, flashHalfDuration);
            yield return FadeImage(flash, 1f, 0f, flashHalfDuration);
        }

        yield return new WaitForSecondsRealtime(pauseAfterFlash);

        // 2. Bars slide in until the screen is covered
        yield return SlideBars();

        // 3. Screen is black: unfreeze and let the caller load the scene
        Time.timeScale = previousTimeScale;
        frozen = false;

        onScreenCovered?.Invoke();

        // 4. Stay black through the load, then fade away
        yield return HoldAndReveal();
    }

    private IEnumerator RunFade(Action onScreenCovered, float duration)
    {
        BuildUI();

        // Cover the whole screen with the (black) bars, then fade the whole thing in
        for (int i = 0; i < barCount; i++)
            SetBarOffset(i, 0f);

        group.alpha = 0f;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Mathf.Min(Time.unscaledDeltaTime, 0.033f);
            float t = Mathf.Clamp01(elapsed / duration);
            group.alpha = t * t * (3f - 2f * t); // smoothstep
            yield return null;
        }

        group.alpha = 1f;

        onScreenCovered?.Invoke();

        yield return HoldAndReveal();
    }

    // Shared ending: stay black through the scene load, fade away, clean up
    private IEnumerator HoldAndReveal()
    {
        yield return new WaitForSecondsRealtime(holdOnBlack);

        float elapsed = 0f;
        while (elapsed < revealDuration)
        {
            elapsed += Mathf.Min(Time.unscaledDeltaTime, 0.033f);
            float t = Mathf.Clamp01(elapsed / revealDuration);
            group.alpha = 1f - t * t * (3f - 2f * t); // smoothstep
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
            // Clamp so a slow frame can't make the bars jump
            elapsed += Mathf.Min(Time.unscaledDeltaTime, 0.033f);

            for (int i = 0; i < barCount; i++)
            {
                float t = Mathf.Clamp01((elapsed - i * barStagger) / barSlideDuration);
                float eased = 1f - (1f - t) * (1f - t) * (1f - t); // ease-out cubic
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

    // -------------------------------------------------------------------
    // UI building
    // -------------------------------------------------------------------

    private void BuildUI()
    {
        GameObject canvasGO = new GameObject("Canvas");
        canvasGO.transform.SetParent(transform, false);

        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32767; // on top of everything

        canvasGO.AddComponent<GraphicRaycaster>(); // blocks clicks during the transition
        group = canvasGO.AddComponent<CanvasGroup>();

        // Bars first, so the white flash would draw on top of them
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

            startSide[i] = (i % 2 == 0) ? -1f : 1f; // alternate left / right
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

    // xOffset is in screen widths: -1 = fully off the left, 0 = covering, 1 = fully off the right
    private void SetBarOffset(int index, float xOffset)
    {
        float y0 = (float)index / barCount;
        float y1 = (index + 1f) / barCount;
        const float overlap = 0.002f; // avoids hairline gaps between stripes

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