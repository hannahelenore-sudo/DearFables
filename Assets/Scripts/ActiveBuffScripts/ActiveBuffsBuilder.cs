#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Must live in a folder named "Editor".
// Builds the Active Buffs bar (left side of the screen) as the hand-drawn ruled card (b_gg.png),
// laid out like the Stats page of the GameMenu. Everything is placed in the PNG's own pixel coordinates.
//
// BEFORE YOU RUN IT: put b_gg.png in your project (Assets/Resources/MenuUI/ is a good place, next to the menu art)
// and set its Texture Type to "Sprite (2D and UI)". Delete the old Active Buffs bar from the scene first.
public static class ActiveBuffsBuilder
{
    private const string CardSpriteName = "b_gg";

    // The PNG is 1920 x 1080, but the drawn card only covers this part of it
    private const float PngW = 1920f, PngH = 1080f;
    private const float CardLeft = 301f, CardRight = 1591f, CardTop = 48f, CardBottom = 992f;

    // On-screen size of the card (1 = the PNG at full size)
    private const float DefaultScale = 0.5f;

    private static readonly Color Navy = new Color(47f / 255f, 51f / 255f, 105f / 255f, 1f);
    private static readonly Color BoxFill = new Color(0.95f, 0.99f, 1f, 1f);
    private static readonly Color CardBlue = new Color(0.74f, 0.83f, 0.93f, 1f); // only used if the PNG is missing

    // The ruled lines of the card: text sits in the gap between two lines (centre of each gap, y down from the PNG's top)
    private const float RowMood = 384f;       // gap 355-419   (the portrait is on the right: keep these two narrow)
    private const float RowBoons = 449f;      // gap 419-486
    private const float RowBlueprint = 517f;  // gap 486-553
    private const float RowTrinkets1 = 587f;  // gap 553-626
    private const float RowTrinkets2 = 655f;  // gap 626-691
    private const float RowTrinkets3 = 717f;  // gap 691-750
    private const float RowAdvantage = 779f;  // gap 750-815 (the little star is bottom right: keep it narrow)

    private const float TextX = 382f;
    private const float RowSize = 44f;

    private static TMP_FontAsset font;

