using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// =====================================================================================
//  QuestLog: a tiny quest list the menu reads from. Call it from your dialogue / NPC scripts:
//      QuestLog.Add("deliver_letter", "Deliver the Letter", "Bring the letter to the shopkeeper.");
//      QuestLog.Complete("deliver_letter");
//  (Held in memory for the session. Tell me if you want it saved.)
// =====================================================================================
public static class QuestLog
{
    [Serializable]
    public class Quest
    {
        public string id;
        public string title;
        public string description;
        public bool completed;
    }

    private static readonly List<Quest> quests = new List<Quest>();

    public static IReadOnlyList<Quest> All { get { return quests; } }

    /// <summary>Used by SaveSystem when loading.</summary>
    public static void ReplaceAll(IEnumerable<Quest> list)
    {
        quests.Clear();
        if (list != null) quests.AddRange(list);
    }

    public static bool Has(string id) { return Find(id) != null; }

    public static bool IsCompleted(string id)
    {
        Quest q = Find(id);
        return q != null && q.completed;
    }

    public static void Add(string id, string title, string description)
    {
        if (Find(id) != null) return;
        quests.Add(new Quest { id = id, title = title, description = description });
    }

    public static void Complete(string id)
    {
        Quest q = Find(id);
        if (q != null) q.completed = true;
    }

    private static Quest Find(string id)
    {
        for (int i = 0; i < quests.Count; i++)
            if (quests[i].id == id) return quests[i];
        return null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { quests.Clear(); }
}

// Mouse hover / click for one menu entry
public class GameMenuItemHover : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
{
    public Action onEnter;
    public Action onClick;
    public void OnPointerEnter(PointerEventData eventData) { if (onEnter != null) onEnter(); }
    public void OnPointerClick(PointerEventData eventData) { if (onClick != null) onClick(); }
}

/// <summary>
/// Persona-style slanted game menu: INVENTORY / STATS / QUESTS / SETTINGS / CREDITS.
/// Builds its own UI at runtime and survives scene changes, so there is nothing to set up:
/// it appears automatically in the scenes listed in "Active Scenes" (GameScene and CardGame).
/// Open it with Esc (or Start on a gamepad) or the MENU button. While open the game is paused.
/// Up/Down (or W/S) or the mouse change the entry, Left/Right change volume in Settings, Esc closes.
/// </summary>
public class GameMenu : MonoBehaviour
{
    public static GameMenu Instance { get; private set; }

    [Header("Where the menu is available (scene names)")]
    public string[] activeScenes = { "GameScene", "CardGame" };

    [Header("Return to main menu")]
    public string mainMenuScene = "MainMenu";

    [Header("Controls")]
    public Key openKey = Key.Escape;
    public bool showOpenButton = true;

    [Header("Credits")]
    [TextArea(6, 24)]
    public string creditsText =
        "YOUR GAME TITLE\n\n\n" +
        "Game Design\nYour Name\n\n" +
        "Programming\nYour Name\n\n" +
        "Art\nYour Name\n\n" +
        "Music & Sound\nYour Name\n\n\n" +
        "Thank you for playing!";
    public float creditsScrollSpeed = 70f;

    // ---------------------------------------------------------------- look
    private static readonly Color Lavender = new Color(0.90f, 0.85f, 1f, 1f);
    private static readonly Color Ink = new Color(0.16f, 0.06f, 0.33f, 1f);
    private static readonly Color DeepPurple = new Color(0.17f, 0.07f, 0.33f, 1f);
    private static readonly Color Accent = new Color(0.50f, 0.32f, 0.92f, 1f);
    private static readonly Color Unselected = new Color(0.62f, 0.56f, 0.74f, 1f);

    private const float Slant = 10f;        // degrees the menu text leans
    private const float WedgeAngle = 12f;   // degrees the dark panel's edge leans
    private const string VolumeKey = "menu_master_volume";

    private static readonly string[] Names = { "Inventory", "Stats", "Quests", "Save / Load", "Credits", "Settings", "Main Menu", "Continue", "New Game", "Quit" };
    private static readonly string[] Hints =
    {
        "Check what you are carrying",
        "See your boosts and upgrades",
        "Keep track of your tasks",
        "Save your progress or load it back",
        "The people behind the game",
        "Change volume and display",
        "Leave the game and go back to the title screen",
        "Pick up where you left off",
        "Start a brand new adventure",
        "Close the game"
    };
    private const int Inventory = 0, Stats = 1, Quests = 2, SaveLoad = 3, Credits = 4, Settings = 5, MainMenuEntry = 6,
                       Continue = 7, NewGame = 8, Quit = 9;

    // which entries are shown, per mode
    private static readonly int[] GameIds = { Inventory, Stats, Quests, SaveLoad, Credits, Settings, MainMenuEntry };
    private static readonly int[] TitleIds = { Continue, NewGame, Settings, Credits, Quit };

    // ---------------------------------------------------------------- state
    private bool isOpen;
    private float openT;
    private int selected;
    private int[] ids = GameIds;
    private bool titleMode;
    private Image bgImage;
    private Image panelBg;
    private GameObject[] decor;
    private GameObject closeButtonObj;
    private GameObject footerObj;
    private TextMeshProUGUI footerText;

