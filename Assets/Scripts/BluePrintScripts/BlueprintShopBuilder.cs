#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Must live in a folder named "Editor".
public static class BlueprintShopBuilder
{
    // ------------------------------------------------------------------
    // Tools > Shop > Create Blueprint Shop UI
    // ------------------------------------------------------------------
    [MenuItem("Tools/Shop/Create Blueprint Shop UI")]
    public static void CreateUI()
    {
        if (Object.FindAnyObjectByType<BlueprintShopUI>() != null)
        {
            Debug.LogWarning("A Blueprint Shop UI already exists in this scene, so no second one was made.");
            return;
        }

        GameObject canvasGO = new GameObject("BlueprintShopCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(canvasGO, "Create Blueprint Shop UI");

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
            Undo.RegisterCreatedObjectUndo(es, "Create Blueprint Shop UI");
        }

        GameObject shopGO = new GameObject("BlueprintShop", typeof(RectTransform));
        shopGO.transform.SetParent(canvasGO.transform, false);
        RectTransform rt = (RectTransform)shopGO.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        BlueprintShopUI ui = shopGO.AddComponent<BlueprintShopUI>();
        ui.BuildUI();

        // Hook it up to any Blueprint Shop interactable that is waiting for it
        foreach (ShopInteractable si in Object.FindObjectsByType<ShopInteractable>())
            if (si.shopUI == null && si.GetComponent<BlueprintShopVisual>() != null) si.shopUI = ui;

        Selection.activeGameObject = shopGO;
        Debug.Log("Blueprint Shop UI created. It is visible in the editor so you can style it; it hides itself when the game starts. Assign icons / sounds on the BlueprintShopUI component.");
    }

    // ------------------------------------------------------------------
    // Tools > Shop > Create Blueprint Shop (World)
    // ------------------------------------------------------------------
    [MenuItem("Tools/Shop/Create Blueprint Shop (World)")]
    public static void CreateWorldShop()
    {
        GameObject shop = new GameObject("Blueprint Shop");
        Undo.RegisterCreatedObjectUndo(shop, "Create Blueprint Shop");

        // Sprite 1: the structure (empty lot -> solar panel / windmill). Move / resize THIS object to place it.
        GameObject structure = new GameObject("Structure", typeof(SpriteRenderer));
        structure.transform.SetParent(shop.transform, false);

        // Sprite 2: the shopkeeper
        GameObject keeper = new GameObject("Blueprint Shopkeeper", typeof(SpriteRenderer));
        keeper.transform.SetParent(shop.transform, false);
        keeper.transform.localPosition = new Vector3(1.5f, 0f, 0f);

        BlueprintShopVisual visual = shop.AddComponent<BlueprintShopVisual>();
        visual.target = structure.GetComponent<SpriteRenderer>();
        visual.shopkeeperRenderer = keeper.GetComponent<SpriteRenderer>();

        ShopInteractable interact = shop.AddComponent<ShopInteractable>();
        interact.shopkeeper = keeper.transform;
        interact.shopUI = Object.FindAnyObjectByType<BlueprintShopUI>();

        Selection.activeGameObject = shop;
        Debug.Log("Created 'Blueprint Shop'. It has a Structure sprite and a Shopkeeper sprite. Assign the Empty / Solar / Windmill sprites on Blueprint Shop Visual, the shopkeeper sprite on the shopkeeper object, and (if it says so) run Tools > Shop > Create Blueprint Shop UI.");
    }
}
#endif