using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// The Active Buffs bar on the LEFT side of the screen (the Trinket bar is on the right).
/// Compact version: every section (Mood, Boons, Trinkets, Advantage Hit) is just an icon.
/// Hover an icon to see its stats in a tooltip next to it.
/// - Mood: what the current mood gives you (Power, Mult, hands...).
/// - Boons: your permanent boons.
/// - Trinkets: only the stat values your trinkets give (trinkets that give no stats are not listed).
/// - Advantage Hit: the score-to-beat reduction from hitting the enemy with a card (during that fight).
/// - At the bottom: the overall stats (Sun Power, Wind Power, Speed).
/// Build it with  Tools > HUD > Create Active Buffs Bar.
/// Swap the section icons by changing the sprite on each section's Icon image.
/// It survives scene loads. If a scene contains a second copy, the copy deletes itself.
/// </summary>
public class ActiveBuffsSidebar : MonoBehaviour
{
    [Serializable]
    public class Section
    {
        [Tooltip("Shown as the tooltip title.")]
        public string title;
        [Tooltip("The square button for this section. Hover it to open the tooltip.")]
        public RectTransform box;
        [Tooltip("The section icon. Change its sprite to change the icon. Dimmed while the section is empty.")]
        public Image icon;
        [Tooltip("Letter shown while the icon has no sprite yet.")]
        public TMP_Text fallbackLabel;

        [NonSerialized] public List<RowData> data = new List<RowData>();
    }

    public class RowData
    {
        public Sprite icon;
        public string text;
        public Color color;
    }

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
    [Tooltip("The bar itself. It slides in and out to the left.")]
    public RectTransform slideRoot;
    [Tooltip("Size of the whole bar. Use the same number as the Trinket bar's Panel Scale so they match.")]
    [Range(0.3f, 1.5f)] public float panelScale = 0.75f;
    [Tooltip("The object that holds the section icons (it has the vertical layout on it).")]
    public RectTransform layoutRoot;
    public Button toggleButton;
    public TMP_Text toggleArrow;
    [Tooltip("A hidden row (Icon + Label) that gets cloned for every line in the tooltip.")]
    public RectTransform rowTemplate;

    [Header("Tooltip")]
    [Tooltip("The popup that appears next to a hovered icon. Rows are added inside it.")]
    public RectTransform tooltip;
    public TMP_Text tooltipTitle;
    public string emptyText = "None";
    [Tooltip("Gap between the icon and the tooltip (in reference pixels).")]
    public float tooltipGap = 10f;
    [Tooltip("Also write the trinket's name in front of its stats in the Trinkets tooltip.")]
    public bool showTrinketNames = false;
    [Tooltip("Number fields on TrinketData that are NOT stats (e.g. price). Matching is not case sensitive.")]
    public string[] ignoredTrinketFields = { "price", "cost", "sellPrice", "buyPrice", "rarity", "weight", "id", "index" };

    [Header("Sections")]
    public Section moodSection = new Section();
    public Section boonsSection = new Section();
    public Section trinketsSection = new Section();
    public Section advantageSection = new Section();
    [Tooltip("Blueprint Shop structure (Solar Panel / Windmill) and its upgrades. If left empty, the rows are shown under Boons.")]
    public Section structureSection = new Section();

    [Header("Stats (bottom of the bar)")]
    public StatRow sunPower = new StatRow();
    public StatRow windPower = new StatRow();
    public StatRow moveSpeed = new StatRow();
    [Tooltip("Drag your Solar card asset here. Its current value is the Sun stat (leave empty to find it automatically).")]
    public SolarCard sunCard;
    [Tooltip("Drag your Wind card asset here. Its current value is the Wind stat (leave empty to find it automatically).")]
    public WindCard windCard;
    public Color statUpColor = new Color(0.4f, 1f, 0.4f);
    public Color statDownColor = new Color(1f, 0.4f, 0.4f);

