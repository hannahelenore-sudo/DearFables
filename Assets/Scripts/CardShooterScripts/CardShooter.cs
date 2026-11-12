using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Press E to cycle through the items you own. The icons float above your head.
/// Left click uses the selected item (toward the mouse):
///   Card      - flies up to 'range' meters, then falls with real physics. Hitting an EnemyChase starts the battle.
///   Tsinelas  - (needs the Tsinelas trinket) hitting an enemy stuns it: it spins and sees stars.
///   Small Rock- (needs the Small Rock trinket) hitting an enemy clears your current Mood.
///   Wind Card - HOLD left click to run faster. Drains the same stamina bar. See WindBoost.cs.
/// Every throw costs stamina from the bar.
/// </summary>
public class CardShooter : MonoBehaviour
{
    [Header("Shooting")]
    [Tooltip("0 = left click, 1 = right click.")]
    public int shootMouseButton = 0;
    public float range = 3f;
    public float cardSpeed = 14f;
    public float cooldown = 0.4f;
    [Tooltip("How close the card must pass to an enemy's centre to hit it.")]
    public float hitRadius = 0.6f;
    [Tooltip("Height above the player's feet where the card spawns.")]
    public float spawnHeight = 0.5f;

    [Header("Items (press E to cycle)")]
    public KeyCode cycleKey = KeyCode.E;
    public AudioClip cycleSound;
    [Range(0f, 1f)] public float cycleVolume = 0.6f;
    [Tooltip("Show the selected item above the head for a moment when you switch or use it.")]
    public bool showItemIcons = true;
    [Tooltip("ON: the Wind Card only appears when you own the Wind Fan trinket. OFF: it is always available.")]
    public bool windRequiresWindFan = false;
    [Tooltip("Optional. If empty, the icon from the Trinket asset is used, then a generated one.")]
    public Sprite tsinelasSprite;
    public Sprite smallRockSprite;
    public Sprite windCardSprite;

    [Header("Throwables (Tsinelas / Small Rock)")]
    public float itemRange = 4f;
    public float itemSpeed = 14f;
    [Tooltip("Spin in the air (degrees/second).")]
    public float itemSpinSpeed = 720f;
    [Tooltip("Size as a fraction of the character's height.")]
    public float itemHeightRatio = 0.22f;
    [Tooltip("How long the Tsinelas stuns an enemy (seconds).")]
    public float stunDuration = 2.5f;
    public AudioClip itemThrowSound;

    [Header("Stamina")]
    [Tooltip("Leave empty: a CardStamina is added to this object automatically.")]
    public CardStamina stamina;
    [Tooltip("Cards used by each throw.")]
    public float cardCost = 1f;
    public float tsinelasCost = 1f;
    [Tooltip("Stamina used by a Small Rock. 0 = free, because it already uses up the item itself.")]
    public float smallRockCost = 0f;
    [Tooltip("Played when you try to use something with no stamina left.")]
    public AudioClip emptySound;
    [Range(0f, 1f)] public float emptyVolume = 1f;

    [Header("Visual - Size")]
    [Tooltip("Sprite of the thrown card.")]
    public Sprite cardSprite;
    [Tooltip("Card height as a fraction of the character's height. 0.3 = card is about 30% as tall as the character.")]
    public float cardHeightRatio = 0.3f;
    [Tooltip("Extra multiplier on top of the automatic size (1 = no change). Keep this positive!")]
    public float cardScale = 1f;

    [Header("Visual - Glow (cards only)")]
    public bool glow = true;
    [Tooltip("Colour and strength (alpha) of the glow. If you use Bloom in your Global Volume, a bright colour here will bloom too.")]
    public Color glowColor = new Color(1f, 0.85f, 0.3f, 0.8f);
    [Tooltip("How far the glow reaches past the card (2 = twice the card size).")]
    public float glowSize = 2.2f;
    public float glowPulseSpeed = 6f;
    [Range(0f, 0.8f)] public float glowPulseAmount = 0.25f;

