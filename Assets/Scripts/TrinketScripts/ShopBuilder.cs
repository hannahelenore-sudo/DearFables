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
                TextAlignmentOptions.Top, new Vector2(0f, -115f), new Vector2(260f, 170f));

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