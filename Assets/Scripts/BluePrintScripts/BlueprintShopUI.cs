using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// The Blueprint Shop screen. Two states:
///  - Nothing built: pick a Solar Panel or a Windmill (costs batteries). You can only have one.
///  - Built: three upgrades (Power / Stability / Mult) and a Destroy button (costs batteries, resets the upgrades).
/// Open it with Open() - ShopInteractable does this. Needs an EventSystem in the scene.
/// If you leave the UI fields empty it builds its own UI at runtime; use Tools > Shop > Create Blueprint Shop UI
/// to build it in the editor so you can restyle it.
/// </summary>
public class BlueprintShopUI : ShopPanelBase
{
    [Serializable]
    public class OptionView
    {
        public Button button;
        public Image icon;
        public TMP_Text title;
        public TMP_Text description;
        public TMP_Text info;
    }

    [Header("UI (built automatically if empty)")]
    public GameObject panelRoot;
    public TMP_Text titleText;
    [Tooltip("Shown while nothing is built: the Solar / Windmill choice.")]
    public GameObject chooseView;
    [Tooltip("Shown once something is built: the three upgrades.")]
    public GameObject upgradeView;
    public OptionView solarOption;
    public OptionView windOption;
    [Tooltip("3 buttons in this order: Power, Stability, Mult.")]
    public OptionView[] upgradeOptions;
    public Button destroyButton;
    public Button closeButton;

    [Header("Shopkeeper")]
    public ShopkeeperDialogue shopkeeper;

    [Header("Icons (optional)")]
    public Sprite solarIcon;
    public Sprite windIcon;
    public Sprite powerIcon;
    public Sprite stabilityIcon;
    public Sprite multIcon;

    [Header("Animation")]
    public float popDuration = 0.2f;
    public float optionPopDuration = 0.25f;
    public float optionStagger = 0.08f;

    [Header("Sounds")]
    [Range(0f, 1f)] public float sfxVolume = 1f;
    public AudioClip openSound;
    public AudioClip buildSound;
    public AudioClip upgradeSound;
    public AudioClip destroySound;
    public AudioClip closeSound;
    public AudioClip cantAffordSound;

    [Header("Shopkeeper Lines (one is picked at random)")]
    [TextArea(1, 3)]
    public string[] greetingLines =
    {
        "Welcome to the blueprint shop! What shall we build?",
        "Solar or wind? Pick one and I'll get to work.",
        "Got big plans? I've got the blueprints!"
    };
    [TextArea(1, 3)]
    public string[] greetingBuiltLines =
    {
        "Your {item} is running nicely. Want to improve it?",
        "Back for upgrades? I like your style.",
        "That {item} could use a little more oomph, don't you think?"
    };
    [TextArea(1, 3)]
    [Tooltip("{item} is replaced with Solar Panel / Windmill.")]
    public string[] builtLines =
    {
        "One {item}, coming right up! No take-backs... unless you pay.",
        "Hammer time! Your {item} is built.",
        "A fine {item}. Built to last!"
    };
    [TextArea(1, 3)]
    [Tooltip("{item} is replaced with the upgrade's name.")]
    public string[] upgradedLines =
    {
        "Done! {item} installed.",
        "Ooh, that's better. {item} done!",
        "Tighten a bolt here, swap a part there... {item}!"
    };
    [TextArea(1, 3)]
    public string[] maxedLines =
    {
        "That one's already as good as it gets!",
        "Can't improve that any further, friend."
    };
    [TextArea(1, 3)]
    public string[] brokeLines =
    {
        "Hmm, you're a few batteries short, friend.",
        "Come back when you've got more batteries!",
        "Blueprints aren't free, you know!"
    };
    [TextArea(1, 3)]
    public string[] destroyWarnLines =
    {
        "Tearing it down costs batteries and you lose every upgrade. Sure? Click again.",
        "Are you certain? All the upgrades go too. Click again to confirm."
    };
    [TextArea(1, 3)]
    public string[] destroyedLines =
    {
        "And... gone. Clean slate! Pick something new.",
        "Demolished! What will it be this time?"
    };

    private CanvasGroup panelGroup;
    private TMP_Text destroyLabel;
    private bool destroyArmed;

