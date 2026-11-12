#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

// Must live in a folder named "Editor".
public static class ShopBuilder
{
    // ------------------------------------------------------------------
    // Tools > Shop > Create Shop UI
    // ------------------------------------------------------------------
    [MenuItem("Tools/Shop/Create Shop UI")]
    public static void CreateShopUI()
    {
        // Canvas
        GameObject canvasGO = new GameObject("ShopCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(canvasGO, "Create Shop UI");

        Canvas canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;

        CanvasScaler scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        if (Object.FindAnyObjectByType<EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            Undo.RegisterCreatedObjectUndo(es, "Create Shop UI");
        }

        // Script holder (always active) + the part that is shown/hidden
        GameObject shopGO = NewUI("TrinketShop", canvasGO.transform);
        Stretch(shopGO);
        TrinketShopUI ui = shopGO.AddComponent<TrinketShopUI>();

        GameObject root = NewUI("ShopRoot", shopGO.transform);
        Stretch(root);

        GameObject dim = NewUI("Dim", root.transform);
        Stretch(dim);
        AddImage(dim, new Color(0f, 0f, 0f, 0.6f));

        GameObject panel = NewUI("Panel", root.transform);
        SetRect(panel, new Vector2(0f, 70f), new Vector2(1100f, 650f));
        AddImage(panel, new Color(0.30f, 0.22f, 0.16f, 1f));

        AddText(panel.transform, "Title", "Trinket Shop", 44, new Color(1f, 0.93f, 0.7f), FontStyles.Bold,
            TextAlignmentOptions.Center, new Vector2(0f, 280f), new Vector2(600f, 70f));

        // 3 slots
        TrinketShopUI.Slot[] slots = new TrinketShopUI.Slot[3];
        float[] xs = { -340f, 0f, 340f };
        Color ink = new Color(0.25f, 0.16f, 0.08f);

        for (int i = 0; i < 3; i++)
        {
            GameObject slotGO = NewUI("Slot" + i, panel.transform);
            SetRect(slotGO, new Vector2(xs[i], 20f), new Vector2(300f, 440f));
            Image bg = AddImage(slotGO, new Color(0.96f, 0.90f, 0.75f));
            Button btn = slotGO.AddComponent<Button>();
            btn.targetGraphic = bg;

            GameObject iconGO = NewUI("Icon", slotGO.transform);
            SetRect(iconGO, new Vector2(0f, 110f), new Vector2(150f, 150f));
            Image icon = AddImage(iconGO, Color.white);
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.enabled = false;

            TextMeshProUGUI nameText = AddText(slotGO.transform, "Name", "Trinket", 32, ink, FontStyles.Bold,
                TextAlignmentOptions.Center, new Vector2(0f, -10f), new Vector2(280f, 50f));
            TextMeshProUGUI descText = AddText(slotGO.transform, "Description", "Description", 22, ink, FontStyles.Normal,
                TextAlignmentOptions.Top, new Vector2(0f, -100f), new Vector2(260f, 140f));
            TextMeshProUGUI priceText = AddText(slotGO.transform, "Price", "Cost: 0", 28, new Color(0.15f, 0.4f, 0.15f), FontStyles.Bold,
                TextAlignmentOptions.Center, new Vector2(0f, -195f), new Vector2(260f, 40f));

            GameObject taken = NewUI("TakenOverlay", slotGO.transform);
            Stretch(taken);
            Image takenImg = AddImage(taken, new Color(0f, 0f, 0f, 0.65f));
            takenImg.raycastTarget = false;
            AddText(taken.transform, "Label", "TAKEN", 44, Color.white, FontStyles.Bold,
                TextAlignmentOptions.Center, Vector2.zero, new Vector2(280f, 60f));
            taken.SetActive(false);

            slots[i] = new TrinketShopUI.Slot
            {
                button = btn,
                icon = icon,
                nameText = nameText,
                descriptionText = descText,
                priceText = priceText,
                takenOverlay = taken
            };
        }

        // Reshuffle + close
        Button reshuffle = NewButton(panel.transform, "ReshuffleButton", "RESHUFFLE", new Color(0.25f, 0.6f, 0.3f),
            new Vector2(0f, -285f), new Vector2(320f, 70f), 34);
        Button close = NewButton(panel.transform, "CloseButton", "X", new Color(0.7f, 0.2f, 0.2f),
            new Vector2(500f, 285f), new Vector2(60f, 60f), 34);

        // Shopkeeper dialogue box (just a name + text, no character sprites)
        GameObject box = NewUI("DialogueBox", root.transform);
        SetRect(box, new Vector2(0f, -400f), new Vector2(1100f, 170f));
        AddImage(box, new Color(0.10f, 0.07f, 0.05f, 0.95f));

        TextMeshProUGUI speakerName = AddText(box.transform, "SpeakerName", "Shopkeeper", 34, new Color(1f, 0.85f, 0.4f),
            FontStyles.Bold, TextAlignmentOptions.TopLeft, new Vector2(0f, 55f), new Vector2(1040f, 45f));
        TextMeshProUGUI dialogueText = AddText(box.transform, "DialogueText", "", 30, Color.white,
            FontStyles.Normal, TextAlignmentOptions.TopLeft, new Vector2(0f, -20f), new Vector2(1040f, 100f));

        ShopkeeperDialogue talk = shopGO.AddComponent<ShopkeeperDialogue>();
        talk.dialogueBox = box;
        talk.nameText = speakerName;
        talk.dialogueText = dialogueText;

        ui.shopkeeper = talk;
        ui.panelRoot = root;
        ui.slots = slots;
        ui.reshuffleButton = reshuffle;
        ui.closeButton = close;

        if (Object.FindAnyObjectByType<TrinketSidebar>() == null) CreateSidebar();

        Selection.activeObject = shopGO;
        Debug.Log("Shop UI created. It is visible in the editor so you can style it; it hides itself when the game starts.");
    }

    // ------------------------------------------------------------------
    // Tools > Shop > Create Shop World Objects
    // ------------------------------------------------------------------
    [MenuItem("Tools/Shop/Create Shop World Objects")]
    public static void CreateWorldObjects()
    {
        Vector3 pos = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.pivot : Vector3.zero;

        GameObject keeper = new GameObject("Shopkeeper", typeof(SpriteRenderer));
        keeper.transform.position = pos + Vector3.left * 2.5f;
        Undo.RegisterCreatedObjectUndo(keeper, "Create Shop World Objects");

        GameObject car = new GameObject("Shop Car", typeof(SpriteRenderer), typeof(BoxCollider), typeof(ShopInteractable));
        car.transform.position = pos;
        Undo.RegisterCreatedObjectUndo(car, "Create Shop World Objects");

        BoxCollider col = car.GetComponent<BoxCollider>();
        col.size = new Vector3(4f, 2.5f, 1f);

        ShopInteractable interact = car.GetComponent<ShopInteractable>();
        interact.shopUI = Object.FindAnyObjectByType<TrinketShopUI>();
        interact.shopkeeper = keeper.transform; // the "Press E" prompt floats above the shopkeeper

        Selection.activeObject = car;
        Debug.Log("Created 'Shopkeeper' and 'Shop Car'. Assign your sprites in their Sprite Renderers, then resize the Box Collider on the car to fit it.");
    }

    // ------------------------------------------------------------------
    // Tools > Shop > Create Trinket Sidebar
    // ------------------------------------------------------------------
    [MenuItem("Tools/Shop/Create Trinket Sidebar")]
    public static void CreateSidebar()
    {
        TrinketSidebar existing = Object.FindAnyObjectByType<TrinketSidebar>();
        if (existing != null)
        {
            Selection.activeObject = existing.gameObject;
            Debug.LogWarning("A Trinket Sidebar already exists in this scene, so no second one was made.");
            return;
        }

        GameObject canvasGO = new GameObject("TrinketSidebarCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(canvasGO, "Create Trinket Sidebar");

        Canvas canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 60; // above the shop (50) so you can still sell while it is open

        CanvasScaler scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        if (Object.FindAnyObjectByType<EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            Undo.RegisterCreatedObjectUndo(es, "Create Trinket Sidebar");
        }

        TrinketSidebar bar = canvasGO.AddComponent<TrinketSidebar>();

        Color panelColor = new Color(0.30f, 0.30f, 0.30f, 1f);
        Color frameColor = new Color(0.58f, 0.58f, 0.58f, 1f);
        Color ink = new Color(0.25f, 0.16f, 0.08f);

        // The sliding part: anchored to the right edge, middle of the screen
        GameObject slide = NewUI("SlideRoot", canvasGO.transform);
        RectTransform sr = slide.GetComponent<RectTransform>();
        sr.anchorMin = sr.anchorMax = new Vector2(1f, 0.5f);
        sr.pivot = new Vector2(1f, 0.5f);
        sr.sizeDelta = new Vector2(190f, 960f);
        sr.anchoredPosition = Vector2.zero;
        sr.localScale = new Vector3(0.75f, 0.75f, 1f); // matches 'Panel Scale' on the TrinketSidebar
        AddImage(slide, panelColor);

        // 5 empty frames (top slot = first effect to trigger)
        RectTransform[] frames = new RectTransform[TrinketInventory.MaxTrinkets];
        for (int i = 0; i < frames.Length; i++)
        {
            GameObject f = NewUI("Frame" + i, slide.transform);
            SetRect(f, new Vector2(0f, 400f - 134f * i), new Vector2(160f, 120f));
            Image fi = AddImage(f, frameColor);
            fi.raycastTarget = false;
            frames[i] = f.GetComponent<RectTransform>();
        }

        // Item template (cloned for every trinket you own)
        GameObject tpl = NewUI("ItemTemplate", slide.transform);
        SetRect(tpl, Vector2.zero, new Vector2(160f, 120f));
        Image tplBg = AddImage(tpl, new Color(0.96f, 0.90f, 0.75f));
        Outline outline = tpl.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 0.85f, 0.2f, 1f);
        outline.effectDistance = new Vector2(4f, -4f);
        outline.enabled = false;

        GameObject tplIconGO = NewUI("Icon", tpl.transform);
        SetRect(tplIconGO, new Vector2(0f, 20f), new Vector2(80f, 80f));
        Image tplIcon = AddImage(tplIconGO, Color.white);
        tplIcon.preserveAspect = true;
        tplIcon.raycastTarget = false;
        tplIcon.enabled = false;

        TextMeshProUGUI tplLabel = AddText(tpl.transform, "Name", "Trinket", 20, ink, FontStyles.Bold,
            TextAlignmentOptions.Center, Vector2.zero, new Vector2(150f, 110f));
        tplLabel.enableAutoSizing = true;
        tplLabel.fontSizeMin = 12f;
        tplLabel.fontSizeMax = 24f;

        TrinketItemView tplView = tpl.AddComponent<TrinketItemView>();
        tplView.background = tplBg;
        tplView.icon = tplIcon;
        tplView.label = tplLabel;
        tplView.selectedOutline = outline;
        tpl.SetActive(false);

        // Stats at the bottom: Sun Power, Wind Power, Movement Speed (assign your icon sprites to the Icon images)
        TrinketSidebar.StatRow sun = NewStatRow(slide.transform, "SunPower", new Vector2(0f, -270f), new Color(1f, 0.75f, 0.1f));
        TrinketSidebar.StatRow wind = NewStatRow(slide.transform, "WindPower", new Vector2(0f, -340f), new Color(0.3f, 0.85f, 0.95f));
        TrinketSidebar.StatRow speed = NewStatRow(slide.transform, "MoveSpeed", new Vector2(0f, -410f), new Color(0.55f, 0.33f, 0.15f));

        // Open / close tab on the left edge of the bar
        GameObject tab = NewUI("ToggleTab", slide.transform);
        RectTransform tr = tab.GetComponent<RectTransform>();
        tr.anchorMin = tr.anchorMax = new Vector2(0f, 0.5f);
        tr.pivot = new Vector2(1f, 0.5f);
        tr.sizeDelta = new Vector2(46f, 120f);
        tr.anchoredPosition = Vector2.zero;
        Image tabImg = AddImage(tab, panelColor);
        Button tabBtn = tab.AddComponent<Button>();
        tabBtn.targetGraphic = tabImg;
        TextMeshProUGUI arrow = AddText(tab.transform, "Arrow", ">", 44, new Color(0.8f, 0.8f, 0.8f), FontStyles.Bold,
            TextAlignmentOptions.Center, Vector2.zero, new Vector2(46f, 120f));

        // Info card (shown on hover / selection, sits left of the tab)
        GameObject card = NewUI("InfoCard", slide.transform);
        SetRect(card, new Vector2(-330f, 0f), new Vector2(300f, 240f));
        AddImage(card, new Color(0.10f, 0.07f, 0.05f, 0.95f));
        TextMeshProUGUI infoName = AddText(card.transform, "Name", "Trinket", 28, new Color(1f, 0.85f, 0.4f), FontStyles.Bold,
            TextAlignmentOptions.Center, new Vector2(0f, 90f), new Vector2(280f, 40f));
        TextMeshProUGUI infoDesc = AddText(card.transform, "Description", "Description", 20, Color.white, FontStyles.Normal,
            TextAlignmentOptions.Top, new Vector2(0f, 10f), new Vector2(270f, 120f));
        TextMeshProUGUI infoHint = AddText(card.transform, "Hint", "Sell trinkets at the shop", 18, new Color(0.7f, 0.7f, 0.7f),
            FontStyles.Italic, TextAlignmentOptions.Center, new Vector2(0f, -90f), new Vector2(270f, 30f));
        Button sell = NewButton(card.transform, "SellButton", "SELL", new Color(0.7f, 0.2f, 0.2f),
            new Vector2(0f, -90f), new Vector2(180f, 50f), 28);
        card.SetActive(false);

        bar.slideRoot = sr;
        bar.frames = frames;
        bar.itemTemplate = tplView;
        bar.toggleButton = tabBtn;
        bar.toggleArrow = arrow;
        bar.sunPower = sun;
        bar.windPower = wind;
        bar.moveSpeed = speed;
        bar.infoCard = card.GetComponent<RectTransform>();
        bar.infoName = infoName;
        bar.infoDescription = infoDesc;
        bar.infoHint = infoHint;
        bar.sellButton = sell;

        Selection.activeObject = canvasGO;
        Debug.Log("Trinket Sidebar created. It is shown open in the editor so you can style it; in play mode it starts closed, opens with the shop, and follows you into the card scene. Assign the stat icons, slot sprites and sounds on the TrinketSidebar component.");
    }

    private static TrinketSidebar.StatRow NewStatRow(Transform parent, string name, Vector2 pos, Color iconColor)
    {
        GameObject iconGO = NewUI(name + "Icon", parent);
        SetRect(iconGO, pos + new Vector2(-50f, 0f), new Vector2(48f, 48f));
        Image icon = AddImage(iconGO, iconColor);
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        TextMeshProUGUI value = AddText(parent, name + "Value", "0", 34, Color.white, FontStyles.Bold,
            TextAlignmentOptions.Left, pos + new Vector2(35f, 0f), new Vector2(90f, 50f));

        return new TrinketSidebar.StatRow { icon = icon, valueText = value };
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

    private static void SetRect(GameObject go, Vector2 anchoredPos, Vector2 size)
    {
        RectTransform r = go.GetComponent<RectTransform>();
        r.anchorMin = new Vector2(0.5f, 0.5f);
        r.anchorMax = new Vector2(0.5f, 0.5f);
        r.pivot = new Vector2(0.5f, 0.5f);
        r.sizeDelta = size;
        r.anchoredPosition = anchoredPos;
    }

    private static Image AddImage(GameObject go, Color color)
    {
        Image img = go.AddComponent<Image>();
        img.color = color;
        return img;
    }

    private static TextMeshProUGUI AddText(Transform parent, string name, string text, float size, Color color,
        FontStyles style, TextAlignmentOptions align, Vector2 pos, Vector2 rectSize)
    {
        GameObject go = NewUI(name, parent);
        SetRect(go, pos, rectSize);

        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.fontStyle = style;
        t.alignment = align;
        t.raycastTarget = false;
        return t;
    }

    private static Button NewButton(Transform parent, string name, string label, Color color, Vector2 pos, Vector2 size, float fontSize)
    {
        GameObject go = NewUI(name, parent);
        SetRect(go, pos, size);
        Image img = AddImage(go, color);
        Button btn = go.AddComponent<Button>();
        btn.targetGraphic = img;

        AddText(go.transform, "Label", label, fontSize, Color.white, FontStyles.Bold,
            TextAlignmentOptions.Center, Vector2.zero, size);
        return btn;
    }
}
#endif