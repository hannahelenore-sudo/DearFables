#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Must live in a folder named "Editor".
// Builds the compact Active Buffs bar (left side of the screen): one icon button per section, with a hover tooltip.
// It copies the scale, canvas settings, font and stat icons from your Trinket bar when there is one in the scene,
// so the two match. Then just swap the section icon sprites / font.
public static class ActiveBuffsBuilder
{
    // Compact sizes (reference pixels, before the panel scale)
    private const float PanelWidth = 124f;
    private const float PanelHeight = 700f;
    private const float SectionSize = 92f;
    private const float StatsHeight = 180f;

    private static TMP_FontAsset font;

    [MenuItem("Tools/HUD/Create Active Buffs Bar")]
    public static void Create()
    {
        ActiveBuffsSidebar existing = Object.FindAnyObjectByType<ActiveBuffsSidebar>();
        if (existing != null)
        {
            Selection.activeObject = existing.gameObject;
            Debug.LogWarning("There is already an Active Buffs bar in this scene.");
            return;
        }

        // ---- Copy what we can from the Trinket bar ----
        TrinketSidebar trinket = Object.FindAnyObjectByType<TrinketSidebar>();
        float scale = 0.75f;
        int order = 45;
        font = null;

        if (trinket != null)
        {
            scale = trinket.panelScale;
            Canvas tc = trinket.GetComponent<Canvas>();
            if (tc != null) order = tc.sortingOrder;
            if (trinket.infoName != null) font = trinket.infoName.font;
        }

        Sprite panelSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        // ---- Canvas ----
        GameObject canvasGO = new GameObject("ActiveBuffsCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(canvasGO, "Create Active Buffs Bar");

        Canvas canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = order;

        CanvasScaler scaler = canvasGO.GetComponent<CanvasScaler>();
        CanvasScaler ts = trinket != null ? trinket.GetComponent<CanvasScaler>() : null;
        if (ts != null)
        {
            scaler.uiScaleMode = ts.uiScaleMode;
            scaler.referenceResolution = ts.referenceResolution;
            scaler.screenMatchMode = ts.screenMatchMode;
            scaler.matchWidthOrHeight = ts.matchWidthOrHeight;
        }
        else
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        ActiveBuffsSidebar bar = canvasGO.AddComponent<ActiveBuffsSidebar>();
        bar.panelScale = scale;

        // ---- Panel (left edge, vertically centred; slides left to hide) ----
        GameObject root = NewUI("SlideRoot", canvasGO.transform);
        RectTransform rr = root.GetComponent<RectTransform>();
        rr.anchorMin = rr.anchorMax = new Vector2(0f, 0.5f);
        rr.pivot = new Vector2(0f, 0.5f);
        rr.sizeDelta = new Vector2(PanelWidth, PanelHeight);
        rr.anchoredPosition = new Vector2(0f, -50f * scale); // 5 squares: keep the top where it was
        Image bg = AddImage(root, new Color(0.33f, 0.33f, 0.33f), panelSprite, true);
        bg.raycastTarget = true; // clicks on the bar must not throw cards / start walking
        bar.slideRoot = rr;

        // ---- Section icons ----
        GameObject content = NewUI("Sections", root.transform);
        RectTransform cr = content.GetComponent<RectTransform>();
        cr.anchorMin = Vector2.zero;
        cr.anchorMax = Vector2.one;
        cr.offsetMin = new Vector2(8f, StatsHeight + 8f);
        cr.offsetMax = new Vector2(-8f, -8f);
        VerticalLayoutGroup v = content.AddComponent<VerticalLayoutGroup>();
        v.spacing = 8f;
        v.childAlignment = TextAnchor.UpperCenter;
        v.childControlWidth = true;
        v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;
        bar.layoutRoot = cr;

        bar.moodSection = MakeSection(content.transform, "Mood", "Mood", panelSprite);
        bar.boonsSection = MakeSection(content.transform, "Boons", "Boons", panelSprite);
        bar.trinketsSection = MakeSection(content.transform, "Trinkets", "Trinkets", panelSprite);
        bar.advantageSection = MakeSection(content.transform, "AdvantageHit", "Advantage Hit", panelSprite);
        bar.structureSection = MakeSection(content.transform, "Blueprint", "Blueprint", panelSprite);

        // ---- Row template (cloned for every tooltip line) ----
        bar.rowTemplate = MakeRowTemplate(root.transform);

        // ---- Stats ----
        GameObject stats = NewUI("Stats", root.transform);
        RectTransform sr = stats.GetComponent<RectTransform>();
        sr.anchorMin = new Vector2(0f, 0f);
        sr.anchorMax = new Vector2(1f, 0f);
        sr.pivot = new Vector2(0.5f, 0f);
        sr.sizeDelta = new Vector2(0f, StatsHeight);
        sr.anchoredPosition = new Vector2(0f, 6f);
        VerticalLayoutGroup sv = stats.AddComponent<VerticalLayoutGroup>();
        sv.spacing = 4f;
        sv.padding = new RectOffset(6, 6, 4, 4);
        sv.childAlignment = TextAnchor.MiddleCenter;
        sv.childControlWidth = true;
        sv.childControlHeight = true;
        sv.childForceExpandWidth = true;
        sv.childForceExpandHeight = true;

        bar.sunPower = MakeStatRow(stats.transform, "SunPower");
        bar.windPower = MakeStatRow(stats.transform, "WindPower");
        bar.moveSpeed = MakeStatRow(stats.transform, "MoveSpeed");

        // ---- Tab (sticks out of the right edge so it stays visible when the bar is closed) ----
        GameObject tab = NewUI("Tab", root.transform);
        RectTransform tr = tab.GetComponent<RectTransform>();
        tr.anchorMin = tr.anchorMax = new Vector2(1f, 0.5f);
        tr.pivot = new Vector2(0f, 0.5f);
        tr.sizeDelta = new Vector2(56f, 104f);
        tr.anchoredPosition = Vector2.zero;
        Image tabImg = AddImage(tab, new Color(0.33f, 0.33f, 0.33f), panelSprite, true);
        Button button = tab.AddComponent<Button>();
        button.targetGraphic = tabImg;
        bar.toggleButton = button;
        TextMeshProUGUI arrow = AddText(tab.transform, "Arrow", "<", 50f, new Color(0.65f, 0.65f, 0.65f), FontStyles.Bold, TextAlignmentOptions.Center);
        Stretch(arrow.gameObject);
        bar.toggleArrow = arrow;

        // ---- Hover tooltip (a child of the canvas so it is not scaled / moved with the bar) ----
        MakeTooltip(canvasGO.transform, bar, panelSprite);

        // ---- Icons, from the Trinket bar's old stat row ----
        if (trinket != null)
        {
            Copy(bar.sunPower, trinket.sunPower);
            Copy(bar.windPower, trinket.windPower);
            Copy(bar.moveSpeed, trinket.moveSpeed);
            if (trinket.sunPower.icon != null) bar.sunIcon = trinket.sunPower.icon.sprite;
            if (trinket.windPower.icon != null) bar.windIcon = trinket.windPower.icon.sprite;
        }

        // ---- Power cards (their current value is the Sun / Wind stat) ----
        bar.sunCard = FindAsset<SolarCard>();
        bar.windCard = FindAsset<WindCard>();

        Selection.activeObject = canvasGO;
        Debug.Log("Active Buffs bar created (left side, compact). " +
                  (trinket != null ? "Scale and stat icons were copied from your Trinket bar. " : "No Trinket bar found in this scene, so default scale / no stat icons: set the Sun / Wind / Speed icons yourself. ") +
                  "Set each section's icon: select Sections > (Mood / Boons / Trinkets / AdvantageHit / Blueprint) > Icon and assign a sprite. " +
                  "Sun card: " + (bar.sunCard != null ? bar.sunCard.name : "NOT FOUND (drag it into 'Sun Card')") + ", Wind card: " +
                  (bar.windCard != null ? bar.windCard.name : "NOT FOUND (drag it into 'Wind Card')") + ".");
    }

    // ------------------------------------------------------------------
    // Pieces
    // ------------------------------------------------------------------

    // One square icon button. The Icon image gets its sprite from you; until then a letter is shown.
    private static ActiveBuffsSidebar.Section MakeSection(Transform parent, string name, string title, Sprite sprite)
    {
        GameObject box = NewUI(name, parent);
        AddImage(box, new Color(0.65f, 0.65f, 0.65f), sprite, true).raycastTarget = false;

        LayoutElement le = box.AddComponent<LayoutElement>();
        le.preferredHeight = SectionSize;
        le.minHeight = SectionSize;

        GameObject iconGO = NewUI("Icon", box.transform);
        Stretch(iconGO);
        RectTransform ir = iconGO.GetComponent<RectTransform>();
        ir.offsetMin = new Vector2(10f, 10f);
        ir.offsetMax = new Vector2(-10f, -10f);
        Image icon = iconGO.AddComponent<Image>();
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        icon.enabled = false; // no sprite yet

        TextMeshProUGUI letter = AddText(box.transform, "Letter", title.Substring(0, 1), 52f, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
        Stretch(letter.gameObject);

        ActiveBuffsSidebar.Section section = new ActiveBuffsSidebar.Section();
        section.title = title;
        section.box = box.GetComponent<RectTransform>();
        section.icon = icon;
        section.fallbackLabel = letter;
        return section;
    }

    private static void MakeTooltip(Transform canvasRoot, ActiveBuffsSidebar bar, Sprite sprite)
    {
        GameObject tip = NewUI("Tooltip", canvasRoot);
        RectTransform tr = tip.GetComponent<RectTransform>();
        tr.anchorMin = tr.anchorMax = new Vector2(0.5f, 0.5f);
        tr.pivot = new Vector2(0f, 0.5f);

        Image bg = AddImage(tip, new Color(0.2f, 0.2f, 0.2f, 0.95f), sprite, true);
        bg.raycastTarget = false; // must never block the icons underneath

        VerticalLayoutGroup v = tip.AddComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(14, 14, 10, 10);
        v.spacing = 4f;
        v.childAlignment = TextAnchor.UpperLeft;
        v.childControlWidth = true;
        v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;

        ContentSizeFitter fit = tip.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        tip.AddComponent<LayoutElement>().minWidth = 160f;

        TextMeshProUGUI title = AddText(tip.transform, "Title", "Mood", 34f, Color.white, FontStyles.Bold, TextAlignmentOptions.Left);
        title.gameObject.AddComponent<LayoutElement>().preferredHeight = 42f;

        bar.tooltip = tr;
        bar.tooltipTitle = title;
        tip.SetActive(false);
    }

    private static RectTransform MakeRowTemplate(Transform parent)
    {
        GameObject row = NewUI("RowTemplate", parent);
        HorizontalLayoutGroup h = row.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 8f;
        h.childAlignment = TextAnchor.MiddleLeft;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = false;
        row.AddComponent<LayoutElement>().preferredHeight = 42f;

        GameObject icon = NewUI("Icon", row.transform);
        Image img = icon.AddComponent<Image>();
        img.preserveAspect = true;
        img.raycastTarget = false;
        LayoutElement ile = icon.AddComponent<LayoutElement>();
        ile.preferredWidth = 38f;
        ile.preferredHeight = 38f;

        TextMeshProUGUI label = AddText(row.transform, "Label", "+50", 32f, Color.white, FontStyles.Bold, TextAlignmentOptions.Left);
        label.gameObject.AddComponent<LayoutElement>().preferredHeight = 40f;

        row.SetActive(false);
        return row.GetComponent<RectTransform>();
    }

    private static ActiveBuffsSidebar.StatRow MakeStatRow(Transform parent, string name)
    {
        GameObject row = NewUI(name, parent);
        HorizontalLayoutGroup h = row.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 8f;
        h.childAlignment = TextAnchor.MiddleCenter;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = false;

        GameObject icon = NewUI("Icon", row.transform);
        Image img = icon.AddComponent<Image>();
        img.color = Color.white;
        img.preserveAspect = true;
        img.raycastTarget = false;
        LayoutElement ile = icon.AddComponent<LayoutElement>();
        ile.preferredWidth = 46f;
        ile.preferredHeight = 46f;

        TextMeshProUGUI value = AddText(row.transform, "Value", "0", 36f, Color.white, FontStyles.Bold, TextAlignmentOptions.Left);
        LayoutElement vle = value.gameObject.AddComponent<LayoutElement>();
        vle.minWidth = 0f;
        vle.preferredWidth = 0f;
        vle.flexibleWidth = 1f; // takes the space next to the icon; long numbers shrink to fit (set at runtime)
        vle.preferredHeight = 46f;

        ActiveBuffsSidebar.StatRow stat = new ActiveBuffsSidebar.StatRow();
        stat.icon = img;
        stat.valueText = value;
        return stat;
    }

    private static void Copy(ActiveBuffsSidebar.StatRow mine, TrinketSidebar.StatRow theirs)
    {
        if (mine == null || theirs == null || mine.icon == null || theirs.icon == null) return;
        mine.icon.sprite = theirs.icon.sprite;
        mine.icon.color = theirs.icon.color;
    }

    private static T FindAsset<T>() where T : Object
    {
        string[] guids = AssetDatabase.FindAssets("t:" + typeof(T).Name);
        if (guids.Length == 0) return null;
        return AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[0]));
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static GameObject NewUI(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static void Stretch(GameObject go)
    {
        RectTransform r = go.GetComponent<RectTransform>();
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;
    }

    private static Image AddImage(GameObject go, Color color, Sprite sprite, bool sliced)
    {
        Image img = go.AddComponent<Image>();
        img.color = color;
        if (sprite != null)
        {
            img.sprite = sprite;
            img.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
        }
        return img;
    }

    private static TextMeshProUGUI AddText(Transform parent, string name, string text, float size, Color color,
        FontStyles style, TextAlignmentOptions align)
    {
        GameObject go = NewUI(name, parent);
        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.fontStyle = style;
        t.alignment = align;
        t.raycastTarget = false;
        return t;
    }
}
#endif