    // -------------------------------------------------------------------
    // Setup
    // -------------------------------------------------------------------

    private void Awake()
    {
        IsAnyShopOpen = false;

        if (panelRoot == null) BuildUI();

        panelGroup = panelRoot.GetComponent<CanvasGroup>();
        if (panelGroup == null) panelGroup = panelRoot.AddComponent<CanvasGroup>();
        panelRoot.SetActive(false);

        if (solarOption != null && solarOption.button != null)
            solarOption.button.onClick.AddListener(() => BuildStructure(StructureType.Solar));
        if (windOption != null && windOption.button != null)
            windOption.button.onClick.AddListener(() => BuildStructure(StructureType.Wind));

        if (upgradeOptions != null)
        {
            for (int i = 0; i < upgradeOptions.Length; i++)
            {
                int index = i;
                if (upgradeOptions[i] != null && upgradeOptions[i].button != null)
                    upgradeOptions[i].button.onClick.AddListener(() => BuyUpgrade((StructureUpgrade)index));
            }
        }

        if (destroyButton != null)
        {
            destroyButton.onClick.AddListener(DestroyStructure);
            destroyLabel = destroyButton.GetComponentInChildren<TMP_Text>(true);
        }
        if (closeButton != null) closeButton.onClick.AddListener(Close);
    }

    private void OnDisable()
    {
        if (IsOpen) IsAnyShopOpen = false;
    }

    private void Update()
    {
        if (IsOpen && Input.GetKeyDown(KeyCode.Escape)) Close();
    }

    // -------------------------------------------------------------------
    // Open / close
    // -------------------------------------------------------------------

    public override void Open()
    {
        if (IsOpen) return;

        if (panelRoot == null)
        {
            Debug.LogError("BlueprintShopUI: no panel. Use Tools > Shop > Create Blueprint Shop UI.", this);
            return;
        }

        IsOpen = true;
        IsAnyShopOpen = true;
        destroyArmed = false;

        PlaySFX(openSound);
        panelRoot.SetActive(true);
        StartCoroutine(PopIn());
        Refresh(true);

        if (shopkeeper != null)
        {
            if (StructureProgress.IsBuilt)
                shopkeeper.SayRandom(greetingBuiltLines, StructureProgress.DisplayName(StructureProgress.Current));
            else
                shopkeeper.SayRandom(greetingLines, null);
        }
    }

    public override void Close()
    {
        if (!IsOpen) return;

        IsOpen = false;
        IsAnyShopOpen = false;
        destroyArmed = false;

        PlaySFX(closeSound);
        if (shopkeeper != null) shopkeeper.Clear();
        StopAllCoroutines();
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    private IEnumerator PopIn()
    {
        Vector3 baseScale = panelRoot.transform.localScale;
        float elapsed = 0f;

        while (elapsed < popDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / popDuration);
            float eased = 1f - (1f - t) * (1f - t);

            panelGroup.alpha = eased;
            panelRoot.transform.localScale = baseScale * Mathf.Lerp(0.9f, 1f, eased);
            yield return null;
        }

        panelGroup.alpha = 1f;
        panelRoot.transform.localScale = baseScale;
    }

    // -------------------------------------------------------------------
    // Buying
    // -------------------------------------------------------------------

    private void BuildStructure(StructureType type)
    {
        if (!IsOpen || StructureProgress.IsBuilt) return;

        if (!BatteryManager.TrySpend(StructureProgress.BuildCost))
        {
            CantAfford();
            return;
        }

        StructureProgress.Build(type);
        PlaySFX(buildSound);
        if (shopkeeper != null) shopkeeper.SayRandom(builtLines, StructureProgress.DisplayName(type));
        Refresh(true);
    }

    private void BuyUpgrade(StructureUpgrade upgrade)
    {
        if (!IsOpen || !StructureProgress.IsBuilt) return;

        if (StructureProgress.IsMaxed(upgrade))
        {
            PlaySFX(cantAffordSound);
            if (shopkeeper != null) shopkeeper.SayRandom(maxedLines, null);
            return;
        }

        if (!BatteryManager.TrySpend(StructureProgress.NextCost(upgrade)))
        {
            CantAfford();
            return;
        }

        StructureProgress.Upgrade(upgrade);
        PlaySFX(upgradeSound);
        if (shopkeeper != null) shopkeeper.SayRandom(upgradedLines, StructureProgress.UpgradeName(upgrade));
        Refresh(false);

        int idx = (int)upgrade;
        if (upgradeOptions != null && idx < upgradeOptions.Length && upgradeOptions[idx].button != null)
            StartCoroutine(PopOption(upgradeOptions[idx].button.transform, 0f));
    }

