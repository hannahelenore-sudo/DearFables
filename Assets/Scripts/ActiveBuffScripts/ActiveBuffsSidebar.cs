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
/// New look: the same hand-drawn ruled card as the Stats page of the GameMenu (b_gg.png).
///   - Top of the card:  Sun / Wind / Speed numbers.
///   - On the ruled lines: Moods, Boons, Blueprints, Trinkets, Advantage hit (always visible, no hovering needed).
/// Click the little tab on the card's right edge to slide it in / out.
/// Build it with  Tools > HUD > Create Active Buffs Bar  (delete the old bar first).
/// It survives scene loads. If a scene contains a second copy, the copy deletes itself.
/// </summary>
public class ActiveBuffsSidebar : MonoBehaviour
{
    [Serializable]
    public class Section
    {
        [Tooltip("Written in front of the line, e.g. \"Moods\".")]
        public string title;
        [Tooltip("The text on the card's ruled line.")]
        public TMP_Text text;
        [Tooltip("Only used by Trinkets: extra ruled lines to continue on when the first one is full.")]
        public TMP_Text[] moreLines;

        [NonSerialized] public List<string> data = new List<string>();
    }

    [Serializable]
    public class StatRow
    {
        public Image icon;
        public TMP_Text valueText;
        [Tooltip("Shown instead of the icon while the icon has no sprite (e.g. \"SPD\").")]
        public TMP_Text fallbackLabel;

        [NonSerialized] public float last = float.NaN;
        [NonSerialized] public Color baseColor = Color.white;
        [NonSerialized] public Vector3 baseScale = Vector3.one;
        [NonSerialized] public Coroutine flash;
    }

    // ---------------- Setup (filled by the builder) ----------------

    [Header("Panel")]
    [Tooltip("The whole card (a 1920 x 1080 block like the PNG). It slides in and out to the left.")]
    public RectTransform slideRoot;
    [Tooltip("Size of the whole card on screen.")]
    [Range(0.2f, 1.2f)] public float panelScale = 0.5f;
    [Tooltip("Invisible rectangle over the visible part of the card. The mouse is 'over the bar' while it is inside this.")]
    public RectTransform hitArea;
    public Button toggleButton;
    public TMP_Text toggleArrow;

    [Header("Card art (in the PNG's own pixels)")]
    [Tooltip("Where the drawn card starts on the PNG (the PNG has empty space around the card).")]
    public float cardLeft = 301f;
    [Tooltip("Where the drawn card ends on the PNG. The toggle tab sticks out from here.")]
    public float cardRight = 1591f;

    [Header("Text")]
    public string emptyText = "None";
    [Tooltip("Also write the trinket's name in front of its stats in the Trinkets line.")]
    public bool showTrinketNames = false;
    [Tooltip("Number fields on TrinketData that are NOT stats (e.g. price). Matching is not case sensitive.")]
    public string[] ignoredTrinketFields = { "price", "cost", "sellPrice", "buyPrice", "rarity", "weight", "id", "index" };

    [Header("Sections (one ruled line each)")]
    public Section moodSection = new Section();
    public Section boonsSection = new Section();
    public Section trinketsSection = new Section();
    public Section advantageSection = new Section();
    [Tooltip("Blueprint Shop structure (Solar Panel / Windmill) and its upgrades. If left empty, its info is shown on the Boons line.")]
    public Section structureSection = new Section();

    [Header("Stats (top of the card)")]
    public StatRow sunPower = new StatRow();
    public StatRow windPower = new StatRow();
    public StatRow moveSpeed = new StatRow();
    [Tooltip("Drag your Solar card asset here. Its current value is the Sun stat (leave empty to find it automatically).")]
    public SolarCard sunCard;
    [Tooltip("Drag your Wind card asset here. Its current value is the Wind stat (leave empty to find it automatically).")]
    public WindCard windCard;
    public Color statUpColor = new Color(0.1f, 0.6f, 0.2f);
    public Color statDownColor = new Color(0.8f, 0.15f, 0.15f);

    // ---------------- Behaviour ----------------

    [Header("Behaviour")]
    public bool persistAcrossScenes = true;
    public bool startOpen = false;
    [Tooltip("Scene names where the bar is hidden completely. The Trinket bar's list is used too.")]
    public string[] hideInScenes;

    [Header("Animation")]
    public float slideSpeed = 12f;

    [Header("Sounds")]
    [Range(0f, 1f)] public float sfxVolume = 1f;
    public AudioClip openSound;
    public AudioClip closeSound;

    // ---------------- Static API ----------------

    public static ActiveBuffsSidebar Instance { get; private set; }

    /// <summary>True while the mouse is over the card. Used to stop clicks leaking into the world.</summary>
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

        FillMenuIcons();
        CaptureStat(sunPower);
        CaptureStat(windPower);
        CaptureStat(moveSpeed);

        if (toggleButton != null) toggleButton.onClick.AddListener(Toggle);
        if (slideRoot != null) slideRoot.localScale = new Vector3(panelScale, panelScale, 1f);

