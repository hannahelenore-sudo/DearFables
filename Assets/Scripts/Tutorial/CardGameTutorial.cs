using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class CardGameTutorialGate
{
    public const string CardGameSceneName = "CardGame";

    public const float EngageWindow = 4f;

    public static bool Pending;
    static float lastNpcEngageTime = -999f;

    public static bool Completed { get; private set; }

    static bool activeTutorial;
    static bool tutorialUsed;
    public static bool TutorialUsed { get { return tutorialUsed; } }
    static bool forcedInEditor;

    public static bool IsTutorialFight
    {
        get
        {
            if (activeTutorial || forcedInEditor) return true;
            if (tutorialUsed) return false;
            return Pending || Time.unscaledTime - lastNpcEngageTime <= EngageWindow;
        }
    }

    public static void ForceTutorialInEditor() { if (!tutorialUsed) forcedInEditor = true; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Pending = false;
        Completed = false;
        activeTutorial = false;
        tutorialUsed = false;
        forcedInEditor = false;
        lastNpcEngageTime = -999f;
    }

    public static void MarkCompleted() { Completed = true; }

    public static void ResetCompleted() { Completed = false; tutorialUsed = false; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Hook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    static void OnSceneUnloaded(Scene scene)
    {
        if (scene.name == CardGameSceneName)
        {
            activeTutorial = false;
            forcedInEditor = false;
        }
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!tutorialUsed && scene.name == CardGameSceneName &&
            Time.unscaledTime - lastNpcEngageTime <= EngageWindow)
        {
            Pending = true;
        }
    }

    public static void NoteStarQuestEngaged() { if (!tutorialUsed) lastNpcEngageTime = Time.unscaledTime; }

    public static void MarkFromStarQuest() { if (!tutorialUsed) Pending = true; }

    public static bool Consume()
    {
        bool p = Pending && !tutorialUsed;
        Pending = false;
        if (p) MarkTutorialTriggered();
        return p;
    }

    public static void MarkTutorialTriggered()
    {
        tutorialUsed = true;
        activeTutorial = true;
        Pending = false;
    }
}

[DisallowMultipleComponent]
public class CardGameTutorial : MonoBehaviour
{
    public enum TriggerMode
    {
        AfterOpeningSequence = 0,
        WhenObjectVisible = 1
    }

    [Serializable]
    public class Step
    {
        public string speaker = "Bee";
        [TextArea(2, 6)] public string text;
        [Tooltip("Optional. Falls back to Default Portrait.")] public Sprite portrait;
        [Tooltip("Components to leave un-dimmed. Empty = whole screen dimmed.")]
        public Transform[] highlight;
        public float padding = 16f;
    }

    [Serializable]
    public class Section
    {
        public string id;
        public TriggerMode trigger;
        [Tooltip("WhenObjectVisible: the screen/canvas that must be showing.")]
        public GameObject visibleTarget;
        [Tooltip("Optional: also wait for this group to be (almost) fully faded in.")]
        public CanvasGroup visibleGroup;
        [Tooltip("Camera used to project world-space targets (sprites). Empty = Camera.main.")]
        public Camera worldCamera;
        [Tooltip("Seconds the section must be ready before it starts (lets animations settle).")]
        public float delayAfterReady = 0.4f;
        public Step[] steps;

        [NonSerialized] public bool done;
        [NonSerialized] public float readySince = -1f;
    }

    [Header("Run conditions")]
    [Tooltip("Only run when the card game was started by hitting the StarQuest NPC.")]
    public bool onlyWhenStartedFromStarQuest = true;
    [Tooltip("Editor testing: run even if you opened CardGame directly.")]
    public bool forceRunInEditor = false;

    [Header("Opening sequence")]
    [Tooltip("BattleManager (read by reflection so this script has no hard dependency).")]
    public MonoBehaviour battleManager;
    public string openingFlagField = "isOpeningSequenceActive";
    public float openingFallbackDelay = 6f;
    public float openingMaxWait = 15f;