    private int CurrentId { get { return ids[selected]; } }
    private float prevTimeScale = 1f;
    private bool sceneAllowed;

    // ---------------------------------------------------------------- ui refs
    private GameObject buttonRoot;
    private GameObject menuRoot;
    private CanvasGroup menuGroup;
    private RectTransform slideRoot;
    private RectTransform panelRoot;
    private RectTransform body;
    private Image highlight;
    private TextMeshProUGUI[] items;
    private TextMeshProUGUI bigTitle;
    private TextMeshProUGUI headerText;
    private TextMeshProUGUI hintText;
    private Vector2 panelRest = new Vector2(-470f, -20f);

    private Slider volumeSlider;
    private TextMeshProUGUI volumeValue;
    private TextMeshProUGUI fullscreenLabel;
    private string saveStatus = "";
    private bool[] saveArmed = new bool[SaveSystem.SlotCount];
    private ScrollRect creditsScroll;
    private float creditsY;

    // =====================================================================
    //  Setup
    // =====================================================================

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Instance = null; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        if (FindFirstObjectByType<GameMenu>() != null) return;
        new GameObject("GameMenu").AddComponent<GameMenu>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        AudioListener.volume = PlayerPrefs.GetFloat(VolumeKey, 1f);

        BuildOpenButton();
        BuildMenu();
        menuRoot.SetActive(false);

        SceneManager.sceneLoaded += OnSceneLoaded;
        ApplyScene(SceneManager.GetActiveScene());
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            RestoreTime();
            Instance = null;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyScene(scene);
    }

    private void ApplyScene(Scene scene)
    {
        sceneAllowed = false;
        for (int i = 0; i < activeScenes.Length; i++)
            if (activeScenes[i] == scene.name) sceneAllowed = true;

        // never carry an open menu (or its pause) into another scene
        if (isOpen) Close();
        openT = 0f;
        menuRoot.SetActive(false);

        // the title screen gets its own always-open version of the menu
        bool wantTitle = scene.name == mainMenuScene;
        if (wantTitle) sceneAllowed = true;
        SetMode(wantTitle);
        if (titleMode) Open();
    }

    private void SetMode(bool title)
    {
        titleMode = title;
        ids = title ? TitleIds : GameIds;

        // on the title screen the game's own title art must stay visible
        bgImage.color = title ? new Color(0.05f, 0.02f, 0.12f, 0.25f) : Lavender;
        panelBg.color = title ? new Color(1f, 1f, 1f, 0.88f) : new Color(1f, 1f, 1f, 0.6f);
        for (int i = 0; i < decor.Length; i++) decor[i].SetActive(!title);
        closeButtonObj.SetActive(!title);
        footerText.text = title ? "ENTER  Select" : "ENTER  Select        ESC  Close";

        // the old Play / Quit buttons of the scene are replaced by this menu
        if (title)
        {
            GameObject oldStart = GameObject.Find("START");
            GameObject oldQuit = GameObject.Find("Quit");
            if (oldStart != null) oldStart.SetActive(false);
            if (oldQuit != null) oldQuit.SetActive(false);
        }

        RebuildItems();
        SetSelected(0, true);
    }

    // =====================================================================
    //  Input + animation
    // =====================================================================

    private void Update()
    {
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);

        HandleInput();
        Animate(dt);

        bool showButton = showOpenButton && sceneAllowed && !titleMode && !isOpen && openT <= 0f;
        if (buttonRoot.activeSelf != showButton) buttonRoot.SetActive(showButton);