    [Header("Icons (taken from the Trinket bar's stat icons if left empty)")]
    public Sprite sunIcon;
    public Sprite windIcon;

    [Header("Colours")]
    public Color buffColor = Color.white;
    public Color dimColor = new Color(1f, 1f, 1f, 0.6f);
    public Color advantageColor = new Color(0.55f, 1f, 0.55f);
    [Tooltip("Section icon colour while the section has something in it.")]
    public Color iconActiveColor = Color.white;
    [Tooltip("Section icon colour while the section is empty.")]
    public Color iconEmptyColor = new Color(1f, 1f, 1f, 0.3f);

    // ---------------- Behaviour ----------------

    [Header("Behaviour")]
    public bool persistAcrossScenes = true;
    public bool startOpen = false;
    [Tooltip("Scene names where the bar is hidden completely. The Trinket bar's list is used too.")]
    public string[] hideInScenes;

    [Header("Animation")]
    public float slideSpeed = 12f;

    [Header("Position")]
    [Tooltip("Moves the whole bar down (reference pixels) so it does not overlap the star / money counter. Raise it to go lower.")]
    public float lowerBy = 60f;

    [Header("Sounds")]
    [Range(0f, 1f)] public float sfxVolume = 1f;
    public AudioClip openSound;
    public AudioClip closeSound;

    // ---------------- Static API ----------------

    public static ActiveBuffsSidebar Instance { get; private set; }

    /// <summary>True while the mouse is over the bar. Used to stop clicks leaking into the world.</summary>
    public static bool IsPointerOver
    {
        get { return Instance != null && Instance.PointerInside(); }
    }

    // ---------------- State ----------------

    private Canvas canvas;
    private bool duplicate;
    private bool open;
    private string lastSignature;
    private bool iconsBorrowed;
    private float nextCardSearch;

    private PlayerMovement player;
    private float lastSpeed;
    private float nextFindTime;

    private Section hovered;
    private bool tooltipDirty;
    private readonly List<GameObject> tooltipRows = new List<GameObject>();
    private readonly Vector3[] corners = new Vector3[4];
    private FieldInfo[] trinketFields;

    private RectTransform ToggleRect { get { return toggleButton != null ? toggleButton.transform as RectTransform : null; } }

    private Camera Cam
    {
        get { return canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null; }
    }

    private Section[] AllSections()
    {
        return new Section[] { moodSection, boonsSection, trinketsSection, advantageSection, structureSection };
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

        if (rowTemplate != null) rowTemplate.gameObject.SetActive(false);
        if (toggleButton != null) toggleButton.onClick.AddListener(Toggle);
        if (slideRoot != null) slideRoot.localScale = new Vector3(panelScale, panelScale, 1f);
        EnsureStructureSection();
        if (slideRoot != null)
        {
            Vector2 sp = slideRoot.anchoredPosition;
            sp.y -= lowerBy;
            slideRoot.anchoredPosition = sp;
        }
        if (tooltip != null)
        {
            tooltip.localScale = new Vector3(panelScale, panelScale, 1f);
            tooltip.gameObject.SetActive(false);
        }

        open = startOpen;
        SnapSlide();
        UpdateArrow();
    }