    [Header("Look")]
    public Sprite defaultPortrait;
    [Tooltip("Copy the project's font from this text (optional).")]
    public TMP_Text fontSource;
    public Color dimColor = new Color(0f, 0f, 0f, 0.8f);
    public Color frameColor = new Color(1f, 0.85f, 0.25f, 1f);
    public float frameThickness = 4f;
    [Tooltip("Combine all highlights of a step into one big area instead of outlining each element.")]
    public bool mergeHighlights = true;
    public int overlaySortingOrder = 32000;
    public float charsPerSecond = 70f;
    public string nextLabel = "Press SPACE to continue";
    public string lastStepLabel = "Press SPACE to finish";
    public string skipLabel = "Skip tutorial";
    [Tooltip("Freeze the battle (Time.timeScale = 0) while a step is on screen.")]
    public bool pauseGame = true;

    [Header("When the card game is finished")]
    [Tooltip("Field on the BattleManager that is true when the player won.")]
    public string wonFlagField = "hasPlayerWon";
    [Tooltip("Field on the BattleManager that is true when the player lost.")]
    public string lostFlagField = "hasPlayerLost";
    [Tooltip("ON: only a WIN opens the objects in the game scene. OFF: finishing the fight (win or lose) opens them.")]
    public bool requireWin = false;

    [Header("Content")]
    public Section[] sections;

    GameObject overlayRoot;
    Canvas overlayCanvas;
    TutorialSpotlightGraphic spotlight;
    RectTransform panel;
    Image portraitImage;
    Button nextButton;
    TextMeshProUGUI speakerText, bodyText, counterText, nextLabelText;
    bool advanceRequested, skipRequested, finishedTyping;
    float startTime, prevTimeScale = 1f;
    bool paused;
    bool tutorialRun;
    static Sprite whiteSprite;

    void Awake()
    {
        if (onlyWhenStartedFromStarQuest && Application.isEditor && forceRunInEditor)
            CardGameTutorialGate.ForceTutorialInEditor();
    }

    void Start()
    {
        bool alreadyUsed = CardGameTutorialGate.TutorialUsed;
        bool fromNpc = CardGameTutorialGate.Consume();
        bool run = !alreadyUsed && (!onlyWhenStartedFromStarQuest || fromNpc ||
                   (Application.isEditor && forceRunInEditor));
        if (!run || sections == null || sections.Length == 0)
        {
            enabled = false;
            return;
        }

        CardGameTutorialGate.MarkTutorialTriggered();

        tutorialRun = true;
        startTime = Time.unscaledTime;
        BuildOverlay();
        StartCoroutine(Run());
        StartCoroutine(WatchForBattleEnd());
    }

    IEnumerator WatchForBattleEnd()
    {
        if (battleManager == null)
        {
            Debug.LogWarning("CardGameTutorial: assign the Battle Manager so the end of the fight can be detected.", this);
            yield break;
        }

        while (true)
        {
            TryReadBool(battleManager, wonFlagField, out bool won);
            TryReadBool(battleManager, lostFlagField, out bool lost);
            if (won || (lost && !requireWin))
            {
                CardGameTutorialGate.MarkCompleted();
                yield break;
            }
            yield return null;
        }
    }

    void Update()
    {
        if (overlayRoot == null || !overlayRoot.activeSelf) return;
        if (SpacePressed()) Advance();
    }

