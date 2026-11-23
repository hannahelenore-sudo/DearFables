using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#pragma warning disable 618

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

public class GameMenuItemHover : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
{
    public Action onEnter;
    public Action onClick;
    public void OnPointerEnter(PointerEventData eventData) { if (onEnter != null) onEnter(); }
    public void OnPointerClick(PointerEventData eventData) { if (onClick != null) onClick(); }
}

public static class StarFrames
{
    private const int Size = 128, Super = 2, Around = 40, Rings = 6, Points = 5;
    private const float Puff = 0.45f;

    private static Sprite cache;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { cache = null; }

    public static Sprite Get()
    {
        if (cache != null) return cache;
        try { cache = Generate(1, true)[0]; }
        catch (Exception e) { Debug.LogWarning("GameMenu: could not build the star: " + e.Message); }
        return cache;
    }

    private static int Index(int side, int ring, int k) { return (side * (Rings + 1) + ring) * Around + (k % Around); }

    private static float Radius(float th)
    {
        float t = Mathf.Max(0f, (1f + Mathf.Cos(Points * (th - Mathf.PI * 0.5f))) * 0.5f);
        return 0.58f + 0.42f * Mathf.Pow(t, 0.9f);
    }

    private static Vector3 Pt(int side, float phi, float th)
    {
        float rho = Mathf.Sin(phi) * Radius(th);
        float z = Puff * Mathf.Cos(phi);
        return new Vector3(rho * Mathf.Cos(th), rho * Mathf.Sin(th), side == 0 ? z : -z);
    }

    private static Sprite[] Generate(int frameCount, bool flat)
    {
        int n = 2 * (Rings + 1) * Around;
        Vector3[] pos = new Vector3[n];
        Vector3[] nrm = new Vector3[n];

        for (int side = 0; side < 2; side++)
        {
            for (int r = 0; r <= Rings; r++)
            {
                float phi = r / (float)Rings * Mathf.PI * 0.5f;
                for (int k = 0; k < Around; k++)
                {
                    float th = k / (float)Around * Mathf.PI * 2f;
                    Vector3 p = Pt(side, phi, th);
                    Vector3 nr;
                    if (r == 0)
                    {
                        nr = new Vector3(0f, 0f, side == 0 ? 1f : -1f);
                    }
                    else
                    {
                        Vector3 dPhi = Pt(side, Mathf.Min(phi + 0.02f, Mathf.PI * 0.5f), th) - Pt(side, phi - 0.02f, th);
                        Vector3 dTh = Pt(side, phi, th + 0.01f) - Pt(side, phi, th - 0.01f);
                        nr = Vector3.Cross(dPhi, dTh).normalized;
                        if (Vector3.Dot(nr, p) < 0f) nr = -nr;
                    }
                    pos[Index(side, r, k)] = p;
                    nrm[Index(side, r, k)] = nr;
                }
            }
        }

        List<int> triList = new List<int>();
        for (int side = 0; side < 2; side++)
        {
            for (int r = 0; r < Rings; r++)
            {
                for (int k = 0; k < Around; k++)
                {
                    int a = Index(side, r, k), b = Index(side, r, k + 1);
                    int c = Index(side, r + 1, k), d = Index(side, r + 1, k + 1);
                    if (r == 0) { triList.Add(a); triList.Add(c); triList.Add(d); }
                    else { triList.Add(a); triList.Add(b); triList.Add(c); triList.Add(b); triList.Add(d); triList.Add(c); }
                }
            }
        }
        int[] tris = triList.ToArray();
        int triCount = tris.Length / 3;

        Vector3 light = new Vector3(-0.45f, 0.55f, 0.70f).normalized;
        Vector3 half = (light + new Vector3(0f, 0f, 1f)).normalized;
        Color baseColor = new Color(1f, 0.745f, 0.137f, 1f);
        Color shadeColor = new Color(0.90f, 0.55f, 0.08f, 1f);
        int faceLimit = (Rings + 1) * Around;

        int S = Size * Super;
        float scale = S * 0.5f * 0.92f;
        float lean = flat ? 0f : 14f * Mathf.Deg2Rad;
        float sx = Mathf.Sin(lean), cx = Mathf.Cos(lean);

        Vector3[] rp = new Vector3[n];
        Vector3[] rn = new Vector3[n];
        Color[] vc = new Color[n];
        Vector2[] sp = new Vector2[n];
        float[] keys = new float[triCount];
        int[] order = new int[triCount];
        Color32[] hi = new Color32[S * S];
        Color32[] lo = new Color32[Size * Size];

        Sprite[] frames = new Sprite[frameCount];
        for (int f = 0; f < frameCount; f++)
        {
            float yaw = f / (float)frameCount * Mathf.PI * 2f;
            float sy = Mathf.Sin(yaw), cy = Mathf.Cos(yaw);

            for (int i = 0; i < n; i++)
            {
                Vector3 p = Rotate(pos[i], sy, cy, sx, cx);
                Vector3 nr = Rotate(nrm[i], sy, cy, sx, cx);
                rp[i] = p;
                rn[i] = nr;

                float diff = Mathf.Max(0f, Vector3.Dot(nr, light));
                float shade = flat ? 1f : Mathf.Clamp01(0.62f + 0.50f * diff);
                float spec = flat ? 0f : Mathf.Pow(Mathf.Max(0f, Vector3.Dot(nr, half)), 22f) * 0.25f;
                Color col = Color.Lerp(shadeColor, baseColor, shade);
                vc[i] = new Color(col.r + spec, col.g + spec, col.b + spec, 1f);
                sp[i] = new Vector2(S * 0.5f + p.x * scale, S * 0.5f + p.y * scale);
            }

            int count = 0;
            for (int t = 0; t < triCount; t++)
            {
                int a = tris[t * 3], b = tris[t * 3 + 1], c = tris[t * 3 + 2];
                float nz = (rn[a].z + rn[b].z + rn[c].z) / 3f;
                if (nz < -0.12f) continue;
                keys[count] = (rp[a].z + rp[b].z + rp[c].z) / 3f;
                order[count] = t;
                count++;
            }
            Array.Sort(keys, order, 0, count);

            for (int i = 0; i < hi.Length; i++) hi[i] = new Color32(0, 0, 0, 0);
            for (int i = 0; i < count; i++)
            {
                int t = order[i];
                int a = tris[t * 3], b = tris[t * 3 + 1], c = tris[t * 3 + 2];
                FillTriangle(hi, S, sp[a], sp[b], sp[c], vc[a], vc[b], vc[c], pos[a], pos[b], pos[c], a < faceLimit);
            }

            AddOutline(hi, S);

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float r = 0f, g = 0f, bl = 0f, al = 0f;
                    for (int oy = 0; oy < Super; oy++)
                    {
                        for (int ox = 0; ox < Super; ox++)
                        {
                            Color32 px = hi[(y * Super + oy) * S + (x * Super + ox)];
                            if (px.a == 0) continue;
                            r += px.r; g += px.g; bl += px.b; al += 1f;
                        }
                    }
                    if (al <= 0f) { lo[y * Size + x] = new Color32(0, 0, 0, 0); continue; }
                    lo[y * Size + x] = new Color32((byte)(r / al), (byte)(g / al), (byte)(bl / al),
                                                   (byte)(255f * al / (Super * Super)));
                }
            }