    [MenuItem("Tools/HUD/Create Active Buffs Bar")]
    public static void Create()
    {
        ActiveBuffsSidebar existing = Object.FindAnyObjectByType<ActiveBuffsSidebar>();
        if (existing != null)
        {
            Selection.activeObject = existing.gameObject;
            Debug.LogWarning("There is already an Active Buffs bar in this scene. Delete it first, then run this again.");
            return;
        }

        // ---- Copy what we can from the Trinket bar ----
        TrinketSidebar trinket = Object.FindAnyObjectByType<TrinketSidebar>();
        int order = 45;
        font = FindAsset<TMP_FontAsset>("MenuFont"); // the menu's own font, so the two match

        if (trinket != null)
        {
            Canvas tc = trinket.GetComponent<Canvas>();
            if (tc != null) order = tc.sortingOrder;
            if (font == null && trinket.infoName != null) font = trinket.infoName.font;
        }

        Sprite cardSprite = FindSprite(CardSpriteName);
        if (cardSprite == null)
            Debug.LogWarning("Active Buffs: no sprite called \"" + CardSpriteName + "\" found. Import b_gg.png (Texture Type: Sprite (2D and UI)) and run this again. A plain blue card is used for now.");

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
        bar.panelScale = DefaultScale;
        bar.cardLeft = CardLeft;
        bar.cardRight = CardRight;

        // ---- The card: a PNG-sized block, vertically centred on the left edge. It slides left to hide. ----
        GameObject root = NewUI("SlideRoot", canvasGO.transform);
        RectTransform rr = root.GetComponent<RectTransform>();
        rr.anchorMin = rr.anchorMax = new Vector2(0f, 0.5f);
        rr.pivot = new Vector2(0f, 0.5f);
        rr.sizeDelta = new Vector2(PngW, PngH);
        rr.localScale = new Vector3(DefaultScale, DefaultScale, 1f);
        // the drawn card is a little lower than the PNG's middle: nudge so it is centred on screen
        float centreShift = (PngH * 0.5f - (CardTop + CardBottom) * 0.5f) * DefaultScale;
        rr.anchoredPosition = new Vector2(-CardRight * DefaultScale, centreShift); // starts closed
        bar.slideRoot = rr;

        GameObject cardGO = NewUI("Card", root.transform);
        Stretch(cardGO);
        Image cardImg = cardGO.AddComponent<Image>();
        cardImg.raycastTarget = false;
        if (cardSprite != null) { cardImg.sprite = cardSprite; cardImg.color = Color.white; }
        else cardImg.color = CardBlue;

        // invisible rectangle over the drawn card: blocks clicks to the world and tells the script where the mouse is
        GameObject hit = NewUI("HitArea", root.transform);
        RectTransform hr = hit.GetComponent<RectTransform>();
        Place(hr, new Vector2(CardLeft, CardTop), new Vector2(CardRight - CardLeft, CardBottom - CardTop), new Vector2(0f, 1f));
        Image hitImg = hit.AddComponent<Image>();
        hitImg.color = new Color(0f, 0f, 0f, 0f);
        hitImg.raycastTarget = true;
        bar.hitArea = hr;

        // ---- Sun / Wind / Speed across the top (left of the portrait) ----
        const float statY = 205f;
        bar.sunPower = MakeStat(root.transform, "SunPower", "SUN", new Vector2(640f, statY), new Vector2(690f, statY), new Vector2(120f, 90f));
        bar.windPower = MakeStat(root.transform, "WindPower", "WIND", new Vector2(860f, statY), new Vector2(910f, statY), new Vector2(120f, 90f));
        bar.moveSpeed = MakeStat(root.transform, "MoveSpeed", "SPD", new Vector2(1080f, statY), new Vector2(1130f, statY), new Vector2(105f, 90f));

        // ---- The written rows, each on its own ruled line ----
        bar.moodSection = MakeLine(root.transform, "Moods", "Moods", RowMood, 850f);
        bar.boonsSection = MakeLine(root.transform, "Boons", "Boons", RowBoons, 850f);
        bar.structureSection = MakeLine(root.transform, "Blueprints", "Blueprints", RowBlueprint, 1190f);

        // Trinkets can be many: three ruled lines, the text continues on the next one when a line is full
        bar.trinketsSection = MakeLine(root.transform, "Trinkets", "Trinkets", RowTrinkets1, 1190f);
        bar.trinketsSection.moreLines = new TMP_Text[]
        {
            MakeRowText(root.transform, "Trinkets2", "", RowTrinkets2, 1190f, false),
            MakeRowText(root.transform, "Trinkets3", "", RowTrinkets3, 1190f, false)
        };
        bar.trinketsSection.text.enableAutoSizing = false; // these lines wrap by themselves instead of shrinking

        bar.advantageSection = MakeLine(root.transform, "AdvantageHit", "Advantage hit", RowAdvantage, 980f);

        // ---- Toggle tab (sticks out of the card's right edge so it stays visible when the card is hidden) ----
        GameObject tab = NewUI("Tab", root.transform);
        RectTransform tr = tab.GetComponent<RectTransform>();
        Place(tr, new Vector2(CardRight - 6f, 520f), new Vector2(96f, 156f), new Vector2(0f, 0.5f));
        Image tabImg = tab.AddComponent<Image>();
        tabImg.color = BoxFill;
        Outline o = tab.AddComponent<Outline>();
        o.effectColor = Navy;
        o.effectDistance = new Vector2(5f, -5f);
        o.useGraphicAlpha = false;
        Button button = tab.AddComponent<Button>();
        button.targetGraphic = tabImg;
        ColorBlock cb = button.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(0.80f, 0.92f, 1f, 1f);
        cb.pressedColor = new Color(0.62f, 0.82f, 1f, 1f);
        cb.selectedColor = Color.white;
        cb.fadeDuration = 0.05f;
        button.colors = cb;
        bar.toggleButton = button;
        TextMeshProUGUI arrow = AddText(tab.transform, "Arrow", ">", 90f, Navy, FontStyles.Bold, TextAlignmentOptions.Center);
        Stretch(arrow.gameObject);
        bar.toggleArrow = arrow;

        // ---- Icons: the menu's own Sun / Wind / Speed icons if they exist, otherwise the Trinket bar's ----
        FillIcon(bar.sunPower, "Sun_Icon", trinket != null ? trinket.sunPower : null);
        FillIcon(bar.windPower, "Wind_Icon", trinket != null ? trinket.windPower : null);
        FillIcon(bar.moveSpeed, "Speed_Icon", trinket != null ? trinket.moveSpeed : null);

        // ---- Power cards (their current value is the Sun / Wind stat) ----
        bar.sunCard = FindAsset<SolarCard>(null);
        bar.windCard = FindAsset<WindCard>(null);

        Selection.activeObject = canvasGO;
        Debug.Log("Active Buffs card created (left side). " +
                  (cardSprite != null ? "" : "b_gg sprite NOT FOUND: assign it on SlideRoot > Card > Image. ") +
                  "Sun card: " + (bar.sunCard != null ? bar.sunCard.name : "NOT FOUND (drag it into 'Sun Card')") + ", Wind card: " +
                  (bar.windCard != null ? bar.windCard.name : "NOT FOUND (drag it into 'Wind Card')") + ". " +
                  "Size: change 'Panel Scale' on the bar. Text positions are the children of SlideRoot, in the PNG's pixels.");
    }

    // ------------------------------------------------------------------
    // Pieces
    // ------------------------------------------------------------------

    // One row of text on a ruled line. The title is written in bold in front of it by the script.
    private static ActiveBuffsSidebar.Section MakeLine(Transform parent, string name, string title, float y, float width)
    {
        ActiveBuffsSidebar.Section s = new ActiveBuffsSidebar.Section();
        s.title = title;
        s.text = MakeRowText(parent, name, "<b>" + title + ":</b>  None", y, width, true);
        return s;
    }

