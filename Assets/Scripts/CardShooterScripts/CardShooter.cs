using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Left click throws a card toward the mouse (costs 1 card from the stamina wheel).
/// The card flies up to 'range' meters, then falls with real physics and lands on the ground.
/// If it hits an EnemyChase while flying, the battle transition starts.
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

    [Header("Stamina")]
    [Tooltip("Leave empty: a CardStamina is added to this object automatically.")]
    public CardStamina stamina;
    [Tooltip("Played when you try to throw with no cards left.")]
    public AudioClip emptySound;
    [Range(0f, 1f)] public float emptyVolume = 1f;

    [Header("Visual - Size")]
    [Tooltip("Sprite of the thrown card.")]
    public Sprite cardSprite;
    [Tooltip("Card height as a fraction of the character's height. 0.3 = card is about 30% as tall as the character.")]
    public float cardHeightRatio = 0.3f;
    [Tooltip("Extra multiplier on top of the automatic size (1 = no change). Keep this positive!")]
    public float cardScale = 1f;

    [Header("Visual - Glow")]
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
    [Tooltip("How much of the throw speed the card keeps when it starts to fall. 0 = drops straight down, 1 = keeps flying forward.")]
    [Range(0f, 1f)] public float forwardSpeedWhenFalling = 0.2f;
    [Tooltip("How hard the card tumbles when it starts to fall (degrees/second).")]
    public float tumbleSpeed = 300f;
    [Tooltip("Thickness of the card's collider in meters.")]
    public float cardThickness = 0.02f;
    [Range(0f, 1f)] public float cardBounciness = 0.1f;
    [Range(0f, 1f)] public float cardFriction = 0.6f;
    [Tooltip("Card is considered landed after this many seconds even if it is still wobbling.")]
    public float maxSettleTime = 3f;
    [Tooltip("How long the card stays on the ground before fading out.")]
    public float groundStayTime = 1f;
    public float fadeTime = 0.4f;

    [Header("Sound")]
    public AudioClip throwSound;
    [Range(0f, 1f)] public float throwVolume = 1f;

    private float cooldownTimer;
    private SpriteRenderer characterRenderer;
    private Collider[] ownColliders;

    private static Sprite glowSprite;

    private void Start()
    {
        // Use the tallest sprite under the player as the "character" (for card size and sorting)
        float best = 0f;
        foreach (SpriteRenderer sr in GetComponentsInChildren<SpriteRenderer>())
        {
            if (sr.bounds.size.y > best)
            {
                best = sr.bounds.size.y;
                characterRenderer = sr;
            }
        }

        // The card must never collide with the player who throws it
        ownColliders = GetComponentsInChildren<Collider>();

        if (stamina == null) stamina = GetComponent<CardStamina>();
        if (stamina == null) stamina = gameObject.AddComponent<CardStamina>();
    }

    private void Update()
    {
        if (cooldownTimer > 0f) cooldownTimer -= Time.deltaTime;

        if (Time.timeScale <= 0f) return;
        if (!Input.GetMouseButtonDown(shootMouseButton)) return;
        if (cooldownTimer > 0f) return;

        // Don't throw when clicking UI (boon cards, buttons)
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        // Out of cards (or still recovering from running dry)
        if (stamina != null && !stamina.CanThrow())
        {
            stamina.NotifyDenied();
            if (emptySound != null)
                GameSFX.Play(emptySound, emptyVolume, 1f);
            return;
        }

        if (!TryGetAimDirection(out Vector3 direction)) return;

        Shoot(direction);
        if (stamina != null) stamina.Use();
        cooldownTimer = cooldown;
    }

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

    /// <summary>Scale that makes the card a fixed fraction of the character's height.</summary>
    private float GetCardScale()
    {
        if (cardSprite == null) return Mathf.Abs(cardScale);

        float characterHeight = characterRenderer != null
            ? characterRenderer.bounds.size.y
            : transform.lossyScale.y;

        float spriteHeight = cardSprite.bounds.size.y;
        if (spriteHeight < 0.0001f) return Mathf.Abs(cardScale);

        return (characterHeight * cardHeightRatio / spriteHeight) * Mathf.Abs(cardScale);
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

    private void Shoot(Vector3 direction)
    {
        if (cardSprite == null)
            Debug.LogWarning("CardShooter: no Card Sprite assigned, the card will be invisible.", this);

        GameObject go = new GameObject("CardProjectile");
        go.transform.position = transform.position + Vector3.up * spawnHeight;
        go.transform.localScale = Vector3.one * GetCardScale();

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = cardSprite;
        ApplySorting(sr, 0);

        // Soft glow behind the card (a child, so it moves/spins/falls with it)
        if (glow && cardSprite != null)
        {
            GameObject g = new GameObject("Glow");
            g.transform.SetParent(go.transform, false);
            Vector3 size = cardSprite.bounds.size;
            g.transform.localPosition = cardSprite.bounds.center;
            g.transform.localScale = new Vector3(size.x * glowSize, size.y * glowSize, 1f);

            SpriteRenderer gsr = g.AddComponent<SpriteRenderer>();
            gsr.sprite = GetGlowSprite();
            gsr.color = glowColor;
            ApplySorting(gsr, -1); // right behind the card
        }

        EnemyChase[] enemies = FindObjectsByType<EnemyChase>(FindObjectsSortMode.None);

        CardProjectile projectile = go.AddComponent<CardProjectile>();
        projectile.Launch(direction, cardSpeed, range, hitRadius, spinSpeed, enemies, ownColliders,
                          forwardSpeedWhenFalling, tumbleSpeed, cardThickness,
                          cardBounciness, cardFriction, maxSettleTime, groundStayTime, fadeTime);
        projectile.SetGlowPulse(glowPulseSpeed, glowPulseAmount);

        if (throwSound != null)
            GameSFX.Play(throwSound, throwVolume, Random.Range(0.95f, 1.08f));
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