        // Credits roll by themselves
        if (isOpen && creditsScroll != null && creditsScroll.content != null)
        {
            float range = creditsScroll.content.rect.height - creditsScroll.viewport.rect.height;
            if (range > 1f)
            {
                creditsY = Mathf.Min(range, creditsY + creditsScrollSpeed * dt);
                creditsScroll.verticalNormalizedPosition = 1f - creditsY / range;
            }
        }
    }

    private void HandleInput()
    {
        if (!sceneAllowed) return;

        Keyboard kb = Keyboard.current;
        Gamepad pad = Gamepad.current;

        bool toggle = !titleMode && ((kb != null && kb[openKey].wasPressedThisFrame) || (pad != null && pad.startButton.wasPressedThisFrame));
        if (toggle)
        {
            if (isOpen) Close(); else Open();
            return;
        }

        if (!isOpen) return;

        if (!titleMode && pad != null && pad.buttonEast.wasPressedThisFrame) { Close(); return; }

        int move = 0;
        if (kb != null)
        {
            if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) move = -1;
            if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) move = 1;
        }
        if (pad != null)
        {
            if (pad.dpad.up.wasPressedThisFrame) move = -1;
            if (pad.dpad.down.wasPressedThisFrame) move = 1;
        }
        if (move != 0) SetSelected((selected + move + ids.Length) % ids.Length, false);

        int id = CurrentId;

        if (id == Settings)
        {
            int adjust = 0;
            if (kb != null)
            {
                if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) adjust = -1;
                if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) adjust = 1;
            }
            if (pad != null)
            {
                if (pad.dpad.left.wasPressedThisFrame) adjust = -1;
                if (pad.dpad.right.wasPressedThisFrame) adjust = 1;
            }
            if (adjust != 0 && volumeSlider != null)
                volumeSlider.value = Mathf.Clamp01(volumeSlider.value + adjust * 0.05f);

            bool confirm = (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame))
                        || (pad != null && pad.buttonSouth.wasPressedThisFrame);
            if (confirm) ToggleFullscreen();
        }
        else if (id == NewGame || id == Quit)
        {
            bool confirm = (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))
                        || (pad != null && pad.buttonSouth.wasPressedThisFrame);
            if (confirm) RunTitleAction(id);
        }
        else if (id == MainMenuEntry)
        {
            bool confirm = (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))
                        || (pad != null && pad.buttonSouth.wasPressedThisFrame);
            if (confirm) ReturnToMainMenu();
        }
    }

    private void Animate(float dt)
    {
        float target = isOpen ? 1f : 0f;
        openT = Mathf.MoveTowards(openT, target, dt * 5f);

        if (!isOpen && openT <= 0f)
        {
            if (menuRoot.activeSelf) menuRoot.SetActive(false);
            return;
        }

        float e = 1f - Mathf.Pow(1f - Mathf.Clamp01(openT), 3f);
        slideRoot.anchoredPosition = new Vector2((1f - e) * 1000f, 0f);
        panelRoot.anchoredPosition = panelRest + new Vector2(-(1f - e) * 400f, 0f);
        menuGroup.alpha = Mathf.Clamp01(openT * 2f);

        float k = 1f - Mathf.Exp(-14f * dt);
        for (int i = 0; i < items.Length; i++)
        {
            bool sel = i == selected;
            RectTransform rt = items[i].rectTransform;
            rt.anchoredPosition = Vector2.Lerp(rt.anchoredPosition, ItemTarget(i), k);
            float s = Mathf.Lerp(rt.localScale.x, sel ? 1.25f : 1f, k);
            rt.localScale = new Vector3(s, s, 1f);
            items[i].color = Color.Lerp(items[i].color, sel ? Color.white : Unselected, k);
        }

        // the bright slanted bar slides to the highlighted entry
        RectTransform hr = highlight.rectTransform;
        Vector2 hp = ItemTarget(selected) + new Vector2(-40f, 0f);
        float hw = items[selected].preferredWidth * 1.25f + 90f;
        hr.anchoredPosition = Vector2.Lerp(hr.anchoredPosition, hp, k);
        hr.sizeDelta = new Vector2(Mathf.Lerp(hr.sizeDelta.x, hw, k), 120f);
    }

    // =====================================================================
    //  Open / close
    // =====================================================================

    public void Open()
    {
        if (isOpen || !sceneAllowed) return;

        if (!titleMode)
        {
            if (Time.timeScale == 0f) return; // something else (dialogue, cutscene...) already paused the game

            prevTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            GameSpeed.Paused = true;
        }
        isOpen = true;

        menuRoot.SetActive(true);
        BuildPanel();
    }

    public void Close()
    {
        if (!isOpen) return;
        if (titleMode) return; // the title menu cannot be closed
        isOpen = false;
        RestoreTime();
    }

    public void Toggle()
    {
        if (isOpen) Close(); else Open();
    }

    private void RestoreTime()
    {
        if (GameSpeed.Paused)
        {
            GameSpeed.Paused = false;
            Time.timeScale = prevTimeScale;
        }
    }

    private void SetSelected(int index, bool force)
    {
        if (!force && index == selected) return;
        selected = index;
        saveStatus = "";
        for (int i = 0; i < saveArmed.Length; i++) saveArmed[i] = false;
        bigTitle.text = Names[ids[index]].ToUpper();
        hintText.text = "<size=60%>COMMAND</size>\n" + Hints[ids[index]];
        BuildPanel();
    }

    // =====================================================================
    //  Layout helpers
    // =====================================================================

    private Vector2 ItemRest(int i)
    {
        float y = ((ids.Length - 1) / 2f - i) * 112f;
        return new Vector2(370f - y * 0.21f, y);
    }

    private Vector2 ItemTarget(int i)
    {
        Vector2 p = ItemRest(i);
        if (i == selected) p.x -= 45f;
        return p;
    }

    private static void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static Image NewImage(string name, Transform parent, Color c)
    {
        RectTransform r = NewRect(name, parent);
        Image img = r.gameObject.AddComponent<Image>();
        img.color = c;
        img.raycastTarget = false;
        return img;
    }

    private static TextMeshProUGUI NewText(string name, Transform parent, string text, float size, Color color,
        TextAlignmentOptions align, FontStyles style)
    {
        RectTransform r = NewRect(name, parent);
        TextMeshProUGUI t = r.gameObject.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.fontStyle = style;
        t.raycastTarget = false;
        return t;
    }

    private static Canvas NewCanvas(string name, Transform parent, int order)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = order;
        CanvasScaler scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    private static void SetAnchored(RectTransform r, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        r.anchorMin = r.anchorMax = anchor;
        r.pivot = pivot;
        r.anchoredPosition = pos;
        r.sizeDelta = size;
    }

    // =====================================================================
    //  Building the UI
    // =====================================================================

    private void BuildOpenButton()
    {
        Canvas canvas = NewCanvas("MenuButtonCanvas", transform, 998);
        buttonRoot = canvas.gameObject;

        Image img = NewImage("MenuButton", canvas.transform, new Color(0.1f, 0.05f, 0.2f, 0.75f));
        img.raycastTarget = true;
        SetAnchored(img.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -30f), new Vector2(120f, 60f));

        Button btn = img.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(Open);
        InteractiveUIButton hover = img.gameObject.AddComponent<InteractiveUIButton>();
        hover.enableWiggleOnUnsupportedClick = false;

        TextMeshProUGUI label = NewText("Label", img.transform, "MENU", 30f, Color.white, TextAlignmentOptions.Center,
            FontStyles.Bold | FontStyles.Italic);
        Stretch(label.rectTransform);
    }

    private void BuildMenu()
    {
        Canvas canvas = NewCanvas("GameMenuCanvas", transform, 1000);
        menuRoot = canvas.gameObject;
        menuGroup = menuRoot.AddComponent<CanvasGroup>();
        RectTransform canvasRect = (RectTransform)canvas.transform;

        // light background (also blocks clicks to whatever is underneath)
        Image bg = NewImage("Background", canvasRect, Lavender);
        bg.raycastTarget = true;
        Stretch(bg.rectTransform);
        bgImage = bg;
        List<GameObject> decorList = new List<GameObject>();

        // a few drifting lavender / violet shards for the shattered look
        decorList.Add(BuildShard(canvasRect, new Vector2(-700f, 330f), new Vector2(900f, 90f), 28f, new Color(1f, 1f, 1f, 0.55f)));
        decorList.Add(BuildShard(canvasRect, new Vector2(-420f, 430f), new Vector2(700f, 50f), 28f, new Color(0.62f, 0.5f, 0.95f, 0.45f)));
        decorList.Add(BuildShard(canvasRect, new Vector2(-760f, -380f), new Vector2(800f, 70f), 28f, new Color(0.62f, 0.5f, 0.95f, 0.35f)));
        decorList.Add(BuildShard(canvasRect, new Vector2(-300f, -470f), new Vector2(900f, 40f), 28f, new Color(1f, 1f, 1f, 0.5f)));

        // huge faded title of the selected entry
        bigTitle = NewText("BigTitle", canvasRect, "", 230f, new Color(0.45f, 0.3f, 0.8f, 0.14f),
            TextAlignmentOptions.Left, FontStyles.Bold | FontStyles.Italic);
        SetAnchored(bigTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(-940f, 330f), new Vector2(1500f, 300f));
        bigTitle.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Slant);
        bigTitle.enableAutoSizing = true;
        bigTitle.fontSizeMax = 230f;
        bigTitle.fontSizeMin = 90f;
        decorList.Add(bigTitle.gameObject);

        // content panel on the left
        BuildPanelShell(canvasRect);

        // top-left hint
        hintText = NewText("Hint", canvasRect, "", 34f, Ink, TextAlignmentOptions.TopLeft, FontStyles.Italic);
        SetAnchored(hintText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(45f, -30f), new Vector2(900f, 100f));
        decorList.Add(hintText.gameObject);

        // everything on the dark side slides in together
        slideRoot = NewRect("SlideRoot", canvasRect);
        Stretch(slideRoot);

        Image wedge = NewImage("DarkWedge", slideRoot, DeepPurple);
        SetAnchored(wedge.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(1020f, 0f), new Vector2(1400f, 2800f));
        wedge.rectTransform.localRotation = Quaternion.Euler(0f, 0f, WedgeAngle);

        Image edge = NewImage("WedgeEdge", slideRoot, Accent);
        SetAnchored(edge.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(314f, 0f), new Vector2(12f, 2800f));
        edge.rectTransform.localRotation = Quaternion.Euler(0f, 0f, WedgeAngle);

        highlight = NewImage("Highlight", slideRoot, Accent);
        highlight.rectTransform.anchorMin = highlight.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        highlight.rectTransform.pivot = new Vector2(0f, 0.5f);
        highlight.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Slant);

        // footer hints
        TextMeshProUGUI footer = NewText("Footer", slideRoot, "ENTER  Select        ESC  Close", 30f,
            new Color(0.78f, 0.72f, 0.92f, 1f), TextAlignmentOptions.Right, FontStyles.Italic);
        SetAnchored(footer.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-45f, 30f), new Vector2(700f, 50f));
        footerText = footer;

        // close button (for mouse players)
        Image closeImg = NewImage("CloseButton", canvasRect, new Color(0.1f, 0.05f, 0.2f, 0.85f));
        closeImg.raycastTarget = true;
        closeButtonObj = closeImg.gameObject;
        SetAnchored(closeImg.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-30f, -30f), new Vector2(150f, 60f));
        Button closeBtn = closeImg.gameObject.AddComponent<Button>();
        closeBtn.targetGraphic = closeImg;
        closeBtn.onClick.AddListener(Close);
        closeImg.gameObject.AddComponent<InteractiveUIButton>().enableWiggleOnUnsupportedClick = false;
        TextMeshProUGUI closeLabel = NewText("Label", closeImg.transform, "CLOSE", 30f, Color.white,
            TextAlignmentOptions.Center, FontStyles.Bold | FontStyles.Italic);
        Stretch(closeLabel.rectTransform);

        decor = decorList.ToArray();
    }

    private void RebuildItems()
    {
        if (items != null)
            for (int i = 0; i < items.Length; i++)
                if (items[i] != null) Destroy(items[i].gameObject);

        items = new TextMeshProUGUI[ids.Length];
        for (int i = 0; i < ids.Length; i++)
        {
            int index = i;
            int id = ids[i];
            TextMeshProUGUI t = NewText("Item_" + Names[id], slideRoot, Names[id].ToUpper(), 68f, Unselected,
                TextAlignmentOptions.Left, FontStyles.Bold | FontStyles.Italic);
            t.raycastTarget = true;
            SetAnchored(t.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), ItemRest(i), new Vector2(900f, 110f));
            t.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Slant);

            GameMenuItemHover h = t.gameObject.AddComponent<GameMenuItemHover>();
            h.onEnter = () => SetSelected(index, false);
            h.onClick = () =>
            {
                SetSelected(index, false);
                if (titleMode) RunTitleAction(id);
            };
            items[i] = t;
        }
    }

    // New Game / Quit on the title screen
    private void RunTitleAction(int id)
    {
        if (id == NewGame)
        {
            SaveSystem.ResetProgress(); // a new game never inherits progress from a previous session

            // hide this menu so the intro video plays on its own
            isOpen = false;
            openT = 0f;
            menuRoot.SetActive(false);

            MainMenu mm = FindFirstObjectByType<MainMenu>();
            if (mm != null) mm.StartGame();
            else SceneManager.LoadScene("GameScene");
        }
        else if (id == Quit)
        {
            MainMenu mm = FindFirstObjectByType<MainMenu>();
            if (mm != null) mm.QuitGame();
            else Application.Quit();
        }
    }

    private GameObject BuildShard(Transform parent, Vector2 pos, Vector2 size, float angle, Color c)
    {
        Image shard = NewImage("Shard", parent, c);
        SetAnchored(shard.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
        shard.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
        return shard.gameObject;
    }

    private void BuildPanelShell(Transform parent)
    {
        panelRoot = NewRect("Panel", parent);
        SetAnchored(panelRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), panelRest, new Vector2(880f, 760f));

        Image bg = panelRoot.gameObject.AddComponent<Image>();
        bg.color = new Color(1f, 1f, 1f, 0.6f);
        bg.raycastTarget = false;
        panelBg = bg;

        Image bar = NewImage("HeaderBar", panelRoot, Accent);
        SetAnchored(bar.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(880f, 10f));

        headerText = NewText("Header", panelRoot, "", 58f, Ink, TextAlignmentOptions.TopLeft, FontStyles.Bold | FontStyles.Italic);
        SetAnchored(headerText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -25f), new Vector2(820f, 80f));

        body = NewRect("Body", panelRoot);
        body.anchorMin = Vector2.zero;
        body.anchorMax = Vector2.one;
        body.offsetMin = new Vector2(30f, 30f);
        body.offsetMax = new Vector2(-30f, -115f);
    }

    // =====================================================================
    //  Panel contents
    // =====================================================================

    private void BuildPanel()
    {
        if (body == null) return;

        for (int i = body.childCount - 1; i >= 0; i--)
        {
            body.GetChild(i).gameObject.SetActive(false);
            Destroy(body.GetChild(i).gameObject);
        }
        creditsScroll = null;
        volumeSlider = null;
        volumeValue = null;
        fullscreenLabel = null;
        creditsY = 0f;

        int id = CurrentId;
        bool hasPanel = id != NewGame && id != Quit;
        panelRoot.gameObject.SetActive(hasPanel);
        if (!hasPanel) return;

        headerText.text = Names[id].ToUpper();

        switch (id)
        {
            case Continue: BuildSaveLoadPanel(); break;
            case Inventory: BuildScrollText(InventoryText(), false); break;
            case Stats: BuildScrollText(StatsText(), false); break;
            case Quests: BuildScrollText(QuestText(), false); break;
            case SaveLoad: BuildSaveLoadPanel(); break;
            case Credits: BuildScrollText("\n\n\n" + creditsText + "\n\n\n\n\n\n", true); break;
            case Settings: BuildSettings(); break;
            case MainMenuEntry: BuildMainMenuPanel(); break;
        }
    }

    private ScrollRect BuildScrollText(string text, bool centered)
    {
        RectTransform sv = NewRect("Scroll", body);
        Stretch(sv);
        Image catcher = sv.gameObject.AddComponent<Image>(); // lets the mouse wheel / drag reach the ScrollRect
        catcher.color = new Color(0f, 0f, 0f, 0f);
        ScrollRect scroll = sv.gameObject.AddComponent<ScrollRect>();

        RectTransform viewport = NewRect("Viewport", sv);
        Stretch(viewport);
        viewport.gameObject.AddComponent<RectMask2D>();

        TextMeshProUGUI t = NewText("Content", viewport, text, centered ? 40f : 34f, Ink,
            centered ? TextAlignmentOptions.Top : TextAlignmentOptions.TopLeft, FontStyles.Normal);
        t.richText = true;
        RectTransform tr = t.rectTransform;
        tr.anchorMin = new Vector2(0f, 1f);
        tr.anchorMax = new Vector2(1f, 1f);
        tr.pivot = new Vector2(0.5f, 1f);
        tr.anchoredPosition = Vector2.zero;
        tr.sizeDelta = Vector2.zero;
        ContentSizeFitter fit = t.gameObject.AddComponent<ContentSizeFitter>();
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = viewport;
        scroll.content = tr;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40f;

        if (centered) creditsScroll = scroll;
        return scroll;
    }

    private void BuildSettings()
    {
        // ---- master volume
        TextMeshProUGUI volLabel = NewText("VolumeLabel", body, "MASTER VOLUME", 36f, Ink, TextAlignmentOptions.TopLeft, FontStyles.Bold);
        SetAnchored(volLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(600f, 50f));

        volumeSlider = MakeSlider(body, new Vector2(0f, -65f), new Vector2(560f, 40f));
        volumeValue = NewText("VolumeValue", body, "", 36f, Ink, TextAlignmentOptions.Left, FontStyles.Bold);
        SetAnchored(volumeValue.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(590f, -62f), new Vector2(180f, 46f));

        volumeSlider.SetValueWithoutNotify(AudioListener.volume);
        volumeValue.text = Mathf.RoundToInt(AudioListener.volume * 100f) + "%";
        volumeSlider.onValueChanged.AddListener(OnVolumeChanged);

        // ---- fullscreen
        Image btnImg = NewImage("FullscreenButton", body, Accent);
        btnImg.raycastTarget = true;
        SetAnchored(btnImg.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -170f), new Vector2(430f, 70f));
        Button btn = btnImg.gameObject.AddComponent<Button>();
        btn.targetGraphic = btnImg;
        btn.onClick.AddListener(ToggleFullscreen);
        btnImg.gameObject.AddComponent<InteractiveUIButton>().enableWiggleOnUnsupportedClick = false;

        fullscreenLabel = NewText("Label", btnImg.transform, "", 34f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold | FontStyles.Italic);
        Stretch(fullscreenLabel.rectTransform);
        fullscreenLabel.text = "FULLSCREEN: " + (Screen.fullScreen ? "ON" : "OFF");

        TextMeshProUGUI tip = NewText("Tip", body, "Left / Right: volume      Enter: fullscreen", 28f,
            new Color(0.16f, 0.06f, 0.33f, 0.6f), TextAlignmentOptions.TopLeft, FontStyles.Italic);
        SetAnchored(tip.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -275f), new Vector2(800f, 50f));
    }

    // ---------------------------------------------------------------- save / load
    private Button MakeButton(Transform parent, string label, Color color, Vector2 pos, Vector2 size, Action onClick, out TextMeshProUGUI text)
    {
        Image img = NewImage("Button_" + label, parent, color);
        img.raycastTarget = true;
        SetAnchored(img.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), pos, size);
        Button btn = img.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(() => onClick());
        img.gameObject.AddComponent<InteractiveUIButton>().enableWiggleOnUnsupportedClick = false;

        text = NewText("Label", img.transform, label, 30f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold | FontStyles.Italic);
        Stretch(text.rectTransform);
        return btn;
    }

    private void BuildSaveLoadPanel()
    {
        bool canSave = !titleMode && SaveSystem.CanSaveHere;
        Color disabled = new Color(0.6f, 0.55f, 0.68f, 1f);
        bool anySave = false;

        for (int i = 0; i < SaveSystem.SlotCount; i++)
        {
            int slot = i + 1;
            float y = -i * 165f;
            SaveFile info = SaveSystem.GetInfo(slot);
            if (info != null) anySave = true;

            Image row = NewImage("Slot" + slot, body, new Color(1f, 1f, 1f, 0.55f));
            SetAnchored(row.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, y), new Vector2(820f, 150f));

            string title = "<b>SLOT " + slot + "</b>\n";
            string sub = info == null
                ? "<size=75%><i>Empty</i></size>"
                : "<size=75%>" + info.scene + "   " + info.savedAt + "\n" + info.summary + "</size>";
            TextMeshProUGUI infoText = NewText("Info", row.transform, title + sub, 36f, Ink, TextAlignmentOptions.Left, FontStyles.Normal);
            SetAnchored(infoText.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(25f, 0f), new Vector2(470f, 130f));

            // SAVE does not exist on the title screen, there is nothing to save yet
            if (!titleMode)
            {
                TextMeshProUGUI saveLabel;
                Button saveBtn = MakeButton(row.transform, "SAVE", canSave ? Accent : disabled, new Vector2(510f, -40f), new Vector2(140f, 70f),
                    () => OnSaveClicked(slot), out saveLabel);
                saveBtn.interactable = canSave;
                if (info != null && saveArmed[i]) saveLabel.text = "SURE?";
            }

            TextMeshProUGUI loadLabel;
            Button loadBtn = MakeButton(row.transform, "LOAD", info != null ? DeepPurple : disabled, new Vector2(665f, -40f), new Vector2(140f, 70f),
                () => OnLoadClicked(slot), out loadLabel);
            loadBtn.interactable = info != null;
        }

        string status = saveStatus;
        if (string.IsNullOrEmpty(status))
        {
            if (titleMode) status = anySave ? "Choose a save to continue." : "No saved games yet. Pick NEW GAME to begin.";
            else if (!canSave) status = "You can only save in the overworld, not during a fight.";
        }
        TextMeshProUGUI st = NewText("Status", body, status, 30f, new Color(0.16f, 0.06f, 0.33f, 0.75f), TextAlignmentOptions.TopLeft, FontStyles.Italic);
        SetAnchored(st.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -505f), new Vector2(820f, 70f));
    }

    private void OnSaveClicked(int slot)
    {
        int i = slot - 1;
        if (SaveSystem.HasSave(slot) && !saveArmed[i])
        {
            // asking before overwriting
            for (int k = 0; k < saveArmed.Length; k++) saveArmed[k] = false;
            saveArmed[i] = true;
            saveStatus = "Press SAVE again to overwrite slot " + slot + ".";
            BuildPanel();
            return;
        }

        for (int k = 0; k < saveArmed.Length; k++) saveArmed[k] = false;
        saveStatus = SaveSystem.Save(slot) ? "Saved to slot " + slot + "." : "Could not save.";
        BuildPanel();
    }

    private void OnLoadClicked(int slot)
    {
        if (!SaveSystem.HasSave(slot)) return;
        Close(); // un-pauses first so the loaded game starts at normal speed
        if (!SaveSystem.Load(slot))
        {
            saveStatus = "Could not load slot " + slot + ".";
        }
    }

    private void BuildMainMenuPanel()
    {
        TextMeshProUGUI warn = NewText("Warning", body,
            "Return to the title screen?\n<size=80%>Anything you have not finished will be lost.</size>",
            40f, Ink, TextAlignmentOptions.TopLeft, FontStyles.Bold);
        SetAnchored(warn.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(800f, 160f));

        Image btnImg = NewImage("ReturnButton", body, Accent);
        btnImg.raycastTarget = true;
        SetAnchored(btnImg.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -190f), new Vector2(520f, 80f));
        Button btn = btnImg.gameObject.AddComponent<Button>();
        btn.targetGraphic = btnImg;
        btn.onClick.AddListener(ReturnToMainMenu);
        btnImg.gameObject.AddComponent<InteractiveUIButton>().enableWiggleOnUnsupportedClick = false;

        TextMeshProUGUI label = NewText("Label", btnImg.transform, "RETURN TO MAIN MENU", 36f, Color.white,
            TextAlignmentOptions.Center, FontStyles.Bold | FontStyles.Italic);
        Stretch(label.rectTransform);

        TextMeshProUGUI tip = NewText("Tip", body, "Click the button or press Enter. Esc to stay.", 28f,
            new Color(0.16f, 0.06f, 0.33f, 0.6f), TextAlignmentOptions.TopLeft, FontStyles.Italic);
        SetAnchored(tip.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -295f), new Vector2(800f, 50f));
    }

    public void ReturnToMainMenu()
    {
        if (!isOpen) return;
        Close(); // un-pauses first so the next scene starts at normal speed
        SceneManager.LoadScene(mainMenuScene);
    }

    private Slider MakeSlider(Transform parent, Vector2 pos, Vector2 size)
    {
        RectTransform root = NewRect("VolumeSlider", parent);
        SetAnchored(root, new Vector2(0f, 1f), new Vector2(0f, 1f), pos, size);

        Image track = NewImage("Track", root, new Color(0.25f, 0.12f, 0.5f, 0.3f));
        track.rectTransform.anchorMin = new Vector2(0f, 0.3f);
        track.rectTransform.anchorMax = new Vector2(1f, 0.7f);
        track.rectTransform.offsetMin = track.rectTransform.offsetMax = Vector2.zero;

        RectTransform fillArea = NewRect("FillArea", root);
        fillArea.anchorMin = new Vector2(0f, 0.3f);
        fillArea.anchorMax = new Vector2(1f, 0.7f);
        fillArea.offsetMin = new Vector2(5f, 0f);
        fillArea.offsetMax = new Vector2(-15f, 0f);

        Image fill = NewImage("Fill", fillArea, Accent);
        fill.rectTransform.sizeDelta = new Vector2(10f, 0f);

        RectTransform handleArea = NewRect("HandleSlideArea", root);
        handleArea.anchorMin = Vector2.zero;
        handleArea.anchorMax = Vector2.one;
        handleArea.offsetMin = new Vector2(14f, 0f);
        handleArea.offsetMax = new Vector2(-14f, 0f);

        Image handle = NewImage("Handle", handleArea, DeepPurple);
        handle.raycastTarget = true;
        handle.rectTransform.anchorMin = new Vector2(0f, 0f);
        handle.rectTransform.anchorMax = new Vector2(0f, 1f);
        handle.rectTransform.sizeDelta = new Vector2(28f, 0f);

        Slider s = root.gameObject.AddComponent<Slider>();
        s.fillRect = fill.rectTransform;
        s.handleRect = handle.rectTransform;
        s.targetGraphic = handle;
        s.direction = Slider.Direction.LeftToRight;
        s.minValue = 0f;
        s.maxValue = 1f;
        s.wholeNumbers = false;
        return s;
    }

    private void OnVolumeChanged(float v)
    {
        AudioListener.volume = v;
        PlayerPrefs.SetFloat(VolumeKey, v);
        if (volumeValue != null) volumeValue.text = Mathf.RoundToInt(v * 100f) + "%";
    }

    private void ToggleFullscreen()
    {
        bool fs = !Screen.fullScreen;
        Screen.fullScreen = fs;
        if (fullscreenLabel != null) fullscreenLabel.text = "FULLSCREEN: " + (fs ? "ON" : "OFF");
    }

    // ---------------------------------------------------------------- inventory
    private string InventoryText()
    {
        StringBuilder sb = new StringBuilder();
        int count = 0;
        try
        {
            foreach (var trinket in TrinketInventory.Owned)
            {
                if (trinket == null) continue;
                count++;
                string title = ReadText(trinket, "displayName", "trinketName", "itemName", "title");
                if (string.IsNullOrEmpty(title))
                {
                    UnityEngine.Object uo = trinket as UnityEngine.Object;
                    title = uo != null ? uo.name : trinket.ToString();
                }
                string desc = ReadText(trinket, "description", "desc", "effectDescription");

                sb.Append("<color=#7B52E0>◆</color> <b>").Append(title).Append("</b>\n");
                if (!string.IsNullOrEmpty(desc)) sb.Append("<size=80%>").Append(desc).Append("</size>\n");
                sb.Append("\n");
            }
        }
        catch (Exception) { }

        if (count == 0) return "<i>You are not carrying anything yet.</i>";
        return sb.ToString();
    }

    // ---------------------------------------------------------------- stats
    private string StatsText()
    {
        StringBuilder sb = new StringBuilder();

        Heading(sb, "Progress");
        Line(sb, "Stars", ReadStars);

        Heading(sb, "Boons");
        Line(sb, "Extra hands", () => Signed(BoonProgress.ExtraHands));
        Line(sb, "Extra hand size", () => Signed(BoonProgress.ExtraHandSize));
        Line(sb, "Wind card power", () => Signed(BoonProgress.BonusWindPower));

        Heading(sb, "Mood");
        Line(sb, "Power", () => Signed(MoodManager.PowerBonus));
        Line(sb, "Hands", () => Signed(MoodManager.HandsModifier));
        Line(sb, "Reshuffles", () => Signed(MoodManager.ReshufflesModifier));
        Line(sb, "Score multiplier", () => "x" + MoodManager.ScoreMultiplier.ToString("0.##"));

        Heading(sb, "Structures");
        Line(sb, "Solar Panel power", () => Signed(StructureProgress.PowerBonus(StructureType.Solar)));
        Line(sb, "Solar Panel mult", () => Signed(StructureProgress.MultBonus(StructureType.Solar)));
        Line(sb, "Windmill power", () => Signed(StructureProgress.PowerBonus(StructureType.Wind)));
        Line(sb, "Windmill mult", () => Signed(StructureProgress.MultBonus(StructureType.Wind)));

        return sb.ToString();
    }

    private static void Heading(StringBuilder sb, string text)
    {
        if (sb.Length > 0) sb.Append("\n");
        sb.Append("<color=#7B52E0><b>").Append(text.ToUpper()).Append("</b></color>\n");
    }

    // A line that can never break the menu: if the value can't be read it is simply skipped
    private static void Line(StringBuilder sb, string label, Func<string> value)
    {
        try
        {
            string v = value();
            if (v == null) return;
            sb.Append(label).Append("   <b>").Append(v).Append("</b>\n");
        }
        catch (Exception) { }
    }

    private static string Signed(float v)
    {
        return (v >= 0f ? "+" : "") + v.ToString("0.##");
    }

    private static string ReadStars()
    {
        Type t = typeof(PlayerProgress);
        BindingFlags f = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        string[] names = { "Stars", "stars", "TotalStars", "totalStars", "StarCount", "starCount" };
        foreach (string n in names)
        {
            PropertyInfo p = t.GetProperty(n, f);
            if (p != null && p.CanRead) return p.GetValue(null, null).ToString();
            FieldInfo fi = t.GetField(n, f);
            if (fi != null) return fi.GetValue(null).ToString();
        }
        return null;
    }

    private static string ReadText(object o, params string[] names)
    {
        if (o == null) return null;
        Type t = o.GetType();
        BindingFlags f = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        foreach (string n in names)
        {
            FieldInfo fi = t.GetField(n, f);
            if (fi != null && fi.FieldType == typeof(string))
            {
                string s = fi.GetValue(o) as string;
                if (!string.IsNullOrEmpty(s)) return s;
            }
            PropertyInfo pi = t.GetProperty(n, f);
            if (pi != null && pi.CanRead && pi.PropertyType == typeof(string))
            {
                string s = pi.GetValue(o, null) as string;
                if (!string.IsNullOrEmpty(s)) return s;
            }
        }
        return null;
    }

    // ---------------------------------------------------------------- quests
    private string QuestText()
    {
        if (QuestLog.All.Count == 0) return "<i>No quests yet.</i>";

        StringBuilder sb = new StringBuilder();
        bool any = false;

        foreach (var q in QuestLog.All)
        {
            if (q.completed) continue;
            any = true;
            sb.Append("<color=#7B52E0>◆</color> <b>").Append(q.title).Append("</b>\n");
            if (!string.IsNullOrEmpty(q.description)) sb.Append("<size=80%>").Append(q.description).Append("</size>\n");
            sb.Append("\n");
        }
        if (!any) sb.Append("<i>All quests complete!</i>\n\n");

        bool header = false;
        foreach (var q in QuestLog.All)
        {
            if (!q.completed) continue;
            if (!header)
            {
                Heading(sb, "Completed");
                header = true;
            }
            sb.Append("<alpha=#99><s>").Append(q.title).Append("</s><alpha=#FF>\n");
        }
        return sb.ToString();
    }
}