            Texture2D tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            tex.SetPixels32(lo);
            tex.Apply();
            frames[f] = Sprite.Create(tex, new Rect(0f, 0f, Size, Size), new Vector2(0.5f, 0.5f), 100f);
            frames[f].name = "StarFrame" + f;
        }
        return frames;
    }

    private static Vector3 Rotate(Vector3 v, float sy, float cy, float sx, float cx)
    {
        float x = v.x * cy + v.z * sy;
        float z = -v.x * sy + v.z * cy;
        float y = v.y * cx - z * sx;
        z = v.y * sx + z * cx;
        return new Vector3(x, y, z);
    }

    private static Color ApplyFace(float u, float v, Color col)
    {
        float cx = (Mathf.Abs(u) - 0.36f) / 0.10f, cy = (v + 0.12f) / 0.065f;
        if (cx * cx + cy * cy < 1f) col = Color.Lerp(col, new Color(1f, 0.55f, 0.25f, 1f), 0.7f);

        Color ink = new Color(0.22f, 0.09f, 0.03f, 1f);
        float ex = (Mathf.Abs(u) - 0.24f) / 0.065f, ey = (v - 0.03f) / 0.10f;
        if (ex * ex + ey * ey < 1f) col = ink;

        if (Mathf.Abs(u) < 0.11f)
        {
            float k = u / 0.11f;
            float mouth = -0.20f + 0.07f * k * k;
            if (Mathf.Abs(v - mouth) < 0.02f) col = ink;
        }
        return col;
    }

    private static void AddOutline(Color32[] buf, int S)
    {
        List<int> edge = new List<int>();
        for (int y = 1; y < S - 1; y++)
        {
            for (int x = 1; x < S - 1; x++)
            {
                int i = y * S + x;
                if (buf[i].a == 0) continue;
                if (buf[i - 1].a == 0 || buf[i + 1].a == 0 || buf[i - S].a == 0 || buf[i + S].a == 0) edge.Add(i);
            }
        }

        Color32 line = new Color32(92, 41, 10, 255);
        for (int e = 0; e < edge.Count; e++)
        {
            int ex = edge[e] % S, ey = edge[e] / S;
            for (int dy = -4; dy <= 4; dy++)
            {
                for (int dx = -4; dx <= 4; dx++)
                {
                    if (dx * dx + dy * dy > 16) continue;
                    int nx = ex + dx, ny = ey + dy;
                    if (nx < 0 || ny < 0 || nx >= S || ny >= S) continue;
                    int j = ny * S + nx;
                    if (buf[j].a == 0) buf[j] = line;
                }
            }
        }
    }

    private static byte ToByte(float v)
    {
        return (byte)Mathf.Clamp(Mathf.RoundToInt(v * 255f), 0, 255);
    }

    private static void FillTriangle(Color32[] buf, int S, Vector2 a, Vector2 b, Vector2 c, Color ca, Color cb, Color cc,
        Vector3 ma, Vector3 mb, Vector3 mc, bool face)
    {
        float area = (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
        if (Mathf.Abs(area) < 1e-5f) return;
        float inv = 1f / area;

        int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x))));
        int x1 = Mathf.Min(S - 1, Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x))));
        int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y))));
        int y1 = Mathf.Min(S - 1, Mathf.CeilToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y))));

        for (int y = y0; y <= y1; y++)
        {
            for (int x = x0; x <= x1; x++)
            {
                float px = x + 0.5f, py = y + 0.5f;
                float w0 = ((b.x - px) * (c.y - py) - (b.y - py) * (c.x - px)) * inv;
                float w1 = ((c.x - px) * (a.y - py) - (c.y - py) * (a.x - px)) * inv;
                float w2 = 1f - w0 - w1;
                if (w0 < -0.02f || w1 < -0.02f || w2 < -0.02f) continue;
                Color col = new Color(ca.r * w0 + cb.r * w1 + cc.r * w2,
                                      ca.g * w0 + cb.g * w1 + cc.g * w2,
                                      ca.b * w0 + cb.b * w1 + cc.b * w2, 1f);
                if (face)
                    col = ApplyFace(ma.x * w0 + mb.x * w1 + mc.x * w2, ma.y * w0 + mb.y * w1 + mc.y * w2, col);
                buf[y * S + x] = new Color32(ToByte(col.r), ToByte(col.g), ToByte(col.b), 255);
            }
        }
    }
}

public class GameMenu : MonoBehaviour
{
    public static GameMenu Instance { get; private set; }

    [Header("Where the menu is available (scene names)")]
    public string[] activeScenes = { "GameScene", "CardGame" };

    [Header("Return to main menu")]
    public string mainMenuScene = "MainMenu";

    [Header("Controls")]
    public Key openKey = Key.Escape;
    public bool showOpenButton = false;

