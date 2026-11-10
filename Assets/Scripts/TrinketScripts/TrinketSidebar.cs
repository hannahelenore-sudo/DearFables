using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// The trinket bar on the right side of the screen (game scene AND card scene).
/// - Shows the trinkets you own (max 5) and Sun Power / Wind Power / Movement Speed.
/// - Opens by itself when the shop opens. Click the little tab to open/close it.
/// - Drag a trinket up/down to change the order its effects trigger (top to bottom, like Balatro jokers).
/// - Click a trinket to select it; while the shop is open a SELL button appears.
/// Build it with  Tools > Shop > Create Trinket Sidebar.
/// It survives scene loads. If a scene contains a second copy, the copy deletes itself.
/// </summary>
public class TrinketSidebar : MonoBehaviour
{
    public enum SpeedStat { Walk, Run }

    [Serializable]
    public class StatRow
    {
        public Image icon;
        public TMP_Text valueText;

        [NonSerialized] public float last = float.NaN;
        [NonSerialized] public Color baseColor = Color.white;
        [NonSerialized] public Vector3 baseScale = Vector3.one;
        [NonSerialized] public Coroutine flash;
    }

    // ---------------- Setup (filled by the builder) ----------------

    [Header("Panel")]
    [Tooltip("The whole bar (panel + tab). It slides in and out.")]
    public RectTransform slideRoot;
    [Tooltip("Size of the whole bar. 1 = full size, 0.75 = smaller, 0.5 = half. Applied when the game starts.")]
    [Range(0.3f, 1.5f)] public float panelScale = 0.75f;
    [Tooltip("The 5 empty slot frames, top to bottom. Trinkets sit on these.")]
    public RectTransform[] frames;
    [Tooltip("A hidden trinket card that gets cloned. Style this one.")]
    public TrinketItemView itemTemplate;
    public Button toggleButton;
    public TMP_Text toggleArrow;

    [Header("Stats")]
    public StatRow sunPower = new StatRow();
    public StatRow windPower = new StatRow();
    public StatRow moveSpeed = new StatRow();
    [Tooltip("Which speed the boot shows.")]
    public SpeedStat speedShows = SpeedStat.Walk;
    public Color statUpColor = new Color(0.4f, 1f, 0.4f);
    public Color statDownColor = new Color(1f, 0.4f, 0.4f);

    [Header("Info Card (hover / selected trinket)")]
    public RectTransform infoCard;
    public TMP_Text infoName;
    public TMP_Text infoDescription;
    public TMP_Text infoHint;
    public Button sellButton;

    // ---------------- Behaviour ----------------

    [Header("Behaviour")]
    public bool persistAcrossScenes = true;
    public bool startOpen = false;
    [Tooltip("Slide open by itself when the shop opens.")]
    public bool openWithShop = true;
    [Tooltip("Slide closed again when the shop closes (only if the shop was what opened it).")]
    public bool closeWhenShopCloses = true;
    [Tooltip("Slide open while a hand is being scored, so you can watch the trinkets trigger.")]
    public bool openWhileScoring = true;
    [Tooltip("Scene names where the bar is hidden completely (main menu, intro...).")]
    public string[] hideInScenes;

    [Header("Animation")]
    public float slideSpeed = 12f;
    public float itemMoveSpeed = 18f;
    [Tooltip("How far left of the trinket the trigger popup appears.")]
    public float popupOffsetX = 85f;
    public Color addPopupColor = new Color(0.85f, 0.2f, 0.2f);
    public Color multiplyPopupColor = new Color(0.9f, 0.45f, 0.1f);
    public Color failPopupColor = new Color(0.4f, 0.4f, 0.45f);

    [Header("Sounds")]
    [Range(0f, 1f)] public float sfxVolume = 1f;
    public AudioClip openSound;
    public AudioClip closeSound;
    public AudioClip addSound;
    public AudioClip selectSound;
    public AudioClip pickupSound;
    [Tooltip("Plays every time the dragged trinket passes another one (the order changes). Pitch climbs a little.")]
    public AudioClip reorderSound;
    public AudioClip dropSound;
    public AudioClip sellSound;
    [Tooltip("Played when you try to take a 6th trinket, or drag while scoring.")]
    public AudioClip errorSound;
    public AudioClip statUpSound;
    public AudioClip statDownSound;

