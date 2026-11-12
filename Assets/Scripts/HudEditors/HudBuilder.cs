#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Must live in a folder named "Editor".
// Builds the mood circle, the battery counter, the health cards, the mood assets and the battery pickup,
// so you can "set it down" and just change the sprites / assets.
public static class HudBuilder
{
    private const string MoodFolder = "Assets/Resources/Moods";
    private const string PrefabFolder = "Assets/Prefabs";
    private const string PickupPath = "Assets/Prefabs/BatteryPickup.prefab";

    // ------------------------------------------------------------------
    // Tools > HUD > Create Player HUD
    // ------------------------------------------------------------------
    [MenuItem("Tools/HUD/Create Player HUD")]
    public static void CreatePlayerHUD()
    {
        CreateMoodAssets(false); // so the moods can be edited right away

        Sprite knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

        // Canvas
        GameObject canvasGO = new GameObject("PlayerHUDCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        Undo.RegisterCreatedObjectUndo(canvasGO, "Create Player HUD");
        Canvas canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40;
        CanvasScaler scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        // Everything sits in one block in the top-left corner. Move / scale "PlayerHUD" to place it.
        GameObject hud = NewUI("PlayerHUD", canvasGO.transform);
        RectTransform hr = hud.GetComponent<RectTransform>();
        hr.anchorMin = hr.anchorMax = hr.pivot = new Vector2(0f, 1f);
        hr.sizeDelta = new Vector2(520f, 300f);
        hr.anchoredPosition = new Vector2(30f, -30f);

        // ---------------- Mood circle ----------------
        GameObject mood = NewUI("MoodCircle", hud.transform);
        PlaceTL(mood, new Vector2(0f, 0f), new Vector2(190f, 190f));
        Image ring = AddImage(mood, Color.black);
        ring.sprite = knob;
        ring.raycastTarget = false;

        GameObject maskGO = NewUI("PortraitMask", mood.transform);
        SetCenter(maskGO, Vector2.zero, new Vector2(170f, 170f));
        Image maskImg = AddImage(maskGO, new Color(0.55f, 0.75f, 0.9f));
        maskImg.sprite = knob;
        maskImg.raycastTarget = false;
        Mask mask = maskGO.AddComponent<Mask>();
        mask.showMaskGraphic = true;

        GameObject face = NewUI("Portrait", maskGO.transform);
        Stretch(face);
        Image faceImg = AddImage(face, new Color(0.55f, 0.75f, 0.9f));
        faceImg.raycastTarget = false;
        faceImg.preserveAspect = true;

        TextMeshProUGUI moodText = AddText(mood.transform, "MoodText", "Neutral", 36, Color.white, FontStyles.Bold,
            TextAlignmentOptions.Center, new Vector2(0f, -72f), new Vector2(230f, 46f));

        GameObject badge = NewUI("FightsBadge", mood.transform);
        SetCenter(badge, new Vector2(72f, 72f), new Vector2(54f, 54f));
        Image badgeImg = AddImage(badge, new Color(0.15f, 0.1f, 0.1f, 0.95f));
        badgeImg.sprite = knob;
        badgeImg.raycastTarget = false;
        TextMeshProUGUI badgeText = AddText(badge.transform, "Fights", "3", 30, Color.white, FontStyles.Bold,
            TextAlignmentOptions.Center, Vector2.zero, new Vector2(54f, 54f));
        badge.SetActive(false);

        TextMeshProUGUI popup = AddText(mood.transform, "MoodPopup", "", 34, new Color(1f, 0.9f, 0.4f), FontStyles.Bold,
            TextAlignmentOptions.Center, new Vector2(0f, 125f), new Vector2(320f, 50f));
        popup.gameObject.SetActive(false);

        MoodHUD moodHud = mood.AddComponent<MoodHUD>();
        moodHud.portrait = faceImg;
        moodHud.moodText = moodText;
        moodHud.circle = mood.GetComponent<RectTransform>();
        moodHud.fightsBadge = badge;
        moodHud.fightsText = badgeText;
        moodHud.popupText = popup;

        // ---------------- Health cards ----------------
        GameObject health = NewUI("HealthCards", hud.transform);
        PlaceTL(health, new Vector2(215f, 0f), new Vector2(330f, 100f));

        Image[] cards = new Image[PlayerHealth.DefaultMax];
        for (int i = 0; i < cards.Length; i++)
        {
            GameObject card = NewUI("HealthCard" + i, health.transform);
            PlaceTL(card, new Vector2(i * 85f, 0f), new Vector2(70f, 98f));
            Image img = AddImage(card, new Color(0.96f, 0.92f, 0.82f));
            img.raycastTarget = false;
            AddText(card.transform, "Mark", "A", 40, new Color(0.2f, 0.15f, 0.12f), FontStyles.Bold,
                TextAlignmentOptions.Center, Vector2.zero, new Vector2(70f, 98f));
            cards[i] = img;
        }

        HealthCardsHUD healthHud = health.AddComponent<HealthCardsHUD>();
        healthHud.cards = cards;

        // ---------------- Batteries ----------------
        GameObject battery = NewUI("Batteries", hud.transform);
        PlaceTL(battery, new Vector2(150f, 185f), new Vector2(250f, 80f));

        GameObject iconGO = NewUI("BatteryIcon", battery.transform);
        PlaceTL(iconGO, new Vector2(0f, 0f), new Vector2(64f, 64f));
        Image iconImg = AddImage(iconGO, new Color(1f, 0.85f, 0.2f));
        iconImg.sprite = knob;
        iconImg.raycastTarget = false;
        iconImg.preserveAspect = true;

        TextMeshProUGUI count = AddText(battery.transform, "BatteryCount", BatteryManager.StartingBatteries.ToString(), 56,
            Color.white, FontStyles.Bold, TextAlignmentOptions.Left, Vector2.zero, new Vector2(170f, 70f));
        RectTransform cr = count.rectTransform;
        cr.anchorMin = cr.anchorMax = cr.pivot = new Vector2(0f, 1f);
        cr.anchoredPosition = new Vector2(80f, 0f);

        BatteryHUD batteryHud = battery.AddComponent<BatteryHUD>();
        batteryHud.countText = count;
        batteryHud.icon = iconGO.GetComponent<RectTransform>();

        // ---------------- Stars + stamina ----------------
        BuildStarCounter(hud.transform);
        BuildStaminaBar(hud.transform);

        Selection.activeObject = hud;
        Debug.Log("Player HUD created (top-left). Swap the sprites on: MoodCircle/PortraitMask/Portrait (default face), HealthCard0-3, StaminaBar/Back + Fill, StarIcon, BatteryIcon. " +
                  "Each mood's own face is set on its asset in Assets/Resources/Moods.");
    }

    // ------------------------------------------------------------------
    // Tools > HUD > Create Mood Assets
    // ------------------------------------------------------------------
    [MenuItem("Tools/HUD/Create Mood Assets")]
    public static void CreateMoodAssetsMenu()
    {
        CreateMoodAssets(true);
    }

    private static void CreateMoodAssets(bool select)
    {
        EnsureFolder("Assets", "Resources");
        EnsureFolder("Assets/Resources", "Moods");

        string[] keys = { "Neutral", "SugarRush", "Angry" };
        Object last = null;
        int created = 0;

        foreach (string key in keys)
        {
            string path = MoodFolder + "/" + key + ".asset";
            MoodData existing = AssetDatabase.LoadAssetAtPath<MoodData>(path);
            if (existing != null) { last = existing; continue; }

            MoodData data = MoodData.Build(key);
            AssetDatabase.CreateAsset(data, path);
            last = data;
            created++;
        }

        AssetDatabase.SaveAssets();
        if (select && last != null)
        {
            EditorGUIUtility.PingObject(last);
            Selection.activeObject = last;
        }
        if (created > 0) Debug.Log("Created " + created + " mood asset(s) in " + MoodFolder + ". Click one to change its numbers or give it a portrait.");
    }

    // ------------------------------------------------------------------
    // Tools > Battery > Create Battery Pickup Prefab
    // ------------------------------------------------------------------
    [MenuItem("Tools/Battery/Create Battery Pickup Prefab")]
    public static void CreatePickupPrefabMenu()
    {
        GameObject prefab = EnsurePickupPrefab();
        EditorGUIUtility.PingObject(prefab);
        Selection.activeObject = prefab;
        Debug.Log("Battery pickup prefab: " + PickupPath + ". Put your battery sprite in its Sprite Renderer and tune the Battery Pickup settings.");
    }

    private static GameObject EnsurePickupPrefab()
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(PickupPath);
        if (existing != null) return existing;

        EnsureFolder("Assets", "Prefabs");

        Sprite knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        GameObject go = new GameObject("BatteryPickup");
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = knob;
        sr.color = new Color(1f, 0.85f, 0.2f);
        go.AddComponent<BatteryPickup>();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, PickupPath);
        Object.DestroyImmediate(go);
        return prefab;
    }

    // ------------------------------------------------------------------
    // Tools > Battery > Create Battery Drops (Game Scene)
    // ------------------------------------------------------------------
    [MenuItem("Tools/Battery/Create Battery Drops (Game Scene)")]
    public static void CreateSceneDropper()
    {
        GameObject go = new GameObject("BatteryDrops");
        Undo.RegisterCreatedObjectUndo(go, "Create Battery Drops");

        BatteryDropper dropper = go.AddComponent<BatteryDropper>();
        dropper.pickupPrefab = EnsurePickupPrefab();
        dropper.dropPendingOnStart = true;
        dropper.trackPlayerPosition = true;

        Selection.activeObject = go;
        Debug.Log("'BatteryDrops' created. Put this in the scene the player returns to after a fight: batteries you win drop where the fight started.");
    }

    // ------------------------------------------------------------------
    // Tools > Battery > Add Dropper To Selected Enemy
    // ------------------------------------------------------------------
    [MenuItem("Tools/Battery/Add Dropper To Selected Enemy")]
    public static void AddDropperToSelection()
    {
        GameObject[] selection = Selection.gameObjects;
        if (selection.Length == 0)
        {
            Debug.LogWarning("Select an enemy in the Hierarchy first.");
            return;
        }

        GameObject prefab = EnsurePickupPrefab();
        foreach (GameObject go in selection)
        {
            BatteryDropper d = go.GetComponent<BatteryDropper>();
            if (d == null) d = Undo.AddComponent<BatteryDropper>(go);
            d.pickupPrefab = prefab;
        }
        Debug.Log("Added a Battery Dropper. Call GetComponent<BatteryDropper>().Drop() from the enemy's defeat code.");
    }

    // ------------------------------------------------------------------
    // Tools > HUD > Add Star Counter + Stamina Bar To Existing HUD
    // For a HUD you already built and put sprites on: adds the new parts without rebuilding.
    // ------------------------------------------------------------------
    [MenuItem("Tools/HUD/Add Star Counter + Stamina Bar To Existing HUD")]
    public static void AddStarAndStaminaToExisting()
    {
        GameObject hud = GameObject.Find("PlayerHUD");
        if (hud == null)
        {
            Debug.LogWarning("No active 'PlayerHUD' in the scene. Use Tools > HUD > Create Player HUD instead.");
            return;
        }

        Transform t = hud.transform;
        if (t.Find("Stars") == null)
            Undo.RegisterCreatedObjectUndo(BuildStarCounter(t), "Add Star Counter");
        if (t.Find("StaminaBar") == null)
            Undo.RegisterCreatedObjectUndo(BuildStaminaBar(t), "Add Stamina Bar");

        // Make room for the star counter: the battery counter slides right of it
        Transform battery = t.Find("Batteries");
        if (battery != null)
        {
            RectTransform br = battery.GetComponent<RectTransform>();
            Undo.RecordObject(br, "Move Batteries");
            br.anchoredPosition = new Vector2(150f, -185f);
        }

        Selection.activeObject = hud;
        Debug.Log("Added the star counter + stamina bar to PlayerHUD and moved the battery counter next to the stars.");
    }

    private static GameObject BuildStarCounter(Transform hud)
    {
        Sprite knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

        GameObject stars = NewUI("Stars", hud);
        PlaceTL(stars, new Vector2(10f, 195f), new Vector2(135f, 80f));

        GameObject iconGO = NewUI("StarIcon", stars.transform);
        PlaceTL(iconGO, Vector2.zero, new Vector2(64f, 64f));
        Image iconImg = AddImage(iconGO, new Color(1f, 0.8f, 0.25f));
        iconImg.sprite = knob;
        iconImg.raycastTarget = false;
        iconImg.preserveAspect = true;

        TextMeshProUGUI count = AddText(stars.transform, "StarCount", "0", 56, Color.white, FontStyles.Bold,
            TextAlignmentOptions.Left, Vector2.zero, new Vector2(60f, 70f));
        RectTransform cr = count.rectTransform;
        cr.anchorMin = cr.anchorMax = cr.pivot = new Vector2(0f, 1f);
        cr.anchoredPosition = new Vector2(80f, 0f);

        StarCounterUI ui = stars.AddComponent<StarCounterUI>();
        ui.starCountText = count;
        ui.starIcon = iconGO.transform;
        ui.prefix = ""; // just the number, like the mockup

        // Let the StarManager update this same text when a star is picked up
        StarManager manager = Object.FindAnyObjectByType<StarManager>();
        if (manager != null && manager.starCounterText == null)
        {
            Undo.RecordObject(manager, "Link star counter");
            manager.starCounterText = count;
        }

        return stars;
    }

    private static GameObject BuildStaminaBar(Transform hud)
    {
        GameObject bar = NewUI("StaminaBar", hud);
        PlaceTL(bar, new Vector2(215f, 112f), new Vector2(325f, 30f));
        Image frame = AddImage(bar, Color.black);
        frame.raycastTarget = false;

        GameObject back = NewUI("Back", bar.transform);
        RectTransform br = back.GetComponent<RectTransform>();
        br.anchorMin = Vector2.zero;
        br.anchorMax = Vector2.one;
        br.offsetMin = new Vector2(3f, 3f);
        br.offsetMax = new Vector2(-3f, -3f);
        Image backImg = AddImage(back, new Color(0.04f, 0.16f, 0.2f));
        backImg.raycastTarget = false;

        // The fill is shrunk from the right by StaminaBarHUD (anchorMax.x = stamina)
        GameObject fillGO = NewUI("Fill", back.transform);
        Stretch(fillGO);
        Image fillImg = AddImage(fillGO, new Color(0.25f, 0.95f, 1f));
        fillImg.raycastTarget = false;

        StaminaBarHUD staminaHud = bar.AddComponent<StaminaBarHUD>();
        staminaHud.fill = fillGO.GetComponent<RectTransform>();
        staminaHud.fillImage = fillImg;
        staminaHud.normalColor = fillImg.color;

        return bar;
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static void EnsureFolder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + name))
            AssetDatabase.CreateFolder(parent, name);
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

    // Anchored to the middle of its parent
    private static void SetCenter(GameObject go, Vector2 anchoredPos, Vector2 size)
    {
        RectTransform r = go.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
        r.sizeDelta = size;
        r.anchoredPosition = anchoredPos;
    }

    // Positioned from the top-left corner of its parent (x right, y DOWN)
    private static void PlaceTL(GameObject go, Vector2 posFromTopLeft, Vector2 size)
    {
        RectTransform r = go.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0f, 1f);
        r.sizeDelta = size;
        r.anchoredPosition = new Vector2(posFromTopLeft.x, -posFromTopLeft.y);
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
        SetCenter(go, pos, rectSize);

        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
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