        open = startOpen;
        SnapSlide();
        UpdateArrow();
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
        SyncStatIcon(sunPower);
        SyncStatIcon(windPower);
        SyncStatIcon(moveSpeed);
        UpdateStat(sunPower, GetSun());
        UpdateStat(windPower, GetWind());
        UpdateStat(moveSpeed, GetSpeed());
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        player = null;
        lastSignature = null; // rebuild the lines
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

    // -------------------------------------------------------------------
    // Stat icons
    // -------------------------------------------------------------------

    // Same icons the GameMenu Stats card uses (Resources/MenuUI: Sun_Icon, Wind_Icon, Speed_Icon), if they exist
    private void FillMenuIcons()
    {
        Sprite[] all = Resources.LoadAll<Sprite>("MenuUI");
        FillIcon(sunPower, all, "Sun_Icon");
        FillIcon(windPower, all, "Wind_Icon");
        FillIcon(moveSpeed, all, "Speed_Icon");
    }

    // "Sun_Icon", "Sun Icon" and "sun-icon" all count as the same name
    private static string Norm(string s)
    {
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < s.Length; i++)
            if (char.IsLetterOrDigit(s[i])) sb.Append(char.ToLowerInvariant(s[i]));
        return sb.ToString();
    }

    private static void FillIcon(StatRow row, Sprite[] all, string name)
    {
        if (row == null || row.icon == null || row.icon.sprite != null) return;
        for (int i = 0; i < all.Length; i++)
        {
            if (Norm(all[i].name) == Norm(name))
            {
                row.icon.sprite = all[i];
                return;
            }
        }
    }

    // Anything still without an icon is taken from the Trinket bar (where the stats used to be)
    private void BorrowIcons()
    {
        if (iconsBorrowed || TrinketSidebar.Instance == null) return;
        iconsBorrowed = true;

        TrinketSidebar t = TrinketSidebar.Instance;
        Borrow(sunPower, t.sunPower);
        Borrow(windPower, t.windPower);
        Borrow(moveSpeed, t.moveSpeed);
    }

    private static void Borrow(StatRow mine, TrinketSidebar.StatRow theirs)
    {
        if (mine == null || theirs == null || mine.icon == null || theirs.icon == null) return;
        if (mine.icon.sprite == null) mine.icon.sprite = theirs.icon.sprite;
    }

    // No sprite: hide the (white) icon box and show the small text label instead
    private static void SyncStatIcon(StatRow row)
    {
        if (row == null) return;
        bool has = row.icon != null && row.icon.sprite != null;
        if (row.icon != null && row.icon.enabled != has) row.icon.enabled = has;
        if (row.fallbackLabel != null && row.fallbackLabel.gameObject.activeSelf == has)
            row.fallbackLabel.gameObject.SetActive(!has);
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

    // Open: the drawn card's left edge sits on the screen's left edge
    private float OpenX
    {
        get { return slideRoot != null ? -cardLeft * slideRoot.localScale.x : 0f; }
    }

    // Closed: the whole drawn card is off screen, only the tab stays visible
    private float ClosedX
    {
        get { return slideRoot != null ? -cardRight * slideRoot.localScale.x : 0f; }
    }

    private void SnapSlide()
    {
        if (slideRoot == null) return;
        Vector2 p = slideRoot.anchoredPosition;
        p.x = open ? OpenX : ClosedX;
        slideRoot.anchoredPosition = p;
    }

    private void UpdateSlide()
    {
        if (slideRoot == null) return;
        float target = open ? OpenX : ClosedX;
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

    // Everything the card shows, as one string: the lines are only rebuilt when it changes
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
                AddRow(moodSection, Signed(power) + " Sun");
                AddRow(moodSection, Signed(power) + " Wind");
            }

            float mult = MoodManager.ScoreMultiplier;
            if (mult > 1.001f) AddRow(moodSection, "X" + mult.ToString("0.##") + " Mult");
            if (MoodManager.HandsModifier != 0) AddRow(moodSection, Signed(MoodManager.HandsModifier) + " Hand");
            if (MoodManager.ReshufflesModifier != 0) AddRow(moodSection, Signed(MoodManager.ReshufflesModifier) + " Reshuffle");

            int left = MoodManager.FightsLeft;
            if (left > 0) AddRow(moodSection, left + (left == 1 ? " fight left" : " fights left"));
        }

        // Boons
        if (BoonProgress.BonusWindPower > 0) AddRow(boonsSection, Signed(BoonProgress.BonusWindPower) + " Wind");
        if (BoonProgress.ExtraHands > 0) AddRow(boonsSection, Signed(BoonProgress.ExtraHands) + " Hand");
        if (BoonProgress.ExtraHandSize > 0) AddRow(boonsSection, Signed(BoonProgress.ExtraHandSize) + " Hand Size");

        // Blueprint Shop structure (falls back to the Boons line if there is no Blueprint line on the card)
        if (StructureProgress.IsBuilt)
        {
            StructureType st = StructureProgress.Current;
            Section ss = (structureSection != null && structureSection.text != null) ? structureSection : boonsSection;
            string cardWord = st == StructureType.Solar ? "Sun" : "Wind";

            AddRow(ss, StructureProgress.DisplayName(st));

            int sPower = StructureProgress.PowerBonus(st);
            if (sPower > 0) AddRow(ss, Signed(sPower) + " " + cardWord);

            int stab = StructureProgress.Level(StructureUpgrade.Stability);
            if (stab > 0)
            {
                int pct = Mathf.RoundToInt((1f - StructureProgress.LowPowerChanceFactor(st)) * 100f);
                AddRow(ss, "-" + pct + "% Low Power chance");
            }

            float sMult = StructureProgress.MultBonus(st);
            if (sMult > 0f) AddRow(ss, "+" + sMult.ToString("0.##") + " " + StructureProgress.CardName(st) + " Set Mult");
        }

        // Trinkets: only the stat values they give. A trinket without stats adds nothing.
        IReadOnlyList<TrinketData> owned = TrinketInventory.Owned;
        for (int i = 0; i < owned.Count; i++)
            if (owned[i] != null) AddTrinketStats(owned[i]);

        // Advantage Hit
        if (AdvantageHit.IsActive)
            AddRow(advantageSection, "-" + Mathf.RoundToInt(AdvantageHit.Reduction * 100f) + "% Score");

        RefreshTexts();
    }

    private static string Signed(float value)
    {
        return (value > 0f ? "+" : "") + value.ToString("0.##");
    }

    private void AddRow(Section s, string text)
    {
        if (s == null) return;
        s.data.Add(text);
    }

    // -------------------------------------------------------------------
    // Writing the card's lines
    // -------------------------------------------------------------------

    private static string Prefix(Section s)
    {
        return "<b>" + s.title + ":</b>  ";
    }

    private void RefreshTexts()
    {
        SetLine(moodSection);
        SetLine(boonsSection);
        SetLine(structureSection);
        SetLine(advantageSection);
        SetTrinketLines();
    }

    private void SetLine(Section s)
    {
        if (s == null || s.text == null) return;
        string body = s.data.Count == 0 ? emptyText : string.Join(", ", s.data.ToArray());
        s.text.text = Prefix(s) + body;
    }

    // Trinkets can be many: they are packed onto the first line and then the extra ruled lines
    private void SetTrinketLines()
    {
        Section s = trinketsSection;
        if (s == null || s.text == null) return;

        List<TMP_Text> slots = new List<TMP_Text>();
        slots.Add(s.text);
        if (s.moreLines != null)
            for (int i = 0; i < s.moreLines.Length; i++)
                if (s.moreLines[i] != null) slots.Add(s.moreLines[i]);

        List<string> lines = new List<string>();
        if (s.data.Count == 0)
        {
            lines.Add(Prefix(s) + emptyText);
        }
        else
        {
            float maxWidth = s.text.rectTransform.rect.width;
            if (maxWidth <= 1f) maxWidth = 1000f;

            string current = Prefix(s);
            bool lineEmpty = true;
            for (int i = 0; i < s.data.Count; i++)
            {
                string item = s.data[i];
                string trial = lineEmpty ? current + item : current + ", " + item;
                if (!lineEmpty && s.text.GetPreferredValues(trial).x > maxWidth)
                {
                    lines.Add(current + ",");
                    current = item;
                }
                else
                {
                    current = trial;
                }
                lineEmpty = false;
            }
            lines.Add(current);
        }

        // more than fits: cut the last visible line short
        if (lines.Count > slots.Count)
        {
            lines.RemoveRange(slots.Count, lines.Count - slots.Count);
            lines[slots.Count - 1] = lines[slots.Count - 1].TrimEnd(',') + "...";
        }

        for (int i = 0; i < slots.Count; i++)
            slots[i].text = i < lines.Count ? lines[i] : "";
    }

    // -------------------------------------------------------------------
    // Trinket stats
    // -------------------------------------------------------------------

    // Reads every number field on the trinket (except the ones in 'ignoredTrinketFields') and lists the ones that are not 0.
    // Fields with "sun" / "wind" in the name are written as "Sun" / "Wind".
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
            string word;
            if (lower.Contains("sun")) word = "Sun";
            else if (lower.Contains("wind")) word = "Wind";
            else word = Prettify(f.Name);

            string text = Signed(v) + " " + word;
            if (showTrinketNames && !nameShown)
            {
                text = t.trinketName + ": " + text;
                nameShown = true;
            }
            AddRow(trinketsSection, text);
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
    // Stats
    // -------------------------------------------------------------------

    private void CaptureStat(StatRow row)
    {
        if (row == null || row.valueText == null) return;
        row.baseColor = row.valueText.color;
        row.baseScale = row.valueText.transform.localScale;
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
        return Over(hitArea != null ? hitArea : slideRoot) || Over(ToggleRect);
    }

    private void PlaySFX(AudioClip clip, float pitch = 1f)
    {
        if (clip == null) return;
        GameSFX.Play(clip, sfxVolume, pitch);
    }
}