    // ---------------- Static API ----------------

    public static TrinketSidebar Instance { get; private set; }

    /// <summary>True while the mouse is over the bar (or dragging a trinket). Use it to stop clicks leaking into the world.</summary>
    public static bool IsPointerOver
    {
        get { return Instance != null && Instance.PointerInside(); }
    }

    public static bool ReorderLocked { get; private set; }

    /// <summary>Makes the trinket at this position bounce and show a popup ("+3 Mult"). Called by DeckManager.</summary>
    public static void Pulse(int index, string label)
    {
        if (Instance != null) Instance.PulseInternal(index, label);
    }

    /// <summary>Call before scoring: locks the order and (optionally) slides the bar open.</summary>
    public static void BeginScoring()
    {
        ReorderLocked = true;
        if (Instance != null) Instance.OnScoringStart();
    }

    /// <summary>Call when the trinkets are done triggering.</summary>
    public static void EndScoring()
    {
        ReorderLocked = false;
        if (Instance != null) Instance.OnScoringEnd();
    }

    /// <summary>Plays the "can't do that" sound and wiggles the bar (used when the shop is full).</summary>
    public static void FlashFull()
    {
        if (Instance == null) return;
        Instance.PlaySFX(Instance.errorSound);
        for (int i = 0; i < Instance.views.Count; i++) Instance.views[i].PlayJuice(false);
    }

    // ---------------- State ----------------

    private Canvas canvas;
    private bool duplicate;
    private bool open;
    private bool openedByShop;
    private bool openedByScoring;
    private bool lastShopOpen;

    private readonly List<TrinketItemView> views = new List<TrinketItemView>();
    private readonly Stack<TrinketItemView> pool = new Stack<TrinketItemView>();
    private TrinketItemView selected;
    private TrinketItemView hovered;
    private TrinketItemView draggingView;
    private TrinketItemView infoShown;

    private PlayerMovement player;
    private float lastSpeed;
    private float nextFindTime;

    private RectTransform ToggleRect { get { return toggleButton != null ? toggleButton.transform as RectTransform : null; } }

    private Camera Cam
    {
        get { return canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null; }
    }