    static bool SpacePressed()
    {
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        return k != null && k.spaceKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Space);
#endif
    }

    void OnDisable() { Resume(); InputLock.Pop(this); }
    void OnDestroy() { Resume(); InputLock.Pop(this); }

    IEnumerator Run()
    {
        while (!skipRequested && AnySectionPending())
        {
            Section next = null;
            for (int i = 0; i < sections.Length; i++)
            {
                var s = sections[i];
                if (s == null || s.done || s.steps == null || s.steps.Length == 0) continue;
                if (IsReady(s)) { next = s; break; }
            }

            if (next == null) { yield return null; continue; }
            yield return PlaySection(next);
            next.done = true;
        }

        if (overlayRoot != null) overlayRoot.SetActive(false);
        Resume();
        enabled = false;
    }

    bool AnySectionPending()
    {
        foreach (var s in sections)
            if (s != null && !s.done && s.steps != null && s.steps.Length > 0) return true;
        return false;
    }

    bool OpeningDone()
    {
        float elapsed = Time.unscaledTime - startTime;
        if (battleManager != null && TryReadBool(battleManager, openingFlagField, out bool active))
            return !active || elapsed >= openingMaxWait;
        return elapsed >= openingFallbackDelay;
    }

    bool AllOpeningSectionsDone()
    {
        foreach (var s in sections)
            if (s != null && s.trigger == TriggerMode.AfterOpeningSequence && !s.done &&
                s.steps != null && s.steps.Length > 0) return false;
        return true;
    }

    bool IsReady(Section s)
    {
        bool ok;
        if (s.trigger == TriggerMode.AfterOpeningSequence)
        {
            ok = OpeningDone();
        }
        else
        {
            ok = AllOpeningSectionsDone() && IsVisible(s.visibleTarget, s.visibleGroup);
        }

        if (!ok) { s.readySince = -1f; return false; }
        if (s.readySince < 0f) s.readySince = Time.unscaledTime;
        return Time.unscaledTime - s.readySince >= s.delayAfterReady;
    }

    static bool IsVisible(GameObject go, CanvasGroup group)
    {
        if (go == null || !go.activeInHierarchy) return false;
        var canvas = go.GetComponent<Canvas>();
        if (canvas != null && !canvas.enabled) return false;
        if (group != null && (!group.gameObject.activeInHierarchy || group.alpha < 0.95f)) return false;
        return true;
    }

    IEnumerator PlaySection(Section s)
    {
        overlayRoot.SetActive(true);
        Pause();
        InputLock.Push(this);

        for (int i = 0; i < s.steps.Length && !skipRequested; i++)
        {
            var step = s.steps[i];
            bool last = i == s.steps.Length - 1;
            advanceRequested = false;

            ShowStep(step, i + 1, s.steps.Length, last);

            float shown = 0f;
            finishedTyping = false;
            bodyText.maxVisibleCharacters = 0;
            bodyText.ForceMeshUpdate();
            int total = bodyText.textInfo.characterCount;

            while (!advanceRequested && !skipRequested)
            {
                UpdateHighlights(s, step);
                KeepNextSelected();
                if (!finishedTyping)
                {
                    shown += charsPerSecond * Time.unscaledDeltaTime;
                    bodyText.maxVisibleCharacters = Mathf.Min(total, Mathf.FloorToInt(shown));
                    if (shown >= total) finishedTyping = true;
                }
                yield return null;
                if (advanceRequested && !finishedTyping)
                {
                    finishedTyping = true;
                    bodyText.maxVisibleCharacters = int.MaxValue;
                    advanceRequested = false;
                }
            }
        }

        overlayRoot.SetActive(false);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        InputLock.Pop(this);
        Resume();
    }

    void KeepNextSelected()
    {
        var es = EventSystem.current;
        if (es != null && nextButton != null && es.currentSelectedGameObject != nextButton.gameObject)
            es.SetSelectedGameObject(nextButton.gameObject);
    }

    void Advance() { advanceRequested = true; }
    void Skip() { skipRequested = true; advanceRequested = true; }

    void Pause()
    {
        if (!pauseGame || paused) return;
        prevTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        paused = true;
    }

    void Resume()
    {
        if (!paused) return;
        Time.timeScale = prevTimeScale <= 0f ? 1f : prevTimeScale;
        paused = false;
    }

    void ShowStep(Step step, int index, int count, bool last)
    {
        speakerText.text = step.speaker ?? "";
        bodyText.text = step.text ?? "";
        counterText.text = index + " / " + count;
        nextLabelText.text = last ? lastStepLabel : nextLabel;

        var sprite = step.portrait != null ? step.portrait : defaultPortrait;
        portraitImage.gameObject.SetActive(sprite != null);
        portraitImage.sprite = sprite;
    }

    void UpdateHighlights(Section s, Step step)
    {
        var holes = new List<Rect>();
        if (step.highlight != null)
        {
            foreach (var t in step.highlight)
            {
                if (TryGetScreenRect(t, s.worldCamera, out Rect r))
                {
                    r.xMin -= step.padding; r.xMax += step.padding;
                    r.yMin -= step.padding; r.yMax += step.padding;
                    holes.Add(r);
                }
            }
        }

        if (mergeHighlights && holes.Count > 1)
        {
            Rect all = holes[0];
            for (int i = 1; i < holes.Count; i++)
                all = Rect.MinMaxRect(Mathf.Min(all.xMin, holes[i].xMin), Mathf.Min(all.yMin, holes[i].yMin),
                                      Mathf.Max(all.xMax, holes[i].xMax), Mathf.Max(all.yMax, holes[i].yMax));
            holes.Clear();
            holes.Add(all);
        }

        spotlight.SetHoles(holes, dimColor, frameColor, frameThickness);
        PlacePanel(holes);
    }

    void PlacePanel(List<Rect> holes)
    {
        float scale = overlayCanvas.scaleFactor <= 0f ? 1f : overlayCanvas.scaleFactor;
        float w = panel.rect.width * scale, h = panel.rect.height * scale, margin = 36f * scale;
        float cx = Screen.width * 0.5f;

        var bottom = new Rect(cx - w * 0.5f, margin, w, h);
        var top = new Rect(cx - w * 0.5f, Screen.height - margin - h, w, h);

        float ob = Overlap(bottom, holes), ot = Overlap(top, holes);
        bool useTop = ot < ob;

        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, useTop ? 1f : 0f);
        panel.pivot = new Vector2(0.5f, useTop ? 1f : 0f);
        panel.anchoredPosition = new Vector2(0f, useTop ? -margin / scale : margin / scale);
    }

    static float Overlap(Rect a, List<Rect> holes)
    {
        float sum = 0f;
        foreach (var r in holes)
        {
            float w = Mathf.Min(a.xMax, r.xMax) - Mathf.Max(a.xMin, r.xMin);
            float h = Mathf.Min(a.yMax, r.yMax) - Mathf.Max(a.yMin, r.yMin);
            if (w > 0f && h > 0f) sum += w * h;
        }
        return sum;
    }

    static bool TryGetScreenRect(Transform t, Camera worldCam, out Rect rect)
    {
        rect = default;
        if (t == null || !t.gameObject.activeInHierarchy) return false;

        bool any = false;
        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;

        void Add(Vector2 p)
        {
            any = true;
            if (p.x < minX) minX = p.x; if (p.x > maxX) maxX = p.x;
            if (p.y < minY) minY = p.y; if (p.y > maxY) maxY = p.y;
        }

        var cornersBuf = new Vector3[4];

        foreach (var g in t.GetComponentsInChildren<Graphic>(false))
        {
            if (!g.enabled || (g.canvasRenderer != null && g.canvasRenderer.cull)) continue;
            if (g.color.a <= 0.01f) continue;

            Camera cam = CanvasCamera(g.canvas);

            var tmp = g as TMP_Text;
            if (tmp != null)
            {
                if (string.IsNullOrEmpty(tmp.text)) continue;
                tmp.ForceMeshUpdate();
                Bounds b = tmp.textBounds;
                if (b.size.x < 1f || b.size.y < 1f) continue;
                var tr = tmp.transform;
                Add(ToScreen(cam, tr.TransformPoint(new Vector3(b.min.x, b.min.y, 0f))));
                Add(ToScreen(cam, tr.TransformPoint(new Vector3(b.max.x, b.max.y, 0f))));
                continue;
            }

            g.rectTransform.GetWorldCorners(cornersBuf);
            if (Vector3.Distance(cornersBuf[0], cornersBuf[2]) < 0.0001f) continue;
            for (int i = 0; i < 4; i++) Add(ToScreen(cam, cornersBuf[i]));
        }

        Camera wc = worldCam != null ? worldCam : Camera.main;
        foreach (var r in t.GetComponentsInChildren<Renderer>(false))
        {
            if (!r.enabled || wc == null) continue;
            Bounds b = r.bounds;
            if (b.size == Vector3.zero) continue;
            for (int i = 0; i < 8; i++)
            {
                var c = new Vector3(
                    (i & 1) == 0 ? b.min.x : b.max.x,
                    (i & 2) == 0 ? b.min.y : b.max.y,
                    (i & 4) == 0 ? b.min.z : b.max.z);
                Vector3 sp = wc.WorldToScreenPoint(c);
                if (sp.z > 0f) Add(new Vector2(sp.x, sp.y));
            }
        }

        if (!any)
        {
            var rt = t as RectTransform;
            if (rt != null)
            {
                Camera cam = CanvasCamera(rt.GetComponentInParent<Canvas>());
                rt.GetWorldCorners(cornersBuf);
                for (int i = 0; i < 4; i++) Add(ToScreen(cam, cornersBuf[i]));
            }
        }

        if (!any) return false;

        minX = Mathf.Max(0f, minX); minY = Mathf.Max(0f, minY);
        maxX = Mathf.Min(Screen.width, maxX); maxY = Mathf.Min(Screen.height, maxY);
        if (maxX - minX < 2f || maxY - minY < 2f) return false;

        rect = Rect.MinMaxRect(minX, minY, maxX, maxY);
        return true;
    }

    static Camera CanvasCamera(Canvas c)
    {
        if (c == null) return null;
        var root = c.rootCanvas;
        if (root.renderMode == RenderMode.ScreenSpaceOverlay) return null;
        return root.worldCamera != null ? root.worldCamera : Camera.main;
    }

    static Vector2 ToScreen(Camera cam, Vector3 world)
    {
        return RectTransformUtility.WorldToScreenPoint(cam, world);
    }

    static bool TryReadBool(object target, string member, out bool value)
    {
        value = false;
        if (target == null || string.IsNullOrEmpty(member)) return false;
        const BindingFlags f = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        for (Type ty = target.GetType(); ty != null; ty = ty.BaseType)
        {
            var fi = ty.GetField(member, f | BindingFlags.DeclaredOnly);
            if (fi != null && fi.FieldType == typeof(bool)) { value = (bool)fi.GetValue(target); return true; }
            var pi = ty.GetProperty(member, f | BindingFlags.DeclaredOnly);
            if (pi != null && pi.PropertyType == typeof(bool) && pi.CanRead)
            { value = (bool)pi.GetValue(target, null); return true; }
        }
        return false;
    }

    static Sprite WhiteSprite()
    {
        if (whiteSprite == null)
        {
            var tex = Texture2D.whiteTexture;
            whiteSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        }
        return whiteSprite;
    }

    void BuildOverlay()
    {
        overlayRoot = new GameObject("TutorialOverlay", typeof(RectTransform));
        overlayRoot.transform.SetParent(transform, false);

        overlayCanvas = overlayRoot.AddComponent<Canvas>();
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.overrideSorting = true;
        overlayCanvas.sortingOrder = overlaySortingOrder;

        var scaler = overlayRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        overlayRoot.AddComponent<GraphicRaycaster>();

        var dimGo = new GameObject("Dim", typeof(RectTransform), typeof(CanvasRenderer));
        dimGo.transform.SetParent(overlayRoot.transform, false);
        var dimRt = dimGo.GetComponent<RectTransform>();
        dimRt.anchorMin = Vector2.zero; dimRt.anchorMax = Vector2.one;
        dimRt.offsetMin = dimRt.offsetMax = Vector2.zero;
        dimRt.pivot = Vector2.zero;
        spotlight = dimGo.AddComponent<TutorialSpotlightGraphic>();
        spotlight.raycastTarget = true;

        var panelGo = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panelGo.transform.SetParent(overlayRoot.transform, false);
        panel = panelGo.GetComponent<RectTransform>();
        panel.sizeDelta = new Vector2(1200f, 240f);
        var panelImg = panelGo.GetComponent<Image>();
        panelImg.sprite = WhiteSprite();
        panelImg.color = new Color(0.07f, 0.07f, 0.11f, 0.97f);
        var outline = panelGo.AddComponent<Outline>();
        outline.effectColor = frameColor;
        outline.effectDistance = new Vector2(3f, -3f);

        var pGo = new GameObject("Portrait", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        pGo.transform.SetParent(panel, false);
        var pRt = pGo.GetComponent<RectTransform>();
        pRt.anchorMin = pRt.anchorMax = new Vector2(0f, 0.5f);
        pRt.pivot = new Vector2(0f, 0.5f);
        pRt.anchoredPosition = new Vector2(20f, 0f);
        pRt.sizeDelta = new Vector2(190f, 190f);
        portraitImage = pGo.GetComponent<Image>();
        portraitImage.preserveAspect = true;
        portraitImage.raycastTarget = false;

        speakerText = MakeText("Speaker", panel, 34, FontStyles.Bold, frameColor);
        var sRt = speakerText.rectTransform;
        sRt.anchorMin = new Vector2(0f, 1f); sRt.anchorMax = new Vector2(1f, 1f);
        sRt.pivot = new Vector2(0f, 1f);
        sRt.offsetMin = new Vector2(230f, -62f); sRt.offsetMax = new Vector2(-220f, -16f);

        bodyText = MakeText("Body", panel, 30, FontStyles.Normal, Color.white);
        bodyText.alignment = TextAlignmentOptions.TopLeft;
        var bRt = bodyText.rectTransform;
        bRt.anchorMin = Vector2.zero; bRt.anchorMax = Vector2.one;
        bRt.offsetMin = new Vector2(230f, 70f); bRt.offsetMax = new Vector2(-30f, -64f);

        counterText = MakeText("Counter", panel, 24, FontStyles.Normal, new Color(1f, 1f, 1f, 0.55f));
        counterText.alignment = TextAlignmentOptions.TopRight;
        var cRt = counterText.rectTransform;
        cRt.anchorMin = new Vector2(1f, 1f); cRt.anchorMax = new Vector2(1f, 1f);
        cRt.pivot = new Vector2(1f, 1f);
        cRt.sizeDelta = new Vector2(180f, 36f);
        cRt.anchoredPosition = new Vector2(-24f, -18f);

        var nextBtn = MakeButton("Next", panel, new Vector2(400f, 56f), frameColor, Color.black, out nextLabelText);
        var nRt = nextBtn.GetComponent<RectTransform>();
        nRt.anchorMin = nRt.anchorMax = new Vector2(1f, 0f);
        nRt.pivot = new Vector2(1f, 0f);
        nRt.anchoredPosition = new Vector2(-24f, 16f);
        nextBtn.onClick.AddListener(Advance);
        nextBtn.navigation = new Navigation { mode = Navigation.Mode.None };
        nextButton = nextBtn;

        var skipBtn = MakeButton("Skip", panel, new Vector2(220f, 44f), new Color(1f, 1f, 1f, 0.12f), Color.white,
            out TextMeshProUGUI skipText);
        skipText.fontSize = 22;
        skipText.text = skipLabel;
        var kRt = skipBtn.GetComponent<RectTransform>();
        kRt.anchorMin = kRt.anchorMax = new Vector2(0f, 0f);
        kRt.pivot = new Vector2(0f, 0f);
        kRt.anchoredPosition = new Vector2(230f, 20f);
        skipBtn.onClick.AddListener(Skip);

        overlayRoot.SetActive(false);
    }

    TextMeshProUGUI MakeText(string name, Transform parent, float size, FontStyles style, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<TextMeshProUGUI>();
        if (fontSource != null && fontSource.font != null) t.font = fontSource.font;
        t.fontSize = size;
        t.fontStyle = style;
        t.color = color;
        t.raycastTarget = false;
        t.enableAutoSizing = false;
        return t;
    }

    Button MakeButton(string name, Transform parent, Vector2 size, Color bg, Color fg, out TextMeshProUGUI label)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        go.GetComponent<RectTransform>().sizeDelta = size;
        var img = go.GetComponent<Image>();
        img.sprite = WhiteSprite();
        img.color = bg;
        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;

        label = MakeText("Label", go.transform, 28, FontStyles.Bold, fg);
        label.alignment = TextAlignmentOptions.Center;
        var lr = label.rectTransform;
        lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
        lr.offsetMin = lr.offsetMax = Vector2.zero;
        return btn;
    }
}