    [Header("Visual - Layer")]
    [Tooltip("Draws the card just behind the player's sprite (same sorting layer): visible over the ground, but the player covers it.")]
    public bool sortBehindPlayer = true;
    [Tooltip("How many sorting steps under the player the card is drawn. 1 = right behind the player.")]
    public int stepsBehindPlayer = 1;
    [Tooltip("Only used when Sort Behind Player is off.")]
    public string sortingLayerName = "Default";
    public int cardSortingOrder = 0;

    [Header("Flight")]
    [Tooltip("How fast the card spins in the air (degrees/second).")]
    public float spinSpeed = 360f;

    [Header("Falling (physics)")]
    [Tooltip("How much of the throw speed it keeps when it starts to fall. 0 = drops straight down, 1 = keeps flying forward.")]
    [Range(0f, 1f)] public float forwardSpeedWhenFalling = 0.2f;
    [Tooltip("How hard it tumbles when it starts to fall (degrees/second).")]
    public float tumbleSpeed = 300f;
    [Tooltip("Thickness of the collider in meters.")]
    public float cardThickness = 0.02f;
    [Range(0f, 1f)] public float cardBounciness = 0.1f;
    [Range(0f, 1f)] public float cardFriction = 0.6f;
    [Tooltip("Considered landed after this many seconds even if it is still wobbling.")]
    public float maxSettleTime = 3f;
    [Tooltip("How long it stays on the ground before fading out.")]
    public float groundStayTime = 1f;
    public float fadeTime = 0.4f;

    [Header("Sound")]
    public AudioClip throwSound;
    [Range(0f, 1f)] public float throwVolume = 1f;

    private float cooldownTimer;
    private SpriteRenderer characterRenderer;
    private Collider[] ownColliders;

    private HeldItemIcons icons;
    private WindBoost wind;
    private ThrowItem selected = ThrowItem.Card;
    private readonly List<ThrowItem> available = new List<ThrowItem>();

    private static Sprite glowSprite;

    private void Start()
    {
        // Use the tallest sprite under the player as the "character" (for sizes and sorting)
        float best = 0f;
        foreach (SpriteRenderer sr in GetComponentsInChildren<SpriteRenderer>())
        {
            if (sr.bounds.size.y > best)
            {
                best = sr.bounds.size.y;
                characterRenderer = sr;
            }
        }

        // Thrown things must never collide with the player who throws them
        ownColliders = GetComponentsInChildren<Collider>();

        if (stamina == null) stamina = GetComponent<CardStamina>();
        if (stamina == null) stamina = gameObject.AddComponent<CardStamina>();

        wind = GetComponent<WindBoost>();
        if (wind == null) wind = gameObject.AddComponent<WindBoost>();
        wind.Init(stamina, characterRenderer);

        if (showItemIcons)
        {
            icons = GetComponent<HeldItemIcons>();
            if (icons == null) icons = gameObject.AddComponent<HeldItemIcons>();
            icons.Init(characterRenderer);
        }
    }

    private void OnDisable()
    {
        if (wind != null) wind.Deactivate();
    }

    private void Update()
    {
        if (cooldownTimer > 0f) cooldownTimer -= Time.deltaTime;

        if (Time.timeScale <= 0f) return; // paused: the boost just waits

        RefreshItems();

        if (Input.GetKeyDown(cycleKey)) CycleItem();

        // Don't use items when clicking UI (boon cards, buttons)
        bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        // ---- Wind Card: click once, runs until the stamina is drained ----
        if (selected == ThrowItem.WindCard)
        {
            if (Input.GetMouseButtonDown(shootMouseButton) && !overUI)
            {
                if (wind.IsActive) { }                 // already running
                else if (wind.CanStart) { wind.Activate(); ShowIcon(false); }
                else Denied();
            }
            return;
        }
        wind.Deactivate(); // any other item selected: no boost, no drain

        // ---- Throwing ----
        if (!Input.GetMouseButtonDown(shootMouseButton)) return;
        if (cooldownTimer > 0f) return;
        if (overUI) return;

        float cost = GetCost(selected);
        if (cost > 0f && stamina != null && !stamina.CanThrow(cost))
        {
            Denied();
            return;
        }

        if (!TryGetAimDirection(out Vector3 direction)) return;

        ThrowItem thrown = selected;
        Shoot(direction, thrown);
        if (thrown == ThrowItem.SmallRock) ConsumeSmallRock();
        if (cost > 0f && stamina != null) stamina.Use(cost);
        cooldownTimer = cooldown;
        ShowIcon(false);
    }