    private void DestroyStructure()
    {
        if (!IsOpen || !StructureProgress.IsBuilt) return;

        // First click only arms it, so it can't be done by accident
        if (!destroyArmed)
        {
            destroyArmed = true;
            RefreshDestroyButton();
            if (shopkeeper != null) shopkeeper.SayRandom(destroyWarnLines, null);
            return;
        }

        if (!BatteryManager.CanAfford(StructureProgress.DestroyCost))
        {
            destroyArmed = false;
            RefreshDestroyButton();
            CantAfford();
            return;
        }

        BatteryManager.TrySpend(StructureProgress.DestroyCost);
        StructureProgress.Destroy();
        destroyArmed = false;

        PlaySFX(destroySound);
        if (shopkeeper != null) shopkeeper.SayRandom(destroyedLines, null);
        Refresh(true);
    }

    private void CantAfford()
    {
        PlaySFX(cantAffordSound);
        BatteryHUD.FlashCantAfford();
        if (shopkeeper != null) shopkeeper.SayRandom(brokeLines, null);
    }

    // -------------------------------------------------------------------
    // Refreshing the screen
    // -------------------------------------------------------------------

    private void Refresh(bool animate)
    {
        bool built = StructureProgress.IsBuilt;

        if (chooseView != null) chooseView.SetActive(!built);
        if (upgradeView != null) upgradeView.SetActive(built);
        if (destroyButton != null) destroyButton.gameObject.SetActive(built);

        if (!built)
        {
            if (titleText != null) titleText.text = "Blueprint Shop";
            SetupChoice(solarOption, StructureType.Solar, solarIcon);
            SetupChoice(windOption, StructureType.Wind, windIcon);
        }
        else
        {
            StructureType type = StructureProgress.Current;
            if (titleText != null) titleText.text = StructureProgress.DisplayName(type);

            for (int i = 0; i < 3 && upgradeOptions != null && i < upgradeOptions.Length; i++)
                SetupUpgrade(upgradeOptions[i], (StructureUpgrade)i, type);

            RefreshDestroyButton();
        }

        if (animate)
        {
            OptionView[] views = built ? upgradeOptions : new[] { solarOption, windOption };
            if (views != null)
            {
                for (int i = 0; i < views.Length; i++)
                    if (views[i] != null && views[i].button != null)
                        StartCoroutine(PopOption(views[i].button.transform, i * optionStagger));
            }
        }
    }

    private void SetupChoice(OptionView v, StructureType type, Sprite icon)
    {
        if (v == null) return;

        string card = StructureProgress.CardName(type);
        if (v.title != null) v.title.text = StructureProgress.DisplayName(type);
        if (v.description != null)
            v.description.text = "Powers up your " + card + " cards.\nUpgrade their Power, stability and set bonus Mult.\nYou can't switch unless you pay to destroy it.";
        if (v.info != null) v.info.text = "Build: " + StructureProgress.BuildCost;
        SetIcon(v, icon);
        if (v.button != null) v.button.interactable = true;
    }