    // Bars made before the Blueprint square existed get one added here: a copy of the last square, with the bar made taller.
    private void EnsureStructureSection()
    {
        if (structureSection != null && structureSection.box != null) return;

        Section src = (advantageSection != null && advantageSection.box != null) ? advantageSection : boonsSection;
        if (src == null || src.box == null || slideRoot == null) return;

        RectTransform box = Instantiate(src.box, src.box.parent);
        box.name = "Blueprint";
        box.SetSiblingIndex(src.box.GetSiblingIndex() + 1);

        Transform iconTf = box.Find("Icon");
        Transform letterTf = box.Find("Letter");

        structureSection = new Section();
        structureSection.title = "Blueprint";
        structureSection.box = box;
        structureSection.icon = iconTf != null ? iconTf.GetComponent<Image>() : null;
        structureSection.fallbackLabel = letterTf != null ? letterTf.GetComponent<TMP_Text>() : null;
        if (structureSection.icon != null) structureSection.icon.sprite = null; // shows the letter "B" until you assign an icon

        // Make room for the extra square
        float spacing = 0f;
        if (layoutRoot != null)
        {
            VerticalLayoutGroup vlg = layoutRoot.GetComponent<VerticalLayoutGroup>();
            if (vlg != null) spacing = vlg.spacing;
        }
        LayoutElement le = box.GetComponent<LayoutElement>();
        float added = (le != null && le.preferredHeight > 0f ? le.preferredHeight : box.rect.height) + spacing;

        Vector2 size = slideRoot.sizeDelta;
        size.y += added;
        slideRoot.sizeDelta = size;

        // The bar grows up and down equally: move it down by half so the top stays where it was
        Vector2 pos = slideRoot.anchoredPosition;
        pos.y -= added * panelScale * 0.5f;
        slideRoot.anchoredPosition = pos;
    }

    private void OnEnable()
    {
        if (duplicate || Instance != this) return;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        if (duplicate || Instance != this) return;
        ApplySceneVisibility();
        EnsureEventSystem();
    }

    private void Update()
    {
        if (duplicate || Instance != this) return;

        BorrowIcons();
        UpdateSlide();
        RefreshBuffs();
        UpdateHover();
        UpdateStat(sunPower, GetSun());
        UpdateStat(windPower, GetWind());
        UpdateStat(moveSpeed, GetSpeed());
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        player = null;
        lastSignature = null; // rebuild the rows
        HideTooltip();
        ApplySceneVisibility();
        EnsureEventSystem();
    }

    private void ApplySceneVisibility()
    {
        if (canvas == null) return;

        bool hide = false;
        string current = SceneManager.GetActiveScene().name;
        hide |= Contains(hideInScenes, current);
        if (TrinketSidebar.Instance != null) hide |= Contains(TrinketSidebar.Instance.hideInScenes, current);
        canvas.enabled = !hide;
    }

    private static bool Contains(string[] list, string name)
    {
        if (list == null) return false;
        for (int i = 0; i < list.Length; i++)
            if (list[i] == name) return true;
        return false;
    }