    // -------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            duplicate = true;
            Destroy(gameObject);
            return;
        }

        Instance = this;
        canvas = GetComponent<Canvas>();

        if (persistAcrossScenes)
        {
            if (transform.parent != null) transform.SetParent(null, false);
            DontDestroyOnLoad(gameObject);
        }

        CaptureStat(sunPower);
        CaptureStat(windPower);
        CaptureStat(moveSpeed);

        if (itemTemplate != null) itemTemplate.gameObject.SetActive(false);
        if (infoCard != null) infoCard.gameObject.SetActive(false);

        if (toggleButton != null) toggleButton.onClick.AddListener(Toggle);
        if (sellButton != null) sellButton.onClick.AddListener(SellSelected);

        if (slideRoot != null) slideRoot.localScale = new Vector3(panelScale, panelScale, 1f);

        open = startOpen;
        SnapSlide();
        UpdateArrow();
    }

    private void OnEnable()
    {
        if (duplicate || Instance != this) return;

        TrinketInventory.Added += OnInvAdded;
        TrinketInventory.Removed += OnInvRemoved;
        TrinketInventory.Moved += OnInvMoved;
        TrinketInventory.Cleared += OnInvCleared;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        TrinketInventory.Added -= OnInvAdded;
        TrinketInventory.Removed -= OnInvRemoved;
        TrinketInventory.Moved -= OnInvMoved;
        TrinketInventory.Cleared -= OnInvCleared;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            ReorderLocked = false;
        }
    }

    private void Start()
    {
        if (duplicate || Instance != this) return;
        SyncAll();
        ApplySceneVisibility();
        EnsureEventSystem();
    }

    private void Update()
    {
        if (duplicate || Instance != this) return;

        bool shopOpen = TrinketShopUI.IsAnyShopOpen;
        if (shopOpen != lastShopOpen)
        {
            lastShopOpen = shopOpen;
            OnShopStateChanged(shopOpen);
        }

        UpdateSlide();
        UpdateItems();
        UpdateStat(sunPower, TrinketStats.SunPower);
        UpdateStat(windPower, TrinketStats.WindPower);
        UpdateStat(moveSpeed, GetSpeed());
        UpdateInfoCard();
        HandleOutsideClick();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ReorderLocked = false;
        openedByScoring = false;
        Deselect();
        hovered = null;
        draggingView = null;
        player = null;
        ApplySceneVisibility();
        EnsureEventSystem();
    }

    private void ApplySceneVisibility()
    {
        if (canvas == null) return;
        bool hide = false;
        string current = SceneManager.GetActiveScene().name;
        if (hideInScenes != null)
            for (int i = 0; i < hideInScenes.Length; i++)
                if (hideInScenes[i] == current) hide = true;
        canvas.enabled = !hide;
    }

    // The bar needs an EventSystem for clicking and dragging
    private static void EnsureEventSystem()
    {
        if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    // -------------------------------------------------------------------
    // Open / close
    // -------------------------------------------------------------------

    public void Toggle()
    {
        openedByShop = false;
        openedByScoring = false;
        SetOpen(!open);
    }

    public void SetOpen(bool value, bool sound = true)
    {
        if (value == open) return;
        open = value;
        if (sound) PlaySFX(open ? openSound : closeSound);
        UpdateArrow();
        if (!open) Deselect();
    }

    private void UpdateArrow()
    {
        if (toggleArrow != null) toggleArrow.text = open ? ">" : "<";
    }

    // How far the bar must move right to be hidden (the tab stays visible). Takes the scale into account.
    private float ClosedX
    {
        get { return slideRoot != null ? slideRoot.rect.width * slideRoot.localScale.x : 0f; }
    }

    private void SnapSlide()
    {
        if (slideRoot == null) return;
        Vector2 p = slideRoot.anchoredPosition;
        p.x = open ? 0f : ClosedX;
        slideRoot.anchoredPosition = p;
    }

    private void UpdateSlide()
    {
        if (slideRoot == null) return;
        float target = open ? 0f : ClosedX;
        Vector2 p = slideRoot.anchoredPosition;
        p.x = Mathf.Lerp(p.x, target, 1f - Mathf.Exp(-slideSpeed * Time.unscaledDeltaTime));
        if (Mathf.Abs(p.x - target) < 0.1f) p.x = target;
        slideRoot.anchoredPosition = p;
    }

    private void OnShopStateChanged(bool shopOpen)
    {
        if (shopOpen)
        {
            if (openWithShop && !open)
            {
                openedByShop = true;
                SetOpen(true);
            }
        }
        else
        {
            Deselect();
            if (openedByShop && closeWhenShopCloses) SetOpen(false);
            openedByShop = false;
        }
    }

    private void OnScoringStart()
    {
        Deselect();
        if (openWhileScoring && !open && TrinketInventory.Owned.Count > 0)
        {
            openedByScoring = true;
            SetOpen(true);
        }
    }

    private void OnScoringEnd()
    {
        if (openedByScoring) SetOpen(false);
        openedByScoring = false;
    }

    // -------------------------------------------------------------------
    // Inventory -> views
    // -------------------------------------------------------------------

    private TrinketItemView GetView()
    {
        TrinketItemView v = null;
        while (pool.Count > 0 && v == null) v = pool.Pop();

        if (v == null)
        {
            GameObject go = Instantiate(itemTemplate.gameObject, itemTemplate.transform.parent);
            go.name = "TrinketItem";
            v = go.GetComponent<TrinketItemView>();
        }

        v.gameObject.SetActive(true);
        v.transform.SetSiblingIndex(itemTemplate.transform.GetSiblingIndex() + 1);
        return v;
    }

    private void Release(TrinketItemView v)
    {
        if (v == null) return;
        v.gameObject.SetActive(false);
        pool.Push(v);
    }

    private Vector2 FramePos(int index)
    {
        if (frames == null || frames.Length == 0) return Vector2.zero;
        return frames[Mathf.Clamp(index, 0, frames.Length - 1)].anchoredPosition;
    }

    private void SyncAll()
    {
        if (itemTemplate == null) return;

        for (int i = 0; i < views.Count; i++) Release(views[i]);
        views.Clear();
        selected = hovered = draggingView = infoShown = null;

        IReadOnlyList<TrinketData> owned = TrinketInventory.Owned;
        for (int i = 0; i < owned.Count; i++)
        {
            TrinketItemView v = GetView();
            v.Bind(owned[i], this);
            v.Rect.anchoredPosition = FramePos(i);
            views.Add(v);
        }
    }

    private void OnInvAdded(int index)
    {
        if (itemTemplate == null) return;
        IReadOnlyList<TrinketData> owned = TrinketInventory.Owned;
        if (index < 0 || index >= owned.Count) return;

        TrinketItemView v = GetView();
        v.Bind(owned[index], this);
        index = Mathf.Clamp(index, 0, views.Count);
        views.Insert(index, v);
        v.Rect.anchoredPosition = FramePos(index);
        v.PlaySpawn();
        PlaySFX(addSound);
    }

    private void OnInvRemoved(int index)
    {
        if (index < 0 || index >= views.Count) return;

        TrinketItemView v = views[index];
        views.RemoveAt(index);
        if (selected == v) selected = null;
        if (hovered == v) hovered = null;
        if (draggingView == v) draggingView = null;
        if (infoShown == v) infoShown = null;

        v.SetSelected(false);
        v.PlayRemove(() => Release(v));
    }

    private void OnInvMoved(int from, int to)
    {
        if (from < 0 || from >= views.Count) return;
        to = Mathf.Clamp(to, 0, views.Count - 1);
        TrinketItemView v = views[from];
        views.RemoveAt(from);
        views.Insert(to, v);
    }

    private void OnInvCleared()
    {
        SyncAll();
    }

    private void UpdateItems()
    {
        float k = 1f - Mathf.Exp(-itemMoveSpeed * Time.unscaledDeltaTime);
        for (int i = 0; i < views.Count; i++)
        {
            TrinketItemView v = views[i];
            if (v == null || v == draggingView) continue;
            v.Rect.anchoredPosition = Vector2.Lerp(v.Rect.anchoredPosition, FramePos(i), k);
        }
    }

    // -------------------------------------------------------------------
    // Hover / select / sell
    // -------------------------------------------------------------------

    internal void OnItemHover(TrinketItemView v, bool enter)
    {
        if (enter) hovered = v;
        else if (hovered == v) hovered = null;
    }

    internal void OnItemClicked(TrinketItemView v)
    {
        if (draggingView != null) return;

        if (selected == v)
        {
            Deselect();
            return;
        }

        Deselect();
        selected = v;
        v.SetSelected(true);
        PlaySFX(selectSound);
    }

    private void Deselect()
    {
        if (selected != null) selected.SetSelected(false);
        selected = null;
    }

    private void HandleOutsideClick()
    {
        if (selected == null || !Input.GetMouseButtonDown(0)) return;
        if (Over(infoCard)) return;
        for (int i = 0; i < views.Count; i++)
            if (views[i] != null && Over(views[i].Rect)) return;
        Deselect();
    }

    /// <summary>Removes the selected trinket. Only works while the shop is open.</summary>
    public void SellSelected()
    {
        if (selected == null || !TrinketShopUI.IsAnyShopOpen) return;

        int index = views.IndexOf(selected);
        if (index < 0 || index >= TrinketInventory.Owned.Count) return;

        TrinketData sold = TrinketInventory.Owned[index];
        Deselect();
        TrinketInventory.RemoveAt(index);
        PlaySFX(sellSound);

        TrinketShopUI shop = TrinketShopUI.OpenShop;
        if (shop != null) shop.NotifySold(sold);
    }

    // -------------------------------------------------------------------
    // Drag to reorder
    // -------------------------------------------------------------------

    internal bool TryBeginDrag(TrinketItemView v)
    {
        if (ReorderLocked || !views.Contains(v))
        {
            PlaySFX(errorSound);
            return false;
        }

        Deselect();
        draggingView = v;
        v.Rect.SetAsLastSibling();
        PlaySFX(pickupSound);
        return true;
    }

    internal void Drag(TrinketItemView v, PointerEventData e)
    {
        if (draggingView != v || slideRoot == null) return;

        Vector2 local;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(slideRoot, e.position, Cam, out local)) return;

        // local is relative to the panel's pivot; children are anchored to its centre
        Vector2 pos = local - slideRoot.rect.center;
        float frameX = FramePos(0).x;
        pos.x = Mathf.Clamp(pos.x, frameX - 60f, frameX + 60f);
        v.Rect.anchoredPosition = pos;

        int current = views.IndexOf(v);
        int target = NearestSlot(pos.y);
        target = Mathf.Clamp(target, 0, views.Count - 1);

        if (target != current)
        {
            TrinketInventory.Move(current, target);
            PlaySFX(reorderSound, 1f + 0.05f * target);
        }
    }

    internal void EndDrag(TrinketItemView v)
    {
        if (draggingView == v) draggingView = null;
        PlaySFX(dropSound);
    }

    private int NearestSlot(float y)
    {
        int best = 0;
        float bestDist = float.MaxValue;
        for (int i = 0; i < frames.Length; i++)
        {
            float d = Mathf.Abs(frames[i].anchoredPosition.y - y);
            if (d < bestDist) { bestDist = d; best = i; }
        }
        return best;
    }

    // -------------------------------------------------------------------
    // Trigger animation (called while scoring)
    // -------------------------------------------------------------------

    private void PulseInternal(int index, string label)
    {
        if (index < 0 || index >= views.Count || string.IsNullOrEmpty(label)) return;

        TrinketItemView v = views[index];
        bool fail = label.StartsWith("Nope");
        bool multiply = label.StartsWith("X") || label.StartsWith("x");

        v.PlayJuice(!fail);
        StartCoroutine(PopupRoutine(v.Rect.anchoredPosition, label,
            fail ? failPopupColor : (multiply ? multiplyPopupColor : addPopupColor)));
    }

    private IEnumerator PopupRoutine(Vector2 itemPos, string label, Color color)
    {
        if (slideRoot == null) yield break;

        GameObject go = new GameObject("TrinketPopup", typeof(RectTransform), typeof(CanvasGroup));
        RectTransform r = go.GetComponent<RectTransform>();
        r.SetParent(slideRoot, false);
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.pivot = new Vector2(1f, 0.5f);
        r.sizeDelta = new Vector2(210f, 56f);
        r.SetAsLastSibling();

        Image bg = go.AddComponent<Image>();
        bg.color = color;
        bg.raycastTarget = false;

        GameObject textGO = new GameObject("Text", typeof(RectTransform));
        RectTransform tr = textGO.GetComponent<RectTransform>();
        tr.SetParent(r, false);
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.offsetMin = tr.offsetMax = Vector2.zero;
        TextMeshProUGUI t = textGO.AddComponent<TextMeshProUGUI>();
        t.text = label;
        t.fontSize = 32;
        t.fontStyle = FontStyles.Bold;
        t.alignment = TextAlignmentOptions.Center;
        t.color = Color.white;
        t.raycastTarget = false;

        CanvasGroup cg = go.GetComponent<CanvasGroup>();
        Vector2 start = itemPos + new Vector2(-popupOffsetX, 0f);

        const float duration = 1f;
        float time = 0f;
        while (time < duration)
        {
            if (go == null) yield break;
            time += Time.unscaledDeltaTime;
            float x = Mathf.Clamp01(time / duration);

            // pop in with a little overshoot, then drift up and fade
            float pop = Mathf.Clamp01(x / 0.15f);
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float eased = 1f + c3 * Mathf.Pow(pop - 1f, 3f) + c1 * Mathf.Pow(pop - 1f, 2f);

            r.localScale = Vector3.one * eased;
            r.anchoredPosition = start + Vector2.up * (28f * x);
            cg.alpha = x < 0.7f ? 1f : 1f - (x - 0.7f) / 0.3f;
            yield return null;
        }

        if (go != null) Destroy(go);
    }

    // -------------------------------------------------------------------
    // Stats
    // -------------------------------------------------------------------

    private void CaptureStat(StatRow row)
    {
        if (row == null || row.valueText == null) return;
        row.baseColor = row.valueText.color;
        row.baseScale = row.valueText.transform.localScale;
    }

    private float GetSpeed()
    {
        if (player == null && Time.unscaledTime >= nextFindTime)
        {
            nextFindTime = Time.unscaledTime + 1f;
            player = UnityEngine.Object.FindAnyObjectByType<PlayerMovement>();
        }

        // In scenes without a player (the card scene) the last known value is kept
        if (player != null) lastSpeed = speedShows == SpeedStat.Run ? player.runSpeed : player.walkSpeed;
        return lastSpeed;
    }

    private void UpdateStat(StatRow row, float value)
    {
        if (row == null || row.valueText == null) return;

        if (float.IsNaN(row.last))
        {
            row.last = value;
            row.valueText.text = value.ToString("0.##");
            return;
        }

        if (Mathf.Approximately(value, row.last)) return;

        bool up = value > row.last;
        row.last = value;
        row.valueText.text = value.ToString("0.##");
        PlaySFX(up ? statUpSound : statDownSound);

        if (row.flash != null) StopCoroutine(row.flash);
        row.flash = StartCoroutine(FlashStat(row, up ? statUpColor : statDownColor));
    }

    private IEnumerator FlashStat(StatRow row, Color flashColor)
    {
        const float duration = 0.7f;
        float t = 0f;
        while (t < duration)
        {
            if (row.valueText == null) yield break;
            t += Time.unscaledDeltaTime;
            float x = Mathf.Clamp01(t / duration);
            row.valueText.color = Color.Lerp(flashColor, row.baseColor, x);
            row.valueText.transform.localScale = row.baseScale * (1f + 0.35f * Mathf.Sin(Mathf.Min(x * 3f, 1f) * Mathf.PI));
            yield return null;
        }
        row.valueText.color = row.baseColor;
        row.valueText.transform.localScale = row.baseScale;
        row.flash = null;
    }

    // -------------------------------------------------------------------
    // Info card
    // -------------------------------------------------------------------

    private void UpdateInfoCard()
    {
        if (infoCard == null) return;

        TrinketItemView target = draggingView != null ? null : (selected != null ? selected : hovered);
        if (target != null && (!open || !views.Contains(target))) target = null;

        if (target == null)
        {
            if (infoCard.gameObject.activeSelf) infoCard.gameObject.SetActive(false);
            infoShown = null;
            return;
        }

        if (!infoCard.gameObject.activeSelf) infoCard.gameObject.SetActive(true);

        if (infoShown != target)
        {
            infoShown = target;
            if (infoName != null) infoName.text = target.data != null ? target.data.trinketName : "";
            if (infoDescription != null) infoDescription.text = target.data != null ? target.data.description : "";
        }

        bool isSelected = selected == target;
        bool shop = TrinketShopUI.IsAnyShopOpen;
        if (sellButton != null) sellButton.gameObject.SetActive(isSelected && shop);
        if (infoHint != null)
        {
            infoHint.gameObject.SetActive(isSelected && !shop);
            infoHint.text = "Sell trinkets at the shop";
        }

        // Sits to the left of the tab, level with the trinket
        float tabWidth = ToggleRect != null ? ToggleRect.rect.width : 0f;
        float x = -(slideRoot.rect.width * 0.5f + tabWidth + 14f + infoCard.rect.width * 0.5f);
        float limit = slideRoot.rect.height * 0.5f - infoCard.rect.height * 0.5f;
        float y = Mathf.Clamp(target.Rect.anchoredPosition.y, -limit, limit);
        Vector2 p = infoCard.anchoredPosition;
        p.x = x;
        p.y = Mathf.Lerp(p.y, y, 1f - Mathf.Exp(-20f * Time.unscaledDeltaTime));
        infoCard.anchoredPosition = p;
    }

    // -------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------

    private bool Over(RectTransform r)
    {
        return r != null && r.gameObject.activeInHierarchy &&
               RectTransformUtility.RectangleContainsScreenPoint(r, Input.mousePosition, Cam);
    }

    private bool PointerInside()
    {
        if (canvas != null && !canvas.enabled) return false;
        return draggingView != null || Over(slideRoot) || Over(ToggleRect) || Over(infoCard);
    }

    private void PlaySFX(AudioClip clip, float pitch = 1f)
    {
        if (clip == null) return;
        GameSFX.Play(clip, sfxVolume, pitch);
    }
}