    private void SetupUpgrade(OptionView v, StructureUpgrade upgrade, StructureType type)
    {
        if (v == null) return;

        string card = StructureProgress.CardName(type);
        int lvl = StructureProgress.Level(upgrade);
        int max = StructureProgress.MaxLevel;

        string what;
        Sprite icon;
        switch (upgrade)
        {
            case StructureUpgrade.Power:
                what = "+" + StructureProgress.PowerPerLevel + " Power on every " + card + " card.\nNow: +" + (lvl * StructureProgress.PowerPerLevel);
                icon = powerIcon;
                break;
            case StructureUpgrade.Stability:
                what = "-" + Mathf.RoundToInt(StructureProgress.LowPowerReductionPerLevel * 100f) + "% chance of " + card + " cards being drawn Low Power.\nNow: -" + Mathf.RoundToInt(StructureProgress.LowPowerReductionPerLevel * 100f * lvl) + "%";
                icon = stabilityIcon;
                break;
            default:
                what = "+" + StructureProgress.MultPerLevel.ToString("0.##") + " Mult on the " + card + " set bonus.\nNow: +" + (lvl * StructureProgress.MultPerLevel).ToString("0.##");
                icon = multIcon;
                break;
        }

        if (v.title != null) v.title.text = StructureProgress.UpgradeName(upgrade) + "  (" + lvl + "/" + max + ")";
        if (v.description != null) v.description.text = what;
        if (v.info != null) v.info.text = StructureProgress.IsMaxed(upgrade) ? "MAX" : "Cost: " + StructureProgress.NextCost(upgrade);
        SetIcon(v, icon);
        if (v.button != null) v.button.interactable = !StructureProgress.IsMaxed(upgrade);
    }

    private void RefreshDestroyButton()
    {
        if (destroyLabel == null) return;
        destroyLabel.text = destroyArmed
            ? "Sure? Pay " + StructureProgress.DestroyCost
            : "Destroy (" + StructureProgress.DestroyCost + ")";
    }

    private static void SetIcon(OptionView v, Sprite icon)
    {
        if (v.icon == null) return;
        v.icon.sprite = icon;
        v.icon.enabled = icon != null;
    }

    private IEnumerator PopOption(Transform target, float delay)
    {
        target.localScale = Vector3.zero;
        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);