    private void Denied()
    {
        if (stamina != null) stamina.NotifyDenied();
        if (emptySound != null)
            GameSFX.Play(emptySound, emptyVolume, 1f);
    }

    // ---------------- Items ----------------

    private void RefreshItems()
    {
        available.Clear();
        available.Add(ThrowItem.Card);
        if (TrinketEffects.CanStunEnemies) available.Add(ThrowItem.Tsinelas);
        if (TrinketEffects.CanRemoveMoods) available.Add(ThrowItem.SmallRock);
        if (!windRequiresWindFan || TrinketEffects.HasDash) available.Add(ThrowItem.WindCard);

        // Lost the selected item (sold it, or the last Small Rock was thrown) -> back to the card
        if (!available.Contains(selected))
        {
            selected = ThrowItem.Card;
            ShowIcon(true);
        }
    }

    private void ShowIcon(bool pop)
    {
        if (icons != null) icons.Show(GetIcon(selected), pop);
    }

    /// <summary>The Small Rock is a consumable: each throw uses up one (the sidebar animates the removal).</summary>
    private void ConsumeSmallRock()
    {
        var owned = TrinketInventory.Owned;
        for (int i = owned.Count - 1; i >= 0; i--)
        {
            if (owned[i] != null && owned[i].type == TrinketType.SmallRock)
            {
                TrinketInventory.RemoveAt(i);
                return;
            }
        }
    }

    private void CycleItem()
    {
        if (available.Count > 1)
        {
            int index = available.IndexOf(selected);
            selected = available[(index + 1) % available.Count];

            if (wind != null) wind.Deactivate();
            if (cycleSound != null)
                GameSFX.Play(cycleSound, cycleVolume, 1f);
        }

        ShowIcon(true); // pressing E always shows what you're holding
    }

    private float GetCost(ThrowItem item)
    {
        switch (item)
        {
            case ThrowItem.Tsinelas: return tsinelasCost;
            case ThrowItem.SmallRock: return smallRockCost;
            default: return cardCost;
        }
    }

    private Sprite GetIcon(ThrowItem item)
    {
        switch (item)
        {
            case ThrowItem.Tsinelas:
                if (tsinelasSprite != null) return tsinelasSprite;
                Sprite t = FindTrinketIcon(TrinketType.Tsinelas);
                return t != null ? t : ItemSprites.Tsinelas;

            case ThrowItem.SmallRock:
                if (smallRockSprite != null) return smallRockSprite;
                Sprite r = FindTrinketIcon(TrinketType.SmallRock);
                return r != null ? r : ItemSprites.Rock;

            case ThrowItem.WindCard:
                return windCardSprite != null ? windCardSprite : ItemSprites.Wind;

            default:
                return cardSprite != null ? cardSprite : ItemSprites.Card;
        }
    }

    private Sprite FindTrinketIcon(TrinketType type)
    {
        var owned = TrinketInventory.Owned;
        for (int i = 0; i < owned.Count; i++)
            if (owned[i] != null && owned[i].type == type) return owned[i].icon;
        return null;
    }

    // ---------------- Aiming ----------------

    private bool TryGetAimDirection(out Vector3 direction)
    {
        direction = Vector3.zero;

        Camera cam = Camera.main;
        if (cam == null) return false;

        // Where does the mouse point on the ground, at the player's height?
        Plane ground = new Plane(Vector3.up, transform.position);
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (!ground.Raycast(ray, out float distance)) return false;

        Vector3 aimPoint = ray.GetPoint(distance);
        direction = aimPoint - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f) return false;