    private static void EnsureEventSystem()
    {
        if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    // The stat icons come from the Trinket bar (where the stats used to be) unless you set your own
    private void BorrowIcons()
    {
        if (iconsBorrowed || TrinketSidebar.Instance == null) return;
        iconsBorrowed = true;

        TrinketSidebar t = TrinketSidebar.Instance;
        Borrow(sunPower, t.sunPower);
        Borrow(windPower, t.windPower);
        Borrow(moveSpeed, t.moveSpeed);

        if (sunIcon == null && sunPower.icon != null) sunIcon = sunPower.icon.sprite;
        if (windIcon == null && windPower.icon != null) windIcon = windPower.icon.sprite;
        lastSignature = null;
    }

    private static void Borrow(StatRow mine, TrinketSidebar.StatRow theirs)
    {
        if (mine == null || theirs == null || mine.icon == null || theirs.icon == null) return;
        if (mine.icon.sprite == null) mine.icon.sprite = theirs.icon.sprite;
    }

    // -------------------------------------------------------------------
    // Open / close
    // -------------------------------------------------------------------

    public void Toggle()
    {
        SetOpen(!open);
    }

    public void SetOpen(bool value, bool sound = true)
    {
        if (value == open) return;
        open = value;
        if (sound) PlaySFX(open ? openSound : closeSound);
        UpdateArrow();
    }

    private void UpdateArrow()
    {
        if (toggleArrow != null) toggleArrow.text = open ? "<" : ">";
    }

    // How far the bar must move left to be hidden (the tab stays visible)
    private float ClosedX
    {
        get { return slideRoot != null ? -slideRoot.rect.width * slideRoot.localScale.x : 0f; }
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

    // -------------------------------------------------------------------
    // Buff data
    // -------------------------------------------------------------------

    private void RefreshBuffs()
    {
        string signature = BuildSignature();
        if (signature == lastSignature) return;
        lastSignature = signature;
        RebuildData();
    }

    // Everything the tooltips show, as one string: the data is only rebuilt when it changes
    private string BuildSignature()
    {
        StringBuilder sb = new StringBuilder(96);
        sb.Append(MoodManager.IsNeutral ? "neutral" : MoodManager.Current.name).Append('|')
          .Append(MoodManager.PowerBonus).Append('|')
          .Append(MoodManager.ScoreMultiplier).Append('|')
          .Append(MoodManager.HandsModifier).Append('|')
          .Append(MoodManager.ReshufflesModifier).Append('|')
          .Append(MoodManager.FightsLeft).Append('|')
          .Append(BoonProgress.BonusWindPower).Append('|')
          .Append(BoonProgress.ExtraHands).Append('|')
          .Append(BoonProgress.ExtraHandSize).Append('|')
          .Append(AdvantageHit.IsActive).Append(AdvantageHit.Reduction).Append('|')
          .Append((int)StructureProgress.Current).Append(StructureProgress.Level(StructureUpgrade.Power))
          .Append(StructureProgress.Level(StructureUpgrade.Stability)).Append(StructureProgress.Level(StructureUpgrade.Mult)).Append('|');

        IReadOnlyList<TrinketData> owned = TrinketInventory.Owned;
        for (int i = 0; i < owned.Count; i++)
            sb.Append(owned[i] != null ? owned[i].trinketName : "?").Append(';');

        return sb.ToString();
    }

    private void RebuildData()
    {
        Section[] all = AllSections();
        for (int i = 0; i < all.Length; i++)
            if (all[i] != null) all[i].data.Clear();

        // Mood
        if (!MoodManager.IsNeutral)
        {
            int power = MoodManager.PowerBonus;
            if (power != 0)
            {
                // "+50 Power to all Power types"
                AddRow(moodSection, sunIcon, Signed(power), buffColor);
                AddRow(moodSection, windIcon, Signed(power), buffColor);
            }

            float mult = MoodManager.ScoreMultiplier;
            if (mult > 1.001f) AddRow(moodSection, null, "X" + mult.ToString("0.##") + " Mult", buffColor);
            if (MoodManager.HandsModifier != 0) AddRow(moodSection, null, Signed(MoodManager.HandsModifier) + " Hand", buffColor);
            if (MoodManager.ReshufflesModifier != 0) AddRow(moodSection, null, Signed(MoodManager.ReshufflesModifier) + " Reshuffle", buffColor);

            int left = MoodManager.FightsLeft;
            if (left > 0) AddRow(moodSection, null, left + (left == 1 ? " fight left" : " fights left"), dimColor);
        }

        // Boons
        if (BoonProgress.BonusWindPower > 0) AddRow(boonsSection, windIcon, Signed(BoonProgress.BonusWindPower), buffColor);
        if (BoonProgress.ExtraHands > 0) AddRow(boonsSection, null, Signed(BoonProgress.ExtraHands) + " Hand", buffColor);
        if (BoonProgress.ExtraHandSize > 0) AddRow(boonsSection, null, Signed(BoonProgress.ExtraHandSize) + " Hand Size", buffColor);

        // Blueprint Shop structure (falls back to the Boons icon if there is no Structure section in the bar)
        if (StructureProgress.IsBuilt)
        {
            StructureType st = StructureProgress.Current;
            Section ss = (structureSection != null && structureSection.box != null) ? structureSection : boonsSection;
            Sprite cardIcon = st == StructureType.Solar ? sunIcon : windIcon;

            AddRow(ss, null, StructureProgress.DisplayName(st), dimColor);

            int sPower = StructureProgress.PowerBonus(st);
            if (sPower > 0) AddRow(ss, cardIcon, Signed(sPower), buffColor);

            int stab = StructureProgress.Level(StructureUpgrade.Stability);
            if (stab > 0)
            {
                int pct = Mathf.RoundToInt((1f - StructureProgress.LowPowerChanceFactor(st)) * 100f);
                AddRow(ss, null, "-" + pct + "% Low Power chance", buffColor);
            }

            float sMult = StructureProgress.MultBonus(st);
            if (sMult > 0f) AddRow(ss, null, "+" + sMult.ToString("0.##") + " " + StructureProgress.CardName(st) + " Set Mult", buffColor);
        }

        // Trinkets: only the stat values they give. A trinket without stats adds nothing.
        IReadOnlyList<TrinketData> owned = TrinketInventory.Owned;
        for (int i = 0; i < owned.Count; i++)
            if (owned[i] != null) AddTrinketStats(owned[i]);

        // Advantage Hit
        if (AdvantageHit.IsActive)
            AddRow(advantageSection, null, "-" + Mathf.RoundToInt(AdvantageHit.Reduction * 100f) + "% Score", advantageColor);

        for (int i = 0; i < all.Length; i++) RefreshIcon(all[i]);

        tooltipDirty = true;
    }

    private static string Signed(float value)
    {
        return (value > 0f ? "+" : "") + value.ToString("0.##");
    }

    private void AddRow(Section s, Sprite icon, string text, Color color)
    {
        if (s == null) return;
        s.data.Add(new RowData { icon = icon, text = text, color = color });
    }

    // Dim the icon while empty, show a letter while it has no sprite
    private void RefreshIcon(Section s)
    {
        if (s == null) return;
        bool has = s.data.Count > 0;
        bool hasSprite = s.icon != null && s.icon.sprite != null;

        if (s.icon != null)
        {
            s.icon.enabled = hasSprite;
            s.icon.color = has ? iconActiveColor : iconEmptyColor;
        }
        if (s.fallbackLabel != null)
        {
            s.fallbackLabel.gameObject.SetActive(!hasSprite);
            if (!hasSprite)
            {
                s.fallbackLabel.text = string.IsNullOrEmpty(s.title) ? "?" : s.title.Substring(0, 1);
                Color c = has ? iconActiveColor : iconEmptyColor;
                s.fallbackLabel.color = c;
            }
        }
    }

    // -------------------------------------------------------------------
    // Trinket stats
    // -------------------------------------------------------------------

    // Reads every number field on the trinket (except the ones in 'ignoredTrinketFields') and lists the ones that are not 0.
    // Fields with "sun" / "wind" in the name get the Sun / Wind icon.
    private void AddTrinketStats(TrinketData t)
    {
        if (trinketFields == null) trinketFields = FindTrinketFields();

        bool nameShown = false;
        for (int i = 0; i < trinketFields.Length; i++)
        {
            FieldInfo f = trinketFields[i];
            object raw = f.GetValue(t);
            float v = raw is int ? (int)raw : (float)raw;
            if (Mathf.Approximately(v, 0f)) continue;

            string lower = f.Name.ToLowerInvariant();
            Sprite icon = null;
            if (lower.Contains("sun")) icon = sunIcon;
            else if (lower.Contains("wind")) icon = windIcon;

            string text = Signed(v) + (icon != null ? "" : " " + Prettify(f.Name));

            if (showTrinketNames && !nameShown)
            {
                AddRow(trinketsSection, null, t.trinketName, dimColor);
                nameShown = true;
            }
            AddRow(trinketsSection, icon, text, buffColor);
        }
    }

    private FieldInfo[] FindTrinketFields()
    {
        List<FieldInfo> list = new List<FieldInfo>();
        FieldInfo[] all = typeof(TrinketData).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        for (int i = 0; i < all.Length; i++)
        {
            FieldInfo f = all[i];
            if (f.FieldType != typeof(int) && f.FieldType != typeof(float)) continue;
            if (!f.IsPublic && !Attribute.IsDefined(f, typeof(SerializeField))) continue;
            if (IsIgnoredTrinketField(f.Name)) continue;
            list.Add(f);
        }
        return list.ToArray();
    }

    private bool IsIgnoredTrinketField(string name)
    {
        if (ignoredTrinketFields == null) return false;
        for (int i = 0; i < ignoredTrinketFields.Length; i++)
            if (string.Equals(ignoredTrinketFields[i], name, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    // "bonusWindPower" / "_bonusWindPower" -> "Bonus Wind Power"
    private static string Prettify(string fieldName)
    {
        string n = fieldName.TrimStart('_');
        StringBuilder sb = new StringBuilder(n.Length + 4);
        for (int i = 0; i < n.Length; i++)
        {
            char c = n[i];
            if (i == 0) c = char.ToUpperInvariant(c);
            else if (char.IsUpper(c)) sb.Append(' ');
            sb.Append(c);
        }
        return sb.ToString();
    }

    // -------------------------------------------------------------------
    // Hover tooltip
    // -------------------------------------------------------------------

    private void UpdateHover()
    {
        if (tooltip == null) return;

        Section now = null;
        if (canvas != null && canvas.enabled)
        {
            Section[] all = AllSections();
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && Over(all[i].box)) { now = all[i]; break; }
            }
        }

        if (now == null)
        {
            if (hovered != null) HideTooltip();
            return;
        }

        if (now != hovered || tooltipDirty)
        {
            hovered = now;
            tooltipDirty = false;
            BuildTooltip(now);
        }

        PositionTooltip(now);
    }

    private void BuildTooltip(Section s)
    {
        for (int i = 0; i < tooltipRows.Count; i++)
        {
            if (tooltipRows[i] == null) continue;
            tooltipRows[i].SetActive(false); // out of the layout right now, destroyed at the end of the frame
            Destroy(tooltipRows[i]);
        }
        tooltipRows.Clear();

        if (tooltipTitle != null) tooltipTitle.text = s.title;

        if (s.data.Count == 0)
        {
            AddTooltipRow(null, emptyText, new Color(1f, 1f, 1f, 0.45f));
        }
        else
        {
            for (int i = 0; i < s.data.Count; i++)
                AddTooltipRow(s.data[i].icon, s.data[i].text, s.data[i].color);
        }

        if (!tooltip.gameObject.activeSelf)
        {
            tooltip.gameObject.SetActive(true);
            tooltip.SetAsLastSibling();
        }
        LayoutRebuilder.ForceRebuildLayoutImmediate(tooltip);
    }

    private void AddTooltipRow(Sprite icon, string text, Color color)
    {
        if (rowTemplate == null) return;

        GameObject go = Instantiate(rowTemplate.gameObject, tooltip);
        go.name = "Row";
        go.SetActive(true);

        Transform iconTf = go.transform.Find("Icon");
        if (iconTf != null)
        {
            Image img = iconTf.GetComponent<Image>();
            bool show = icon != null && img != null;
            iconTf.gameObject.SetActive(show);
            if (show) img.sprite = icon;
        }

        TMP_Text label = go.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.text = text;
            label.color = color;
        }

        tooltipRows.Add(go);
    }

    // Right of the hovered icon, vertically centred on it and kept on the screen
    private void PositionTooltip(Section s)
    {
        if (s.box == null) return;

        s.box.GetWorldCorners(corners);
        Vector3 anchor = (corners[2] + corners[3]) * 0.5f;
        float scaleFactor = canvas != null ? canvas.scaleFactor : 1f;
        anchor.x += tooltipGap * scaleFactor;

        float halfHeight = tooltip.rect.height * tooltip.lossyScale.y * 0.5f;
        anchor.y = Mathf.Clamp(anchor.y, halfHeight, Mathf.Max(halfHeight, Screen.height - halfHeight));

        tooltip.position = anchor;
    }

    private void HideTooltip()
    {
        hovered = null;
        if (tooltip != null && tooltip.gameObject.activeSelf) tooltip.gameObject.SetActive(false);
    }

    // -------------------------------------------------------------------
    // Stats
    // -------------------------------------------------------------------

    private void CaptureStat(StatRow row)
    {
        if (row == null || row.valueText == null) return;

        // Make long numbers (3+ digits) shrink to fit the space next to the icon instead of wrapping below it.
        // Done here so a bar that is already in the scene gets fixed without being rebuilt.
        TMP_Text txt = row.valueText;
        txt.enableAutoSizing = true;
        txt.fontSizeMax = txt.fontSize;
        txt.fontSizeMin = Mathf.Min(14f, txt.fontSize);
#pragma warning disable 618
        txt.enableWordWrapping = false;
#pragma warning restore 618
        txt.overflowMode = TextOverflowModes.Overflow;

        LayoutElement tle = txt.GetComponent<LayoutElement>();
        if (tle == null) tle = txt.gameObject.AddComponent<LayoutElement>();
        tle.minWidth = 0f;
        tle.preferredWidth = 0f;
        tle.flexibleWidth = 1f;

        if (row.icon != null)
        {
            LayoutElement ile = row.icon.GetComponent<LayoutElement>();
            if (ile != null && ile.preferredWidth > 0f) ile.minWidth = ile.preferredWidth; // the icon never shrinks
        }

        row.baseColor = txt.color;
        row.baseScale = txt.transform.localScale;
    }

    // The power cards' current value: base value + boons + mood. Always shown, even with no buff active.
    private float GetSun()
    {
        if (sunCard == null && Time.unscaledTime >= nextCardSearch)
        {
            nextCardSearch = Time.unscaledTime + 1f;
            sunCard = FindLoaded<SolarCard>();
        }
        return sunCard != null ? sunCard.GetEffectiveValue() : TrinketStats.SunPower;
    }

    private float GetWind()
    {
        if (windCard == null && Time.unscaledTime >= nextCardSearch)
        {
            nextCardSearch = Time.unscaledTime + 1f;
            windCard = FindLoaded<WindCard>();
        }
        return windCard != null ? windCard.GetEffectiveValue() : TrinketStats.WindPower;
    }

    private static T FindLoaded<T>() where T : UnityEngine.Object
    {
        T[] all = Resources.FindObjectsOfTypeAll<T>();
        return all.Length > 0 ? all[0] : null;
    }

    private float GetSpeed()
    {
        if (player == null && Time.unscaledTime >= nextFindTime)
        {
            nextFindTime = Time.unscaledTime + 1f;
            player = UnityEngine.Object.FindAnyObjectByType<PlayerMovement>();
        }

        // In scenes without a player (the card scene) the last known value is kept
        if (player != null)
        {
            bool run = TrinketSidebar.Instance != null && TrinketSidebar.Instance.speedShows == TrinketSidebar.SpeedStat.Run;
            lastSpeed = run ? player.runSpeed : player.walkSpeed;
        }
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

        // Same sounds as the Trinket bar used for stat changes
        TrinketSidebar t = TrinketSidebar.Instance;
        if (t != null)
        {
            AudioClip clip = up ? t.statUpSound : t.statDownSound;
            if (clip != null) GameSFX.Play(clip, t.sfxVolume, 1f);
        }

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
        return Over(slideRoot) || Over(ToggleRect);
    }

    private void PlaySFX(AudioClip clip, float pitch = 1f)
    {
        if (clip == null) return;
        GameSFX.Play(clip, sfxVolume, pitch);
    }
}