    private static readonly Color Navy = new Color(47f / 255f, 51f / 255f, 105f / 255f, 1f);
    private static readonly Color CardBlue = new Color(170f / 255f, 230f / 255f, 251f / 255f, 1f);
    private static readonly Color BoxFill = new Color(0.95f, 0.99f, 1f, 1f);
    private static readonly Color StarYellow = new Color(0.98f, 0.76f, 0.28f, 1f);
    private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.6f);

    private const string ResPath = "MenuUI/";
    private const string VolumeKey = "menu_master_volume";

    private const float CloudW = 1100f;
    private static readonly string[] CloudSprites = { "StatsF", "SaveLoadF", "Settings", "Main_Menu" };
    private static readonly float[] CloudH = { 508f, 579f, 353f, 300f };
    private static readonly float[] CloudY = { 0f, 80f, 561f, 780f };
    private static readonly Vector2[] LabelPos = { new Vector2(452f, 121f), new Vector2(560f, 375f), new Vector2(640f, 139f), new Vector2(600f, 170f) };
    private static readonly Vector2 CharacterRest = new Vector2(295f, 0f);
    private static readonly Vector2 DecorStarRest = new Vector2(200f, 300f);

    private enum Entry { Stats, SaveLoad, Settings, MainMenu, Continue, NewGame, Quit, Load }
    private enum Page { Main, Stats, SaveLoad, Settings, Confirm }

    private static readonly string[] EntryNames = { "Stats", "Save/Load", "Settings", "Main Menu", "Continue", "New Game", "Quit", "Load" };
    private static readonly Entry[] GameEntries = { Entry.Stats, Entry.SaveLoad, Entry.Settings, Entry.MainMenu };
    private static readonly Entry[] TitleEntries = { Entry.Continue, Entry.NewGame, Entry.Settings, Entry.Load, Entry.Quit };
    private static readonly string[] TitleNames = { "CONTINUE", "NEW GAME", "OPTIONS", "LOAD", "EXIT" };
    private const float TitleX = 92f;
    private const float TitleY = 610f;
    private const float TitleStep = 80f;
    private const float TitleFontSize = 54f;

    private bool isOpen;
    private float openT;
    private float pageT = 1f;
    private int selected;
    private Entry[] entries = GameEntries;
    private Page page = Page.Main;
    private bool titleMode;
    private bool sceneAllowed;
    private float prevTimeScale = 1f;
    private bool confirmYes;
    private int continueSlot;

    private GameObject buttonRoot;
    private GameObject menuRoot;
    private CanvasGroup menuGroup;
    private Image dim;
    private RectTransform mainRoot;
    private RectTransform characterRT;
    private RectTransform decorStar;
    private RectTransform[] cloudRTs = new RectTransform[4];
    private Image[] cloudImgs = new Image[4];
    private TextMeshProUGUI[] labels = new TextMeshProUGUI[4];
    private RectTransform selStar;
    private RectTransform titleRoot;
    private RectTransform titleSwipe;
    private TextMeshProUGUI[] titleLabels;
    private RectTransform pageRoot;
    private GameObject backButtonObj;

    private Slider volumeSlider;
    private TextMeshProUGUI volumeValue;
    private TextMeshProUGUI fullscreenLabel;
    private Image confirmYesImg, confirmNoImg;
    private string saveStatus = "";
    private bool[] saveArmed = new bool[SaveSystem.SlotCount];

    private TMP_FontAsset customFont;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Instance = null; spriteCache = null; swipeSprite = null; warned.Clear(); }

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
        customFont = Resources.Load<TMP_FontAsset>(ResPath + "MenuFont");

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

        if (isOpen) { bool t = titleMode; titleMode = false; Close(); titleMode = t; }
        isOpen = false;
        RestoreTime();
        openT = 0f;
        menuRoot.SetActive(false);

        bool wantTitle = scene.name == mainMenuScene;
        if (wantTitle) sceneAllowed = true;
        SetMode(wantTitle);
        if (titleMode) Open();
    }

    private void SetMode(bool title)
    {
        titleMode = title;
        entries = title ? TitleEntries : GameEntries;

        dim.color = title ? new Color(0f, 0f, 0f, 0f) : DimColor;
        characterRT.gameObject.SetActive(!title);
        decorStar.gameObject.SetActive(!title);

        if (title)
        {
            GameObject oldStart = GameObject.Find("START");
            GameObject oldQuit = GameObject.Find("Quit");
            if (oldStart != null) oldStart.SetActive(false);
            if (oldQuit != null) oldQuit.SetActive(false);
        }

        for (int i = 0; i < labels.Length && i < entries.Length; i++)
            labels[i].text = EntryNames[(int)entries[i]];

        page = Page.Main;
        SetSelected(0, true);
    }

    private void Update()
    {
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);

        HandleInput();
        Animate(dt);

        bool showButton = showOpenButton && sceneAllowed && !titleMode && !isOpen && openT <= 0f;
        if (buttonRoot.activeSelf != showButton) buttonRoot.SetActive(showButton);
    }

    private void HandleInput()
    {
        if (!sceneAllowed) return;

        Keyboard kb = Keyboard.current;
        Gamepad pad = Gamepad.current;
        Mouse mouse = Mouse.current;

        bool toggle = (kb != null && kb[openKey].wasPressedThisFrame) || (pad != null && pad.startButton.wasPressedThisFrame);
        bool back = toggle
                 || (pad != null && pad.buttonEast.wasPressedThisFrame)
                 || (mouse != null && mouse.rightButton.wasPressedThisFrame);

        if (!isOpen)
        {
            if (!titleMode && toggle) Open();
            return;
        }

        if (back) { GoBack(); return; }

        bool confirm = (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame))
                    || (pad != null && pad.buttonSouth.wasPressedThisFrame);

        int move = 0;
        int side = 0;
        if (kb != null)
        {
            if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) move = -1;
            if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) move = 1;
            if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) side = -1;
            if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) side = 1;
        }
        if (pad != null)
        {
            if (pad.dpad.up.wasPressedThisFrame) move = -1;
            if (pad.dpad.down.wasPressedThisFrame) move = 1;
            if (pad.dpad.left.wasPressedThisFrame) side = -1;
            if (pad.dpad.right.wasPressedThisFrame) side = 1;
        }

        switch (page)
        {
            case Page.Main:
                if (move != 0) MoveSelection(move);
                if (confirm && EntryEnabled(entries[selected])) Activate(entries[selected]);
                break;

            case Page.Settings:
                if (side != 0 && volumeSlider != null)
                    volumeSlider.value = Mathf.Clamp01(volumeSlider.value + side * 0.05f);
                if (confirm) ToggleFullscreen();
                break;

            case Page.Confirm:
                if (side != 0 || move != 0) { confirmYes = !confirmYes; RefreshConfirm(); }
                if (confirm)
                {
                    if (confirmYes) ReturnToMainMenu();
                    else ShowPage(Page.Main);
                }
                break;
        }
    }

    private static float Ease(float t)
    {
        return 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);
    }

    private void Animate(float dt)
    {
        float target = isOpen ? 1f : 0f;
        openT = Mathf.MoveTowards(openT, target, dt * 3f);
        pageT = Mathf.MoveTowards(pageT, 1f, dt * 4f);

        if (!isOpen && openT <= 0f)
        {
            if (menuRoot.activeSelf) menuRoot.SetActive(false);
            return;
        }

        menuGroup.alpha = Mathf.Clamp01(openT * 3f);

        float e = Ease(openT);

        pageRoot.anchoredPosition = new Vector2(0f, -(1f - Ease(pageT)) * 700f);

        if (titleMode)
        {
            if (page == Page.Main) AnimateTitle(dt, e);
            return;
        }

        if (page != Page.Main && page != Page.Confirm) return;

        characterRT.anchoredPosition = CharacterRest + new Vector2(-(1f - e) * 700f, 0f);
        float bob = Mathf.Sin(Time.unscaledTime * 2f) * 10f;
        decorStar.anchoredPosition = DecorStarRest + new Vector2(-(1f - e) * 700f, bob);
        decorStar.localRotation = Quaternion.Euler(0f, 0f, -12f + Mathf.Sin(Time.unscaledTime * 1.3f) * 6f);

        float k = 1f - Mathf.Exp(-14f * dt);
        for (int i = 0; i < cloudRTs.Length; i++)
        {
            float ei = Ease(openT * 1.5f - i * 0.15f);
            cloudRTs[i].anchoredPosition = new Vector2((1f - ei) * 1300f, -CloudY[i]);
            float slide = (1f - ei) * 1300f;
            labels[i].rectTransform.anchoredPosition = new Vector2(LabelPos[i].x + slide, -(LabelPos[i].y + CloudY[i]));

            bool sel = i == selected;
            RectTransform lr = labels[i].rectTransform;
            float s = Mathf.Lerp(lr.localScale.x, sel ? 1.18f : 1f, k);
            lr.localScale = new Vector3(s, s, 1f);
            labels[i].color = Color.Lerp(labels[i].color, sel ? Navy : new Color(Navy.r, Navy.g, Navy.b, 0.62f), k);
            cloudImgs[i].color = Color.Lerp(cloudImgs[i].color, sel ? Color.white : new Color(0.9f, 0.95f, 1f, 1f), k);
        }

        if (selStar != null)
            selStar.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.unscaledTime * 3f) * 10f);
    }

    public void Open()
    {
        if (isOpen || !sceneAllowed) return;

        if (!titleMode)
        {
            if (Time.timeScale == 0f) return;

            prevTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            GameSpeed.Paused = true;
        }
        isOpen = true;

        menuRoot.SetActive(true);
        SetSelected(0, true);
        ShowPage(Page.Main);
    }

    public void Close()
    {
        if (!isOpen) return;
        if (titleMode) return;
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

    private void GoBack()
    {
        if (page == Page.Main)
        {
            if (!titleMode) Close();
        }
        else
        {
            ShowPage(Page.Main);
        }
    }

    private void SetSelected(int index, bool force)
    {
        if (!force && index == selected) return;
        selected = index;
        if (titleMode) return;

        if (selStar != null && labels[index] != null)
        {
            selStar.SetParent(labels[index].rectTransform, false);
            labels[index].ForceMeshUpdate();
            float half = labels[index].preferredWidth * 0.5f;
            selStar.anchoredPosition = new Vector2(-half - 70f, 4f);
            selStar.SetAsLastSibling();
        }
    }

    private void Activate(Entry entry)
    {
        switch (entry)
        {
            case Entry.Stats: ShowPage(Page.Stats); break;
            case Entry.SaveLoad:
            case Entry.Load: ShowPage(Page.SaveLoad); break;
            case Entry.Continue: ContinueGame(); break;
            case Entry.Settings: ShowPage(Page.Settings); break;
            case Entry.MainMenu: confirmYes = false; ShowPage(Page.Confirm); break;
            case Entry.NewGame:
            case Entry.Quit: RunTitleAction(entry); break;
        }
    }

    private bool EntryEnabled(Entry e)
    {
        return e != Entry.Continue || continueSlot > 0;
    }

    private void MoveSelection(int dir)
    {
        int idx = selected;
        for (int n = 0; n < entries.Length; n++)
        {
            idx = (idx + dir + entries.Length) % entries.Length;
            if (EntryEnabled(entries[idx])) { SetSelected(idx, false); return; }
        }
    }

    private void RefreshContinue()
    {
        continueSlot = 0;
        DateTime bestTime = DateTime.MinValue;
        bool bestParsed = false;
        for (int slot = 1; slot <= SaveSystem.SlotCount; slot++)
        {
            SaveFile info = SaveSystem.GetInfo(slot);
            if (info == null) continue;

            DateTime t;
            bool parsed = DateTime.TryParse(info.savedAt, out t);
            if (continueSlot == 0 || (parsed && (!bestParsed || t > bestTime)))
            {
                continueSlot = slot;
                if (parsed) { bestTime = t; bestParsed = true; }
            }
        }
    }

    private void ContinueGame()
    {
        if (continueSlot <= 0 || !SaveSystem.HasSave(continueSlot)) return;
        int slot = continueSlot;
        Close();
        if (!SaveSystem.Load(slot))
        {
            ShowPage(Page.SaveLoad);
            saveStatus = "Could not load slot " + slot + ".";
            BuildPage();
        }
    }

    private void AnimateTitle(float dt, float e)
    {
        float k = 1f - Mathf.Exp(-14f * dt);

        for (int i = 0; i < titleLabels.Length; i++)
        {
            TextMeshProUGUI t = titleLabels[i];
            RectTransform lr = t.rectTransform;

            float ei = Ease(openT * 1.5f - i * 0.12f);
            lr.anchoredPosition = new Vector2(TitleX - (1f - ei) * 420f, -(TitleY + i * TitleStep));

            bool sel = i == selected;
            float s = Mathf.Lerp(lr.localScale.x, sel ? 1.22f : 1f, k);
            lr.localScale = new Vector3(s, s, 1f);

            Color target = EntryEnabled(TitleEntries[i]) ? Color.white : new Color(1f, 1f, 1f, 0.4f);
            t.color = Color.Lerp(t.color, target, k);
        }

        if (titleSwipe != null && selected < titleLabels.Length)
        {
            Vector2 p = titleSwipe.anchoredPosition;
            p.x = TitleX - 40f - (1f - e) * 420f;
            p.y = Mathf.Lerp(p.y, -(TitleY + selected * TitleStep), k);
            titleSwipe.anchoredPosition = p;

            float w = titleLabels[selected].preferredWidth * 1.22f + 520f;
            Vector2 sz = titleSwipe.sizeDelta;
            sz.x = Mathf.Lerp(sz.x, w, k);
            titleSwipe.sizeDelta = sz;
        }
    }

    private static Sprite swipeSprite;

    private static Sprite SwipeSprite()
    {
        if (swipeSprite != null) return swipeSprite;
        try
        {
            const int W = 512, H = 64;
            Color32[] px = new Color32[W * H];
            for (int y = 0; y < H; y++)
            {
                float v = (y + 0.5f) / H;
                float edge = Mathf.SmoothStep(0f, 1f, Mathf.Min(v, 1f - v) / 0.22f);
                float end = Mathf.Lerp(0.62f, 1f, Mathf.PerlinNoise(y * 0.3f, 7.3f));
                for (int x = 0; x < W; x++)
                {
                    float u = x / (float)W;
                    float a = Mathf.Clamp01((end - u) / 0.5f);
                    a = a * a * (3f - 2f * a);
                    float grain = 0.9f + 0.1f * Mathf.PerlinNoise(x * 0.08f, y * 0.4f);
                    byte alpha = (byte)Mathf.RoundToInt(255f * Mathf.Clamp01(a * edge * grain * 0.9f));
                    px[y * W + x] = new Color32(110, 170, 245, alpha);
                }
            }

            Texture2D tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            tex.SetPixels32(px);
            tex.Apply();
            swipeSprite = Sprite.Create(tex, new Rect(0f, 0f, W, H), new Vector2(0f, 0.5f), 100f);
        }
        catch (Exception ex) { Debug.LogWarning("GameMenu: could not build the title highlight: " + ex.Message); }
        return swipeSprite;
    }

    private void BuildTitleList(RectTransform canvasRect)
    {
        titleRoot = NewRect("TitleList", canvasRect);
        Stretch(titleRoot);

        Image swipe = NewImage("Swipe", titleRoot, Color.white);
        swipe.sprite = SwipeSprite();
        swipe.enabled = swipe.sprite != null;
        titleSwipe = swipe.rectTransform;
        SetAnchored(titleSwipe, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(TitleX - 40f, -TitleY), new Vector2(760f, 92f));

        titleLabels = new TextMeshProUGUI[TitleEntries.Length];
        for (int i = 0; i < TitleEntries.Length; i++)
        {
            int index = i;
            TextMeshProUGUI t = NewText("Entry_" + TitleNames[i], titleRoot, TitleNames[i], TitleFontSize, Color.white,
                TextAlignmentOptions.Left, FontStyles.Bold);
            t.raycastTarget = true;
            SetAnchored(t.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f),
                new Vector2(TitleX, -(TitleY + i * TitleStep)), new Vector2(640f, 76f));
            t.enableWordWrapping = false;

            t.outlineColor = new Color32(47, 51, 105, 255);
            t.outlineWidth = 0.22f;

            GameMenuItemHover h = t.gameObject.AddComponent<GameMenuItemHover>();
            h.onEnter = () =>
            {
                if (titleMode && page == Page.Main && EntryEnabled(TitleEntries[index])) SetSelected(index, false);
            };
            h.onClick = () =>
            {
                if (!titleMode || page != Page.Main || !EntryEnabled(TitleEntries[index])) return;
                SetSelected(index, false);
                Activate(TitleEntries[index]);
            };
            titleLabels[i] = t;
        }
        titleRoot.gameObject.SetActive(false);
    }

    private void ShowPage(Page p)
    {
        page = p;
        pageT = 0f;
        saveStatus = "";
        for (int i = 0; i < saveArmed.Length; i++) saveArmed[i] = false;

        if (titleMode && p == Page.Main)
        {
            RefreshContinue();
            if (!EntryEnabled(entries[selected]))
            {
                for (int i = 0; i < entries.Length; i++)
                    if (EntryEnabled(entries[i])) { selected = i; break; }
            }
        }
        mainRoot.gameObject.SetActive(!titleMode && (p == Page.Main || p == Page.Confirm));
        titleRoot.gameObject.SetActive(titleMode && p == Page.Main);
        backButtonObj.SetActive(p == Page.Stats || p == Page.SaveLoad || p == Page.Settings);
        backButtonObj.transform.SetAsLastSibling();

        BuildPage();
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

    private TextMeshProUGUI NewText(string name, Transform parent, string text, float size, Color color,
        TextAlignmentOptions align, FontStyles style)
    {
        RectTransform r = NewRect(name, parent);
        TextMeshProUGUI t = r.gameObject.AddComponent<TextMeshProUGUI>();
        if (customFont != null) t.font = customFont;
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

    private static Dictionary<string, Sprite> spriteCache;
    private static readonly HashSet<string> warned = new HashSet<string>();

    private static string Norm(string s)
    {
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < s.Length; i++)
            if (char.IsLetterOrDigit(s[i])) sb.Append(char.ToLowerInvariant(s[i]));
        return sb.ToString();
    }

    private static Sprite LoadSprite(string name, bool optional = false)
    {
        if (spriteCache == null)
        {
            spriteCache = new Dictionary<string, Sprite>();
            Sprite[] all = Resources.LoadAll<Sprite>("MenuUI");
            for (int i = 0; i < all.Length; i++)
            {
                string k = Norm(all[i].name);
                if (!spriteCache.ContainsKey(k)) spriteCache.Add(k, all[i]);
            }
        }

        Sprite found;
        if (spriteCache.TryGetValue(Norm(name), out found)) return found;

        if (!optional && warned.Add(name))
            Debug.LogWarning("GameMenu: no sprite called \"" + name + "\" in Assets/Resources/MenuUI. " +
                             "Check its Texture Type is Sprite (2D and UI). Found: " + string.Join(", ", new List<string>(spriteCache.Keys).ToArray()));
        return null;
    }

    private static void AddOutline(Graphic g, float distance)
    {
        Outline o = g.gameObject.AddComponent<Outline>();
        o.effectColor = Navy;
        o.effectDistance = new Vector2(distance, -distance);
        o.useGraphicAlpha = false;
    }

    private Button MakeBoxButton(Transform parent, string label, Vector2 pos, Vector2 size, float fontSize, Action onClick, bool enabled, out TextMeshProUGUI text)
    {
        Image img = NewImage("Button_" + label, parent, BoxFill);
        img.raycastTarget = true;
        SetAnchored(img.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(pos.x, -pos.y), size);
        AddOutline(img, 4f);

        Button btn = img.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        ColorBlock cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(0.80f, 0.92f, 1f, 1f);
        cb.pressedColor = new Color(0.62f, 0.82f, 1f, 1f);
        cb.selectedColor = Color.white;
        cb.disabledColor = new Color(0.72f, 0.78f, 0.86f, 0.75f);
        cb.fadeDuration = 0.05f;
        btn.colors = cb;
        btn.onClick.AddListener(() => onClick());
        btn.interactable = enabled;
        img.gameObject.AddComponent<InteractiveUIButton>().enableWiggleOnUnsupportedClick = false;

        text = NewText("Label", img.transform, label, fontSize, enabled ? Navy : new Color(Navy.r, Navy.g, Navy.b, 0.45f),
            TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(text.rectTransform);
        return btn;
    }

    private TextMeshProUGUI CardText(Transform parent, string text, float size, Vector2 pos, Vector2 box,
        TextAlignmentOptions align, FontStyles style, float clockwiseDegrees, bool fit)
    {
        TextMeshProUGUI t = NewText("Text", parent, text, size, Navy, align, style);
        t.richText = true;
        RectTransform r = t.rectTransform;
        SetAnchored(r, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(pos.x, -pos.y), box);
        r.localRotation = Quaternion.Euler(0f, 0f, -clockwiseDegrees);
        t.enableWordWrapping = false;
        t.overflowMode = TextOverflowModes.Ellipsis;
        if (fit)
        {
            t.enableAutoSizing = true;
            t.fontSizeMax = size;
            t.fontSizeMin = size * 0.5f;
        }
        return t;
    }

    private RectTransform NewCard(string spriteName, Vector2 native, out RectTransform content)
    {
        RectTransform card = NewRect("Card", pageRoot);
        SetAnchored(card, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, native);
        float s = Mathf.Min(1920f / native.x, 1080f / native.y);
        card.localScale = new Vector3(s, s, 1f);

        Image img = card.gameObject.AddComponent<Image>();
        img.sprite = LoadSprite(spriteName);
        img.color = img.sprite != null ? Color.white : CardBlue;
        img.raycastTarget = false;

        content = NewRect("Content", card);
        SetAnchored(content, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, native);
        return card;
    }

    private static Image NewStar(string name, Transform parent)
    {
        RectTransform r = NewRect(name, parent);
        Image img = r.gameObject.AddComponent<Image>();
        img.raycastTarget = false;
        img.preserveAspect = true;

        Sprite sprite = LoadSprite("Star", true);
        if (sprite == null) sprite = StarFrames.Get();
        if (sprite != null) img.sprite = sprite;
        else img.enabled = false;
        return img;
    }

    private void BuildOpenButton()
    {
        Canvas canvas = NewCanvas("MenuButtonCanvas", transform, 998);
        buttonRoot = canvas.gameObject;

        TextMeshProUGUI label;
        MakeBoxButton(canvas.transform, "MENU", new Vector2(30f, 30f), new Vector2(150f, 64f), 32f, Open, true, out label);
    }

    private void BuildMenu()
    {
        Canvas canvas = NewCanvas("GameMenuCanvas", transform, 1000);
        menuRoot = canvas.gameObject;
        menuGroup = menuRoot.AddComponent<CanvasGroup>();
        RectTransform canvasRect = (RectTransform)canvas.transform;

        dim = NewImage("Dim", canvasRect, DimColor);
        dim.raycastTarget = true;
        Stretch(dim.rectTransform);

        mainRoot = NewRect("Main", canvasRect);
        Stretch(mainRoot);

        Image character = NewImage("Character", mainRoot, Color.white);
        character.sprite = LoadSprite("Menu");
        if (character.sprite == null) character.color = new Color(0.45f, 0.58f, 0.82f, 1f);
        characterRT = character.rectTransform;
        SetAnchored(characterRT, Vector2.zero, Vector2.zero, CharacterRest, new Vector2(728f, 923f));

        Image star = NewStar("Star", mainRoot);
        decorStar = star.rectTransform;
        SetAnchored(decorStar, Vector2.zero, new Vector2(0.5f, 0.5f), DecorStarRest, new Vector2(190f, 190f));
        decorStar.localRotation = Quaternion.Euler(0f, 0f, -12f);

        RectTransform clouds = NewRect("Clouds", mainRoot);
        SetAnchored(clouds, new Vector2(1f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(CloudW, 1080f));

        for (int i = 0; i < 4; i++)
        {
            int index = i;
            Image cloud = NewImage("Cloud_" + CloudSprites[i], clouds, Color.white);
            cloud.sprite = LoadSprite(CloudSprites[i]);
            if (cloud.sprite == null) cloud.color = CardBlue;
            SetAnchored(cloud.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -CloudY[i]), new Vector2(CloudW, CloudH[i]));
            cloudRTs[i] = cloud.rectTransform;
            cloudImgs[i] = cloud;
        }
        for (int i = 0; i < 4; i++)
        {
            int index = i;
            TextMeshProUGUI t = NewText("Label", cloudRTs[i], "", 84f, Navy, TextAlignmentOptions.Center, FontStyles.Bold | FontStyles.Italic);
            t.raycastTarget = true;
            SetAnchored(t.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(LabelPos[i].x, -LabelPos[i].y), new Vector2(620f, 150f));
            t.transform.SetParent(clouds, true);
            t.rectTransform.anchoredPosition = new Vector2(LabelPos[i].x, -(LabelPos[i].y + CloudY[i]));
            t.enableWordWrapping = false;

            GameMenuItemHover h = t.gameObject.AddComponent<GameMenuItemHover>();
            h.onEnter = () => { if (page == Page.Main) SetSelected(index, false); };
            h.onClick = () =>
            {
                if (page != Page.Main) return;
                SetSelected(index, false);
                Activate(entries[index]);
            };
            labels[i] = t;
        }

        Image sel = NewStar("SelectedStar", clouds);
        selStar = sel.rectTransform;
        SetAnchored(selStar, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(100f, 100f));

        BuildTitleList(canvasRect);

        pageRoot = NewRect("Page", canvasRect);
        Stretch(pageRoot);

        RectTransform backRoot = NewRect("BackButton", canvasRect);
        Stretch(backRoot);
        TextMeshProUGUI backLabel;
        Button back = MakeBoxButton(backRoot, "< BACK", new Vector2(30f, 30f), new Vector2(190f, 70f), 36f, GoBack, true, out backLabel);
        backButtonObj = backRoot.gameObject;
        backButtonObj.SetActive(false);
    }

    private void BuildPage()
    {
        for (int i = pageRoot.childCount - 1; i >= 0; i--)
        {
            pageRoot.GetChild(i).gameObject.SetActive(false);
            Destroy(pageRoot.GetChild(i).gameObject);
        }
        volumeSlider = null;
        volumeValue = null;
        fullscreenLabel = null;
        confirmYesImg = confirmNoImg = null;

        switch (page)
        {
            case Page.Stats: BuildStatsPage(); break;
            case Page.SaveLoad: BuildSaveLoadPage(); break;
            case Page.Settings: BuildSettingsPage(); break;
            case Page.Confirm: BuildConfirmPage(); break;
        }
    }

    private void BuildStatsPage()
    {
        RectTransform content;
        NewCard("Stats", new Vector2(1980f, 1039f), out content);

        Func<float, float, float> Y = (x, y) => y + 0.0105f * (x - 700f);
        const float tilt = 0.6f;

        string sun = SidebarValue("sunPower") ?? "-";
        string wind = SidebarValue("windPower") ?? "-";
        string speed = SidebarValue("moveSpeed") ?? "-";

        PlaceIcon(content, LoadSprite("Sun_Icon"), new Vector2(440f, Y(440f, 300f)), new Vector2(92f, 92f), tilt);
        CardText(content, sun, 78f, new Vector2(525f, Y(525f, 300f)), new Vector2(150f, 100f), TextAlignmentOptions.Left, FontStyles.Bold, tilt, true);

        PlaceIcon(content, LoadSprite("Wind_Icon"), new Vector2(740f, Y(740f, 300f)), new Vector2(110f, 88f), tilt);
        CardText(content, wind, 78f, new Vector2(815f, Y(815f, 300f)), new Vector2(150f, 100f), TextAlignmentOptions.Left, FontStyles.Bold, tilt, true);

        Sprite speedIcon = LoadSprite("Speed_Icon", true);
        if (speedIcon != null) PlaceIcon(content, speedIcon, new Vector2(985f, Y(985f, 300f)), new Vector2(90f, 90f), tilt);
        else CardText(content, "SPD", 34f, new Vector2(945f, Y(945f, 300f)), new Vector2(110f, 80f), TextAlignmentOptions.Left, FontStyles.Bold | FontStyles.Italic, tilt, false);
        CardText(content, speed, 58f, new Vector2(1060f, Y(1060f, 300f)), new Vector2(200f, 90f), TextAlignmentOptions.Left, FontStyles.Bold, tilt, true);

        CardText(content, "<b>Moods:</b>  " + (MoodNames() ?? "None"), 50f, new Vector2(425f, Y(425f, 408f)), new Vector2(840f, 66f), TextAlignmentOptions.Left, FontStyles.Normal, tilt, true);
        CardText(content, "<b>Boons:</b>  " + (BoonNames() ?? "None"), 50f, new Vector2(425f, Y(425f, 546f)), new Vector2(540f, 66f), TextAlignmentOptions.Left, FontStyles.Normal, tilt, true);
        CardText(content, "<b>Blueprints:</b>  " + (BlueprintNames() ?? "None"), 50f, new Vector2(985f, Y(985f, 546f)), new Vector2(610f, 66f), TextAlignmentOptions.Left, FontStyles.Normal, tilt, true);
        CardText(content, "<b>Trinkets:</b>  " + (TrinketNames() ?? "None"), 50f, new Vector2(415f, Y(415f, 678f)), new Vector2(1180f, 66f), TextAlignmentOptions.Left, FontStyles.Normal, tilt, true);
        CardText(content, "<b>Advantage hit:</b>  " + AdvantageText(), 50f, new Vector2(405f, Y(405f, 800f)), new Vector2(1000f, 66f), TextAlignmentOptions.Left, FontStyles.Normal, tilt, true);
    }

    private void PlaceIcon(Transform parent, Sprite sprite, Vector2 center, Vector2 size, float clockwiseDegrees)
    {
        if (sprite == null) return;
        Image img = NewImage("Icon", parent, Color.white);
        img.sprite = sprite;
        img.preserveAspect = true;
        SetAnchored(img.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(center.x, -center.y), size);
        img.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -clockwiseDegrees);
    }

    private static readonly Vector2[] SlotStart = { new Vector2(656f, 222f), new Vector2(641f, 346f), new Vector2(625f, 461f), new Vector2(607f, 597f) };
    private static readonly float[] SlotAngle = { 0.1f, 0.8f, 2.4f, 3.8f };
    private static readonly float[] SlotHeight = { 124f, 115f, 136f, 128f };
    private static readonly float[] SlotRight = { 1695f, 1683f, 1678f, 1663f };

    private void BuildSaveLoadPage()
    {
        RectTransform content;
        NewCard("SaveLoad", new Vector2(1836f, 1080f), out content);

        CardText(content, "Save / Load", 76f, new Vector2(650f, 172f), new Vector2(800f, 100f), TextAlignmentOptions.Left, FontStyles.Bold | FontStyles.Italic, 0.1f, false);

        bool canSave = !titleMode && SaveSystem.CanSaveHere;
        bool anySave = false;
        int slots = Mathf.Min(SaveSystem.SlotCount, SlotStart.Length);

        for (int i = 0; i < slots; i++)
        {
            int slot = i + 1;
            SaveFile info = SaveSystem.GetInfo(slot);
            if (info != null) anySave = true;

            float rowW = (SlotRight[i] - SlotStart[i].x) / Mathf.Cos(SlotAngle[i] * Mathf.Deg2Rad);
            RectTransform row = NewRect("Row" + slot, content);
            SetAnchored(row, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(SlotStart[i].x, -SlotStart[i].y), new Vector2(rowW, SlotHeight[i]));
            row.localRotation = Quaternion.Euler(0f, 0f, -SlotAngle[i]);

            float mid = SlotHeight[i] * 0.5f;
            CardText(row, "Slot " + slot, 56f, new Vector2(8f, mid), new Vector2(190f, 80f), TextAlignmentOptions.Left, FontStyles.Bold | FontStyles.Italic, 0f, true);

            string line1 = info == null ? "<i>Empty</i>" : info.savedAt;
            string line2 = info == null ? "" : "\n" + info.summary;
            CardText(row, line1 + line2, 32f, new Vector2(205f, mid), new Vector2(rowW - 205f - 390f, 96f), TextAlignmentOptions.Left, FontStyles.Normal, 0f, true);

            Vector2 btnSize = new Vector2(176f, 78f);
            float btnY = (SlotHeight[i] - btnSize.y) * 0.5f;

            if (!titleMode)
            {
                TextMeshProUGUI saveLabel;
                int captured = slot;
                MakeBoxButton(row, "Save", new Vector2(rowW - 380f, btnY), btnSize, 44f, () => OnSaveClicked(captured), canSave, out saveLabel);
                if (info != null && saveArmed[i]) saveLabel.text = "Sure?";
            }

            TextMeshProUGUI loadLabel;
            int capturedLoad = slot;
            MakeBoxButton(row, "Load", new Vector2(rowW - 190f, btnY), btnSize, 44f, () => OnLoadClicked(capturedLoad), info != null, out loadLabel);
        }

        string status = saveStatus;
        if (string.IsNullOrEmpty(status))
        {
            if (titleMode) status = anySave ? "Choose a save to continue." : "No saved games yet. Pick New Game to begin.";
            else if (!canSave) status = "You can only save in the overworld, not during a fight.";
        }
        if (!string.IsNullOrEmpty(status))
            CardText(content, status, 36f, new Vector2(640f, 900f), new Vector2(1000f, 60f), TextAlignmentOptions.Left, FontStyles.Italic, 6f, true);
    }

    private void OnSaveClicked(int slot)
    {
        int i = slot - 1;
        if (SaveSystem.HasSave(slot) && !saveArmed[i])
        {
            for (int k = 0; k < saveArmed.Length; k++) saveArmed[k] = false;
            saveArmed[i] = true;
            saveStatus = "Press Save again to overwrite slot " + slot + ".";
            BuildPage();
            return;
        }

        for (int k = 0; k < saveArmed.Length; k++) saveArmed[k] = false;
        saveStatus = SaveSystem.Save(slot) ? "Saved to slot " + slot + "." : "Could not save.";
        BuildPage();
    }

    private void OnLoadClicked(int slot)
    {
        if (!SaveSystem.HasSave(slot)) return;
        Close();
        if (!SaveSystem.Load(slot))
        {
            saveStatus = "Could not load slot " + slot + ".";
            BuildPage();
        }
    }

    private RectTransform NewPanel(Vector2 size, out Image bg)
    {
        bg = NewImage("Panel", pageRoot, CardBlue);
        bg.raycastTarget = true;
        SetAnchored(bg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
        AddOutline(bg, 6f);
        return bg.rectTransform;
    }

    private void BuildSettingsPage()
    {
        Image bg;
        RectTransform panel = NewPanel(new Vector2(1000f, 620f), out bg);

        CardText(panel, "Settings", 82f, new Vector2(60f, 90f), new Vector2(700f, 110f), TextAlignmentOptions.Left, FontStyles.Bold | FontStyles.Italic, 0f, false);

        Image line = NewImage("Line", panel, Navy);
        SetAnchored(line.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(50f, -160f), new Vector2(900f, 6f));

        CardText(panel, "Master volume", 48f, new Vector2(60f, 235f), new Vector2(600f, 70f), TextAlignmentOptions.Left, FontStyles.Bold, 0f, false);

        volumeSlider = MakeSlider(panel, new Vector2(60f, 290f), new Vector2(660f, 50f));
        volumeValue = CardText(panel, "", 48f, new Vector2(760f, 315f), new Vector2(180f, 70f), TextAlignmentOptions.Left, FontStyles.Bold, 0f, false);
        volumeSlider.SetValueWithoutNotify(AudioListener.volume);
        volumeValue.text = Mathf.RoundToInt(AudioListener.volume * 100f) + "%";
        volumeSlider.onValueChanged.AddListener(OnVolumeChanged);

        TextMeshProUGUI fsLabel;
        MakeBoxButton(panel, "", new Vector2(60f, 395f), new Vector2(520f, 84f), 42f, ToggleFullscreen, true, out fsLabel);
        fullscreenLabel = fsLabel;
        fullscreenLabel.text = "Fullscreen: " + (Screen.fullScreen ? "ON" : "OFF");

        CardText(panel, "Left / Right: volume      Enter: fullscreen      Esc: back", 30f, new Vector2(60f, 545f), new Vector2(900f, 50f),
            TextAlignmentOptions.Left, FontStyles.Italic, 0f, true);
    }

    private Slider MakeSlider(Transform parent, Vector2 pos, Vector2 size)
    {
        RectTransform root = NewRect("VolumeSlider", parent);
        SetAnchored(root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(pos.x, -pos.y), size);

        Image track = NewImage("Track", root, new Color(Navy.r, Navy.g, Navy.b, 0.28f));
        track.rectTransform.anchorMin = new Vector2(0f, 0.3f);
        track.rectTransform.anchorMax = new Vector2(1f, 0.7f);
        track.rectTransform.offsetMin = track.rectTransform.offsetMax = Vector2.zero;

        RectTransform fillArea = NewRect("FillArea", root);
        fillArea.anchorMin = new Vector2(0f, 0.3f);
        fillArea.anchorMax = new Vector2(1f, 0.7f);
        fillArea.offsetMin = new Vector2(5f, 0f);
        fillArea.offsetMax = new Vector2(-15f, 0f);

        Image fill = NewImage("Fill", fillArea, new Color(0.46f, 0.58f, 0.86f, 1f));
        fill.rectTransform.sizeDelta = new Vector2(10f, 0f);

        RectTransform handleArea = NewRect("HandleSlideArea", root);
        handleArea.anchorMin = Vector2.zero;
        handleArea.anchorMax = Vector2.one;
        handleArea.offsetMin = new Vector2(14f, 0f);
        handleArea.offsetMax = new Vector2(-14f, 0f);

        Image handle = NewImage("Handle", handleArea, Navy);
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
        if (fullscreenLabel != null) fullscreenLabel.text = "Fullscreen: " + (fs ? "ON" : "OFF");
    }

    private void BuildConfirmPage()
    {
        Image bg;
        RectTransform panel = NewPanel(new Vector2(920f, 400f), out bg);

        CardText(panel, "Return to the title screen?", 58f, new Vector2(50f, 100f), new Vector2(840f, 90f), TextAlignmentOptions.Left, FontStyles.Bold | FontStyles.Italic, 0f, true);
        CardText(panel, "Anything you have not saved will be lost.", 38f, new Vector2(50f, 180f), new Vector2(840f, 60f), TextAlignmentOptions.Left, FontStyles.Normal, 0f, true);

        TextMeshProUGUI l1, l2;
        Button yes = MakeBoxButton(panel, "Yes", new Vector2(110f, 255f), new Vector2(300f, 90f), 50f, ReturnToMainMenu, true, out l1);
        Button no = MakeBoxButton(panel, "No", new Vector2(510f, 255f), new Vector2(300f, 90f), 50f, () => ShowPage(Page.Main), true, out l2);
        confirmYesImg = yes.targetGraphic as Image;
        confirmNoImg = no.targetGraphic as Image;
        RefreshConfirm();
    }

    private void RefreshConfirm()
    {
        if (confirmYesImg == null || confirmNoImg == null) return;
        confirmYesImg.color = confirmYes ? new Color(0.70f, 0.88f, 1f, 1f) : BoxFill;
        confirmNoImg.color = confirmYes ? BoxFill : new Color(0.70f, 0.88f, 1f, 1f);
    }

    public void ReturnToMainMenu()
    {
        if (!isOpen) return;
        Close();
        SceneManager.LoadScene(mainMenuScene);
    }

    private void RunTitleAction(Entry entry)
    {
        if (entry == Entry.NewGame)
        {
            SaveSystem.ResetProgress();

            isOpen = false;
            openT = 0f;
            menuRoot.SetActive(false);

            MainMenu mm = FindFirstObjectByType<MainMenu>();
            if (mm != null) mm.StartGame();
            else SceneManager.LoadScene("GameScene");
        }
        else if (entry == Entry.Quit)
        {
            MainMenu mm = FindFirstObjectByType<MainMenu>();
            if (mm != null) mm.QuitGame();
            else Application.Quit();
        }
    }

    private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    private static object GetMember(object o, string name)
    {
        if (o == null) return null;
        Type t = o as Type ?? o.GetType();
        object target = o is Type ? null : o;
        FieldInfo f = t.GetField(name, Any);
        if (f != null) return f.GetValue(f.IsStatic ? null : target);
        PropertyInfo p = t.GetProperty(name, Any);
        if (p != null && p.CanRead && p.GetIndexParameters().Length == 0)
        {
            MethodInfo g = p.GetGetMethod(true);
            return p.GetValue(g != null && g.IsStatic ? null : target, null);
        }
        return null;
    }

    private static string SidebarValue(string slotField)
    {
        try
        {
            MonoBehaviour sidebar = null;
            MonoBehaviour[] all = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].GetType().Name == "ActiveBuffsSidebar") { sidebar = all[i]; break; }
            }
            if (sidebar == null) return null;

            object slot = GetMember(sidebar, slotField);
            TMP_Text tmp = GetMember(slot, "valueText") as TMP_Text;
            if (tmp == null || string.IsNullOrEmpty(tmp.text)) return null;
            string s = Regex.Replace(tmp.text, "<.*?>", "").Trim();
            return s.Length == 0 ? null : s;
        }
        catch (Exception) { return null; }
    }

    private static string ItemName(object o)
    {
        if (o == null) return null;
        string s = o as string;
        if (s != null) return s;
        string n = ReadText(o, "displayName", "trinketName", "boonName", "moodName", "blueprintName", "itemName", "title", "Name", "name");
        if (!string.IsNullOrEmpty(n)) return n;
        UnityEngine.Object uo = o as UnityEngine.Object;
        return uo != null ? uo.name : o.ToString();
    }

    private static string JoinNames(object value)
    {
        if (value == null) return null;
        string single = value as string;
        if (single != null) return single.Length == 0 ? null : single;

        System.Collections.IEnumerable list = value as System.Collections.IEnumerable;
        if (list == null) return ItemName(value);

        List<string> names = new List<string>();
        foreach (object item in list)
        {
            string n = ItemName(item);
            if (!string.IsNullOrEmpty(n)) names.Add(n);
        }
        return names.Count == 0 ? null : string.Join(", ", names);
    }

    private static string NamesFromStatic(string[] typeNames, string[] members)
    {
        try
        {
            foreach (string tn in typeNames)
            {
                Type t = Type.GetType(tn);
                if (t == null) continue;
                foreach (string m in members)
                {
                    string s = JoinNames(GetMember(t, m));
                    if (s != null) return s;
                }
            }
        }
        catch (Exception) { }
        return null;
    }

    private static string MoodNames()
    {
        return NamesFromStatic(new[] { "MoodManager" },
            new[] { "ActiveMoods", "Moods", "CurrentMoods", "Active", "Current", "CurrentMood", "ActiveMood" });
    }

    private static string BoonNames()
    {
        return NamesFromStatic(new[] { "BoonProgress", "BoonManager" },
            new[] { "Owned", "Boons", "OwnedBoons", "Taken", "Chosen", "Selected", "Collected", "Active" });
    }

    private static string BlueprintNames()
    {
        return NamesFromStatic(new[] { "BlueprintProgress", "BlueprintInventory", "BlueprintShop", "StructureProgress", "Blueprints" },
            new[] { "Owned", "Blueprints", "OwnedBlueprints", "Unlocked", "Purchased", "Bought", "Collected" });
    }

    private static string TrinketNames()
    {
        try
        {
            List<string> names = new List<string>();
            foreach (var trinket in TrinketInventory.Owned)
            {
                if (trinket == null) continue;
                string n = ItemName(trinket);
                if (!string.IsNullOrEmpty(n)) names.Add(n);
            }
            return names.Count == 0 ? null : string.Join(", ", names);
        }
        catch (Exception) { return null; }
    }

    private static string AdvantageText()
    {
        try
        {
            string[] types = { "AdvantageHit", "AdvantageManager", "PlayerProgress", "BoonProgress", "MoodManager" };
            string[] members = { "HasAdvantage", "HasAdvantageHit", "AdvantageHit", "Advantage", "Active", "Ready", "hasAdvantage" };
            foreach (string tn in types)
            {
                Type t = Type.GetType(tn);
                if (t == null) continue;
                foreach (string m in members)
                {
                    object v = GetMember(t, m);
                    if (v is bool) return (bool)v ? "Ready" : "None";
                }
            }
        }
        catch (Exception) { }
        return "None";
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
            if (pi != null && pi.CanRead && pi.PropertyType == typeof(string) && pi.GetIndexParameters().Length == 0)
            {
                string s = pi.GetValue(o, null) as string;
                if (!string.IsNullOrEmpty(s)) return s;
            }
        }
        return null;
    }
}