    private static TMP_Text MakeRowText(Transform parent, string name, string text, float y, float width, bool fit)
    {
        TextMeshProUGUI t = AddText(parent, name, text, RowSize, Navy, FontStyles.Normal, TextAlignmentOptions.Left);
        t.richText = true;
        Place(t.rectTransform, new Vector2(TextX, y), new Vector2(width, 64f), new Vector2(0f, 0.5f));
#pragma warning disable 618
        t.enableWordWrapping = false;
#pragma warning restore 618
        t.overflowMode = TextOverflowModes.Ellipsis;
        if (fit)
        {
            // a long line shrinks (down to half size) instead of running off the card
            t.enableAutoSizing = true;
            t.fontSizeMax = RowSize;
            t.fontSizeMin = RowSize * 0.5f;
        }
        return t;
    }

    // icon + number (+ a small text label that is only shown while the icon has no sprite)
    private static ActiveBuffsSidebar.StatRow MakeStat(Transform parent, string name, string fallback, Vector2 iconCentre, Vector2 textPos, Vector2 textBox)
    {
        GameObject icon = NewUI(name + "Icon", parent);
        Place(icon.GetComponent<RectTransform>(), iconCentre, new Vector2(78f, 78f), new Vector2(0.5f, 0.5f));
        Image img = icon.AddComponent<Image>();
        img.color = Color.white;
        img.preserveAspect = true;
        img.raycastTarget = false;

        TextMeshProUGUI label = AddText(parent, name + "Label", fallback, 30f, Navy, FontStyles.Bold | FontStyles.Italic, TextAlignmentOptions.Center);
        Place(label.rectTransform, iconCentre, new Vector2(90f, 60f), new Vector2(0.5f, 0.5f));
        label.gameObject.SetActive(false);

        TextMeshProUGUI value = AddText(parent, name + "Value", "0", 62f, Navy, FontStyles.Bold, TextAlignmentOptions.Left);
        Place(value.rectTransform, textPos, textBox, new Vector2(0f, 0.5f));
        value.enableAutoSizing = true; // long numbers shrink instead of wrapping
        value.fontSizeMax = 62f;
        value.fontSizeMin = 24f;
#pragma warning disable 618
        value.enableWordWrapping = false;
#pragma warning restore 618
        value.overflowMode = TextOverflowModes.Overflow;

        ActiveBuffsSidebar.StatRow stat = new ActiveBuffsSidebar.StatRow();
        stat.icon = img;
        stat.valueText = value;
        stat.fallbackLabel = label;
        return stat;
    }

    private static void FillIcon(ActiveBuffsSidebar.StatRow mine, string menuSpriteName, TrinketSidebar.StatRow theirs)
    {
        Sprite s = FindSprite(menuSpriteName);
        if (s != null) { mine.icon.sprite = s; return; }
        if (theirs != null && theirs.icon != null && theirs.icon.sprite != null)
        {
            mine.icon.sprite = theirs.icon.sprite;
            mine.icon.color = theirs.icon.color;
        }
    }

    // ------------------------------------------------------------------
    // Finding assets
    // ------------------------------------------------------------------

    // "Sun_Icon", "Sun Icon" and "sun-icon" all count as the same name
    private static string Norm(string s)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        for (int i = 0; i < s.Length; i++)
            if (char.IsLetterOrDigit(s[i])) sb.Append(char.ToLowerInvariant(s[i]));
        return sb.ToString();
    }

    // Finds a sprite by its file name (spaces / underscores / case ignored). Also works when the PNG's Sprite Mode is
    // "Multiple" (sprites are then called b_gg_0 ...): the first sprite of that file is used.
    private static Sprite FindSprite(string name)
    {
        string want = Norm(name);
        string[] guids = AssetDatabase.FindAssets("t:Sprite");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (Norm(System.IO.Path.GetFileNameWithoutExtension(path)) != want) continue;

            Sprite first = null;
            Object[] all = AssetDatabase.LoadAllAssetsAtPath(path);
            for (int k = 0; k < all.Length; k++)
            {
                Sprite sp = all[k] as Sprite;
                if (sp == null) continue;
                if (Norm(sp.name) == want) return sp;
                if (first == null) first = sp;
            }
            if (first != null) return first;
        }
        return null;
    }

    // by type (and, when a name is given, by name)
    private static T FindAsset<T>(string name) where T : Object
    {
        string filter = "t:" + typeof(T).Name + (string.IsNullOrEmpty(name) ? "" : " " + name);
        string[] guids = AssetDatabase.FindAssets(filter);
        if (guids.Length == 0) return null;
        return AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[0]));
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    // Position in the PNG's pixels: from the top-left corner, y going DOWN. 'pivot' says which point of the object sits there.
    private static void Place(RectTransform r, Vector2 pos, Vector2 size, Vector2 pivot)
    {
        r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
        r.pivot = pivot;
        r.sizeDelta = size;
        r.anchoredPosition = new Vector2(pos.x, -pos.y);
    }

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