        float elapsed = 0f;
        while (elapsed < optionPopDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / optionPopDuration);

            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float eased = 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);

            target.localScale = Vector3.one * eased;
            yield return null;
        }
        target.localScale = Vector3.one;
    }

    private void PlaySFX(AudioClip clip, float pitch = 1f)
    {
        if (clip == null) return;
        GameSFX.Play(clip, sfxVolume, pitch);
    }

    // -------------------------------------------------------------------
    // UI builder (runs automatically if no panel is assigned, or from the editor menu)
    // -------------------------------------------------------------------

    public void BuildUI()
    {
        Color wood = new Color(0.30f, 0.22f, 0.16f, 1f);
        Color paper = new Color(0.95f, 0.88f, 0.70f, 1f);
        Color ink = new Color(0.25f, 0.16f, 0.08f);

        panelRoot = NewUI("BlueprintRoot", transform).gameObject;
        Stretch((RectTransform)panelRoot.transform);

        RectTransform dim = NewUI("Dim", panelRoot.transform);
        Stretch(dim);
        AddImage(dim, new Color(0f, 0f, 0f, 0.6f));

        RectTransform panel = NewUI("Panel", panelRoot.transform);
        Place(panel, new Vector2(0f, 70f), new Vector2(1100f, 650f));
        AddImage(panel, wood);

        titleText = AddText(panel, "Title", "Blueprint Shop", 44, new Color(1f, 0.93f, 0.7f), FontStyles.Bold,
            new Vector2(0f, 280f), new Vector2(700f, 70f), false);

        // Choose view
        RectTransform choose = NewUI("ChooseView", panel);
        Stretch(choose);
        chooseView = choose.gameObject;
        solarOption = BuildOption(choose, "SolarOption", new Vector2(-260f, 40f), new Vector2(440f, 400f), paper, ink);
        windOption = BuildOption(choose, "WindOption", new Vector2(260f, 40f), new Vector2(440f, 400f), paper, ink);

        // Upgrade view
        RectTransform upgrades = NewUI("UpgradeView", panel);
        Stretch(upgrades);
        upgradeView = upgrades.gameObject;
        upgradeOptions = new OptionView[3];
        float[] xs = { -350f, 0f, 350f };
        for (int i = 0; i < 3; i++)
            upgradeOptions[i] = BuildOption(upgrades, "Upgrade" + i, new Vector2(xs[i], 40f), new Vector2(320f, 340f), paper, ink);

        // Destroy button
        RectTransform destroyRt = NewUI("DestroyButton", panel);
        Place(destroyRt, new Vector2(-380f, -170f), new Vector2(280f, 46f));
        Image destroyImg = AddImage(destroyRt, new Color(0.65f, 0.25f, 0.2f));
        destroyButton = destroyRt.gameObject.AddComponent<Button>();
        destroyButton.targetGraphic = destroyImg;
        AddText(destroyRt, "Label", "Destroy", 24, Color.white, FontStyles.Bold, Vector2.zero, new Vector2(270f, 44f), false);

        // Close button
        RectTransform closeRt = NewUI("CloseButton", panel);
        Place(closeRt, new Vector2(500f, 285f), new Vector2(60f, 60f));
        Image closeImg = AddImage(closeRt, new Color(0.65f, 0.25f, 0.2f));
        closeButton = closeRt.gameObject.AddComponent<Button>();
        closeButton.targetGraphic = closeImg;
        AddText(closeRt, "Label", "X", 32, Color.white, FontStyles.Bold, Vector2.zero, new Vector2(56f, 56f), false);

        // Shopkeeper dialogue strip
        RectTransform box = NewUI("DialogueBox", panel);
        Place(box, new Vector2(0f, -250f), new Vector2(1000f, 100f));
        AddImage(box, new Color(0.15f, 0.10f, 0.07f, 0.95f));
        TMP_Text nameText = AddText(box, "Name", "Blueprint Guy", 24, new Color(1f, 0.85f, 0.4f), FontStyles.Bold,
            new Vector2(0f, 32f), new Vector2(960f, 32f), true);
        TMP_Text dialogueText = AddText(box, "Text", "", 26, Color.white, FontStyles.Normal,
            new Vector2(0f, -12f), new Vector2(960f, 56f), true);

        GameObject dlgGO = new GameObject("ShopkeeperDialogue", typeof(RectTransform));
        dlgGO.transform.SetParent(panel, false);
        shopkeeper = dlgGO.AddComponent<ShopkeeperDialogue>();
        shopkeeper.shopkeeperName = "Blueprint Guy";
        shopkeeper.dialogueBox = box.gameObject;
        shopkeeper.nameText = nameText;
        shopkeeper.dialogueText = dialogueText;
        shopkeeper.Clear();
    }

    private static OptionView BuildOption(Transform parent, string name, Vector2 pos, Vector2 size, Color bg, Color ink)
    {
        RectTransform rt = NewUI(name, parent);
        Place(rt, pos, size);
        Image img = AddImage(rt, bg);
        Button b = rt.gameObject.AddComponent<Button>();
        b.targetGraphic = img;

        float top = size.y * 0.5f;
        OptionView v = new OptionView { button = b };

        RectTransform ic = NewUI("Icon", rt);
        Place(ic, new Vector2(0f, top - 70f), new Vector2(110f, 110f));
        v.icon = ic.gameObject.AddComponent<Image>();
        v.icon.preserveAspect = true;
        v.icon.raycastTarget = false;
        v.icon.enabled = false;

        v.title = AddText(rt, "Title", "", 30, ink, FontStyles.Bold, new Vector2(0f, top - 150f), new Vector2(size.x - 30f, 44f), true);
        v.description = AddText(rt, "Description", "", 22, ink, FontStyles.Normal, new Vector2(0f, -60f), new Vector2(size.x - 40f, size.y - 240f), true);
        v.info = AddText(rt, "Info", "", 28, ink, FontStyles.Bold, new Vector2(0f, -top + 35f), new Vector2(size.x - 30f, 40f), false);
        return v;
    }

    private static RectTransform NewUI(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;
    }

    private static void Place(RectTransform r, Vector2 pos, Vector2 size)
    {
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = pos;
        r.sizeDelta = size;
    }

    private static Image AddImage(RectTransform r, Color c)
    {
        Image i = r.gameObject.AddComponent<Image>();
        i.color = c;
        return i;
    }

    private static TMP_Text AddText(Transform parent, string name, string text, float size, Color color,
        FontStyles style, Vector2 pos, Vector2 sz, bool autoSize)
    {
        RectTransform rt = NewUI(name, parent);
        Place(rt, pos, sz);
        TextMeshProUGUI t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.fontStyle = style;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false;
        if (autoSize)
        {
            t.enableAutoSizing = true;
            t.fontSizeMin = 12f;
            t.fontSizeMax = size;
        }
        return t;
    }
}