        direction.Normalize();
        return true;
    }

    /// <summary>Scale that makes a sprite a fixed fraction of the character's height.</summary>
    private float GetScaleFor(Sprite sprite, float heightRatio)
    {
        float characterHeight = characterRenderer != null
            ? characterRenderer.bounds.size.y
            : transform.lossyScale.y;

        float spriteHeight = sprite != null ? sprite.bounds.size.y : 0f;
        if (spriteHeight < 0.0001f) return 1f;

        return characterHeight * heightRatio / spriteHeight;
    }

    private void ApplySorting(SpriteRenderer target, int extraOffset)
    {
        if (sortBehindPlayer && characterRenderer != null)
        {
            // Same sorting layer as the player, a few steps behind it
            target.sortingLayerID = characterRenderer.sortingLayerID;
            target.sortingOrder = Mathf.Clamp(characterRenderer.sortingOrder - stepsBehindPlayer + extraOffset, -32768, 32767);
        }
        else
        {
            if (!string.IsNullOrEmpty(sortingLayerName)) target.sortingLayerName = sortingLayerName;
            target.sortingOrder = Mathf.Clamp(cardSortingOrder + extraOffset, -32768, 32767);
        }
    }

    private void Shoot(Vector3 direction, ThrowItem item)
    {
        bool isCard = item == ThrowItem.Card;
        Sprite sprite = GetIcon(item);

        GameObject go = new GameObject(isCard ? "CardProjectile" : item + "Projectile");
        go.transform.position = transform.position + Vector3.up * spawnHeight;

        float scale = isCard
            ? GetScaleFor(sprite, cardHeightRatio) * Mathf.Abs(cardScale)
            : GetScaleFor(sprite, itemHeightRatio);
        go.transform.localScale = Vector3.one * scale;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        ApplySorting(sr, 0);

        // Soft glow behind the card (a child, so it moves/spins/falls with it)
        if (isCard && glow && sprite != null)
        {
            GameObject g = new GameObject("Glow");
            g.transform.SetParent(go.transform, false);
            Vector3 size = sprite.bounds.size;
            g.transform.localPosition = sprite.bounds.center;
            g.transform.localScale = new Vector3(size.x * glowSize, size.y * glowSize, 1f);

            SpriteRenderer gsr = g.AddComponent<SpriteRenderer>();
            gsr.sprite = GetGlowSprite();
            gsr.color = glowColor;
            ApplySorting(gsr, -1); // right behind the card
        }

        EnemyChase[] enemies = FindObjectsByType<EnemyChase>(FindObjectsSortMode.None);

        CardProjectile projectile = go.AddComponent<CardProjectile>();
        projectile.kind = item;
        projectile.stunDuration = stunDuration;
        projectile.Launch(direction,
                          isCard ? cardSpeed : itemSpeed,
                          isCard ? range : itemRange,
                          hitRadius,
                          isCard ? spinSpeed : itemSpinSpeed,
                          enemies, ownColliders,
                          forwardSpeedWhenFalling, tumbleSpeed, cardThickness,
                          cardBounciness, cardFriction, maxSettleTime, groundStayTime, fadeTime);
        projectile.SetGlowPulse(glowPulseSpeed, glowPulseAmount);

        AudioClip clip = (!isCard && itemThrowSound != null) ? itemThrowSound : throwSound;
        if (clip != null)
            GameSFX.Play(clip, throwVolume, Random.Range(0.95f, 1.08f));
    }

    // Generates a soft round gradient so no glow image is needed
    private static Sprite GetGlowSprite()
    {
        if (glowSprite != null) return glowSprite;

        const int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        float c = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float r = Vector2.Distance(new Vector2(x, y), new Vector2(c, c)) / (size * 0.5f);
                float a = Mathf.Pow(Mathf.Clamp01(1f - r), 2f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();

        // pixelsPerUnit = size -> the glow sprite is exactly 1 unit wide
        glowSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        return glowSprite;
    }
}