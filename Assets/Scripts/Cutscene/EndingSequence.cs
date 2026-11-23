using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

[System.Serializable]
public class EndingSettings
{
    [Tooltip("Exact name of your main menu scene. It must be in File > Build Profiles / Build Settings.")]
    public string mainMenuScene = "MainMenu";

    [Header("Text")]
    public string message = "Thanks for playing!";
    [Tooltip("Small line shown once the player is allowed to click. Leave empty for none.")]
    public string hint = "Click anywhere to return to the main menu";
    [Tooltip("Leave empty to use TextMeshPro's default font. Drag your game's font here to match the rest of the UI.")]
    public TMP_FontAsset font;
    public Color textColor = new Color(0.15f, 0.15f, 0.22f, 1f);
    public float messageSize = 110f;
    public float hintSize = 40f;

    [Header("Timing (seconds)")]
    [Tooltip("How long the screen takes to fade to white.")]
    public float fadeToWhite = 2f;
    [Tooltip("How long the message takes to fade in once the screen is white.")]
    public float textFadeIn = 1.5f;
    [Tooltip("How long after the text is visible before a click is accepted (stops an accidental click skipping it).")]
    public float clickDelay = 1f;

    [Header("Behaviour")]
    [Tooltip("ON: the game is paused during the ending, so the player cannot walk around or throw cards behind the white screen.")]
    public bool freezeGame = true;
    public Color screenColor = Color.white;
}

public class EndingSequence : MonoBehaviour
{
    public static bool IsPlaying { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { IsPlaying = false; }

    public static void Play() { Play(new EndingSettings()); }

    public static void Play(EndingSettings settings)
    {
        if (IsPlaying) return;
        if (settings == null) settings = new EndingSettings();

        IsPlaying = true;
        GameObject go = new GameObject("EndingSequence");
        EndingSequence seq = go.AddComponent<EndingSequence>();
        seq.StartCoroutine(seq.Run(settings));
    }

    private void OnDestroy()
    {
        IsPlaying = false;
    }

    private IEnumerator Run(EndingSettings s)
    {
        float previousScale = Time.timeScale;
        if (s.freezeGame) Time.timeScale = 0f;

        GameObject canvasGO = new GameObject("EndingCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGO.transform.SetParent(transform, false);

        Canvas canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;

        CanvasScaler scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        GameObject bgGO = new GameObject("White", typeof(RectTransform));
        bgGO.transform.SetParent(canvasGO.transform, false);
        Stretch(bgGO.GetComponent<RectTransform>());
        Image bg = bgGO.AddComponent<Image>();
        Color screen = s.screenColor;
        screen.a = 0f;
        bg.color = screen;
        bg.raycastTarget = true;

        TextMeshProUGUI message = MakeText(canvasGO.transform, "Message", s.message, s.messageSize, s, FontStyles.Bold);
        RectTransform mr = message.rectTransform;
        mr.anchorMin = new Vector2(0f, 0.5f);
        mr.anchorMax = new Vector2(1f, 0.5f);
        mr.pivot = new Vector2(0.5f, 0.5f);
        mr.sizeDelta = new Vector2(-200f, 300f);
        mr.anchoredPosition = Vector2.zero;
        SetAlpha(message, 0f);

        TextMeshProUGUI hint = MakeText(canvasGO.transform, "Hint", s.hint, s.hintSize, s, FontStyles.Italic);
        RectTransform hr = hint.rectTransform;
        hr.anchorMin = new Vector2(0f, 0f);
        hr.anchorMax = new Vector2(1f, 0f);
        hr.pivot = new Vector2(0.5f, 0f);
        hr.sizeDelta = new Vector2(-200f, 80f);
        hr.anchoredPosition = new Vector2(0f, 70f);
        SetAlpha(hint, 0f);

        float t = 0f;
        float fade = Mathf.Max(0.01f, s.fadeToWhite);
        while (t < fade)
        {
            t += Time.unscaledDeltaTime;
            screen.a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / fade));
            bg.color = screen;
            yield return null;
        }
        screen.a = 1f;
        bg.color = screen;

        t = 0f;
        float textFade = Mathf.Max(0.01f, s.textFadeIn);
        while (t < textFade)
        {
            t += Time.unscaledDeltaTime;
            SetAlpha(message, Mathf.Clamp01(t / textFade));
            yield return null;
        }
        SetAlpha(message, 1f);

        if (s.clickDelay > 0f) yield return new WaitForSecondsRealtime(s.clickDelay);

        while (Input.GetMouseButton(0)) yield return null;

        float pulse = 0f;
        while (!ClickedThisFrame())
        {
            pulse += Time.unscaledDeltaTime;
            if (!string.IsNullOrEmpty(s.hint))
                SetAlpha(hint, 0.35f + 0.65f * (0.5f + 0.5f * Mathf.Sin(pulse * 3f)));
            yield return null;
        }

        Time.timeScale = previousScale > 0f ? previousScale : 1f;

        if (Application.CanStreamedLevelBeLoaded(s.mainMenuScene))
        {
            SceneManager.LoadScene(s.mainMenuScene);
        }
        else
        {
            Debug.LogError("EndingSequence: the scene '" + s.mainMenuScene + "' is not in the Build Settings (or the name is wrong). Loading scene 0 instead.");
            SceneManager.LoadScene(0);
        }
    }

    private static bool ClickedThisFrame()
    {
        if (Input.GetMouseButtonDown(0)) return true;
        for (int i = 0; i < Input.touchCount; i++)
            if (Input.GetTouch(i).phase == TouchPhase.Began) return true;
        return false;
    }

    private static TextMeshProUGUI MakeText(Transform parent, string name, string text, float size, EndingSettings s, FontStyles style)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        if (s.font != null) t.font = s.font;
        t.text = text;
        t.fontSize = size;
        t.fontStyle = style;
        t.color = s.textColor;
        t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false;
        return t;
    }

    private static void SetAlpha(TMP_Text t, float a)
    {
        Color c = t.color;
        c.a = a;
        t.color = c;
    }

    private static void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;
    }
}