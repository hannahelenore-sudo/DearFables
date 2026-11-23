using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

public static class BattleStarReward
{
    private static bool hasValue;
    private static bool dropsStar = true;

    public static void Set(bool value) { dropsStar = value; hasValue = true; }

    public static bool Take()
    {
        bool result = !hasValue || dropsStar;
        hasValue = false;
        dropsStar = true;
        return result;
    }
}

public class EnemyChase : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("Drag your player here. This is the only thing the enemy will ever chase.")]
    public Transform player;

    [Header("Chase Settings")]
    public float detectionRange = 6f;
    public float moveSpeed = 2.5f;
    [Tooltip("Seconds the player must stay out of range before the enemy gives up.")]
    public float loseSightTime = 3f;
    [Tooltip("Enemy stops this close to the player.")]
    public float stopDistance = 0.5f;

    [Header("Star Reward")]
    [Tooltip("ON: beating this enemy gives a star (the 'Stars Gained' line shows on the result paper) and the enemy glows yellow in the middle. OFF: no star, and the 'Stars Gained' line is hidden.")]
    public bool dropsStar = true;
    [Tooltip("A yellow glow in the middle of the enemy while it is a star enemy.")]
    public bool glowWhenDropsStar = true;
    public Color starGlowColor = new Color(1f, 0.9f, 0.15f, 1f);
    [Tooltip("Glow size compared to the enemy picture (1 = as wide as the picture).")]
    [Range(0.1f, 1.5f)] public float starGlowSize = 0.7f;
    [Range(0f, 1f)] public float starGlowMinAlpha = 0.35f;
    [Range(0f, 1f)] public float starGlowMaxAlpha = 0.85f;
    public float starGlowPulseSpeed = 3f;

    [Header("Difficulty")]
    [Tooltip("ON: this enemy is counted when the game works out how many enemies there are (DifficultyProgress spreads the blinds 300 ... 12000 over that many enemies). Turn OFF for enemies that should not count. The StarQuest NPC is never counted.")]
    public bool countsForDifficulty = true;

    [Header("Card Game Unlock")]
    [Tooltip("ON: this enemy stays passive (no chasing, no touch battles) until the player has collected the card (CardPickup). Use it so the card game only starts after the card tutorial.")]
    public bool waitForCardUnlock = false;

    [Header("Battle Picture (optional)")]
    [Tooltip("ON: in the battle scene this enemy's picture is flipped / enlarged / moved as set below. OFF: the battle scene shows it as placed (other enemies stay as they are).")]
    public bool customBattlePicture = false;
    [Tooltip("Turn the picture around (mirror left/right).")]
    public bool battleFlip = true;
    [Tooltip("How much bigger the picture is drawn. 1 = unchanged, 2 = twice as big.")]
    public float battleScale = 2f;
    [Tooltip("Moves a SpriteRenderer picture (world units). Negative Y = lower.")]
    public Vector3 battleOffset = new Vector3(0f, -1f, 0f);
    [Tooltip("Moves a UI Image picture (pixels). Negative Y = lower.")]
    public Vector2 battleImageOffset = new Vector2(0f, -150f);

    [Header("Battle Start")]
    [Tooltip("A card thrown by CardShooter starts the battle when it hits this enemy.")]
    public bool startBattleOnCardHit = true;
    [Tooltip("Also start the battle when the enemy touches the player. Turn off if only cards should start fights.")]
    public bool touchStartsBattle = true;
    [Tooltip("Player this close counts as touching. Keep it a bit larger than Stop Distance.")]
    public float touchDistance = 0.8f;
    [Tooltip("Touching is ignored for this many seconds after the scene loads, so the player isn't instantly sent back into battle.")]
    public float touchGraceTime = 2f;
    [Tooltip("Play the Pokemon-style transition before the card game. Needs BattleTransition.cs in the project.")]
    public bool useBattleTransition = true;

    [Header("Card Hit Advantage")]
    [Tooltip("Hitting the enemy with a thrown card gives you an advantage: the score to beat is smaller for that fight.")]
    public bool cardHitGivesAdvantage = true;
    [Range(0f, 0.9f)]
    [Tooltip("How much of the score to beat is removed. 0.1 = 10% less.")]
    public float scoreReduction = 0.1f;

    [Header("Overpowered (Wind + Sun power > score to beat)")]
    [Tooltip("If your combined Wind + Sun power beats the score to beat, a thrown card makes the enemy explode instead of starting a battle.")]
    public bool explodeWhenOverpowered = true;
    [Tooltip("NO LONGER USED. The score to beat now comes from DifficultyProgress.BlindScores.")]
    public int baseTargetScore = 400;
    [Tooltip("NO LONGER USED.")]
    public float maxScoreMultiplier = 3f;
    [Tooltip("Sun + Wind together are multiplied by this before being compared with the score to beat. Higher = the combo obliterates enemies even at high difficulty. 1 = plain Sun + Wind.")]
    public float powerMultiplier = 2f;
    [Tooltip("Optional. Sprite for the X above the enemy. If empty, a red X is drawn automatically.")]
    public Sprite xIconSprite;
    public Vector3 xIconOffset = new Vector3(0f, 1.6f, 0f);
    public float xIconSize = 1f;
    [Tooltip("Optional. Spawned when the enemy explodes (e.g. a particle burst).")]
    public GameObject explosionPrefab;
    public AudioClip explosionSound;
    [Range(0f, 1f)] public float explosionVolume = 1f;
    [Tooltip("Stars the enemy drops when it explodes.")]
    public int starsDropped = 3;
    [Tooltip("Optional. Star pickup prefab (your StarCollectible). If empty, the stars are added to your total straight away.")]
    public GameObject starPickupPrefab;
    [Tooltip("Batteries the enemy drops when it explodes.")]
    public int batteriesDropped = 5;

    [Header("Stun (Tsinelas)")]
    [Tooltip("How long the fast spin at the start of the stun lasts.")]
    public float stunSpinTime = 0.7f;
    [Tooltip("Full turns during the fast spin.")]
    public float stunSpinTurns = 3f;
    [Tooltip("How far the dazed enemy sways left/right afterwards (degrees).")]
    public float stunSwayAngle = 8f;

    [Header("Moods (Small Rock)")]
    [Tooltip("Optional extra: runs when a Small Rock hits this enemy (e.g. play a particle).")]
    public UnityEvent onMoodsRemoved;

    [Header("Sound Effects")]
    [Tooltip("Played the moment the battle starts (card hit or touch).")]
    public AudioClip hitSound;
    [Range(0f, 1f)] public float hitVolume = 1f;
    [Tooltip("Small random pitch change so the sound doesn't feel identical every time. 0 = no variation.")]
    [Range(0f, 0.3f)] public float hitPitchVariation = 0f;

    [Header("Persistence")]
    [Tooltip("Unique name used to remember this enemy was caught. Leave empty to use the object's name (so give each enemy a different name).")]
    public string enemyId = "";

    [Header("Movement")]
    [Tooltip("Keeps the enemy at its current height (good for ground enemies).")]
    public bool lockYAxis = true;
    [Tooltip("After giving up, the enemy walks back to where it started.")]
    public bool returnToStartWhenLost = true;

    [Header("Visual")]
    [Tooltip("Sprite always turns to face the camera.")]
    public bool faceCamera = true;
    [Tooltip("Flip the sprite left/right depending on where the player is. Assumes the image faces right.")]
    public bool flipToFacePlayer = true;

    private static bool battleStarting;
    private bool isChasing;
    private bool battleStarted;
    private float outOfRangeTimer;
    private Vector3 startPosition;
    private SpriteRenderer spriteRenderer;
    private Color baseColor = Color.white;
    private SpriteRenderer starGlow;
    private static Sprite starGlowSprite;

    private float stunTimer;
    private float stunDuration;
    private DizzyStars stars;
    private Coroutine flashRoutine;

    private GameObject xIcon;
    private float powerCheckTimer;
    private bool overpowered;

    public bool IsStunned => stunTimer > 0f;

    private static readonly HashSet<string> caughtEnemies = new HashSet<string>();

    private static readonly HashSet<string> knownEnemies = new HashSet<string>();

    public static int KnownEnemyCount { get { return knownEnemies.Count; } }

    private bool countsInDifficulty;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetKnownEnemies()
    {
        knownEnemies.Clear();
    }

    private void Awake()
    {
        countsInDifficulty = countsForDifficulty && GetComponent<StarQuestNPC>() == null;
        if (countsInDifficulty) knownEnemies.Add(GetId());
    }

    public static void ResetCaughtEnemies()
    {
        caughtEnemies.Clear();
    }

    private string GetId()
    {
        string id = string.IsNullOrEmpty(enemyId) ? name : enemyId;
        return SceneManager.GetActiveScene().name + "/" + id;
    }

    public bool WasCaught { get { return caughtEnemies.Contains(GetId()); } }

    public void HitByCard()
    {
        if (!enabled) return;
        if (battleStarted) return;

        Debug.Log("EnemyChase '" + name + "': Sun " + DeckPower.Sun + " + Wind " + DeckPower.Wind
                  + " = " + DeckPower.Total + " (x" + powerMultiplier + " = " + GetCombinedPower() + ") vs score to beat " + GetScoreToBeat()
                  + (IsOverpowered() ? " -> EXPLODE" : " -> battle"));

        if (explodeWhenOverpowered && IsOverpowered())
        {
            Explode();
            return;
        }

        if (!startBattleOnCardHit) return;
        StartBattle(true);
    }

    public void Stun(float duration)
    {
        if (!enabled || battleStarted || duration <= 0f) return;

        stunDuration = duration;
        stunTimer = duration;

        if (stars == null && spriteRenderer != null)
        {
            stars = GetComponent<DizzyStars>();
            if (stars == null) stars = gameObject.AddComponent<DizzyStars>();
            stars.Init(spriteRenderer);
        }
        if (stars != null) stars.SetActive(true);

        Flash(new Color(1f, 0.9f, 0.5f));
    }

    public void RemoveMoods()
    {
        if (battleStarted) return;

        bool hadMood = !MoodManager.IsNeutral;
        MoodManager.Clear();

        if (onMoodsRemoved != null) onMoodsRemoved.Invoke();

        Flash(hadMood ? new Color(0.6f, 0.95f, 1f) : new Color(0.7f, 0.7f, 0.7f));
    }

    public int GetScoreToBeat()
    {
        return DifficultyProgress.GetEnemyScore(dropsStar);
    }

    public float GetCombinedPower()
    {
        return (DeckPower.Sun + DeckPower.Wind) * powerMultiplier;
    }

    public bool IsOverpowered()
    {
        return GetCombinedPower() > GetScoreToBeat();
    }

    private void UpdatePowerIcon()
    {
        if (!explodeWhenOverpowered) { SetIconVisible(false); return; }

        powerCheckTimer -= Time.deltaTime;
        if (powerCheckTimer > 0f) return;
        powerCheckTimer = 0.25f;

        overpowered = IsOverpowered();
        SetIconVisible(overpowered);
    }

    private void SetIconVisible(bool visible)
    {
        if (visible && xIcon == null) xIcon = CreateXIcon();
        if (xIcon != null && xIcon.activeSelf != visible) xIcon.SetActive(visible);
    }

    private GameObject CreateXIcon()
    {
        GameObject go = new GameObject("PowerX");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = xIconOffset;
        go.transform.localScale = Vector3.one * xIconSize;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = xIconSprite != null ? xIconSprite : GetDefaultXSprite();
        if (spriteRenderer != null)
        {
            sr.sortingLayerID = spriteRenderer.sortingLayerID;
            sr.sortingOrder = spriteRenderer.sortingOrder + 1;
        }
        return go;
    }

    private static Sprite defaultXSprite;

    private static Sprite GetDefaultXSprite()
    {
        if (defaultXSprite != null) return defaultXSprite;

        const int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Color red = new Color(0.9f, 0.1f, 0.1f);
        Color outline = Color.white;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Min(Mathf.Abs(x - y), Mathf.Abs(x - (size - 1 - y))) / 1.4142f;
                bool inside = x > 6 && x < size - 6 && y > 6 && y < size - 6;
                Color c = Color.clear;
                if (inside && d <= 5.5f) c = outline;
                if (inside && d <= 3.5f) c = red;
                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply();
        defaultXSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 64f);
        return defaultXSprite;
    }

    private void Explode()
    {
        battleStarted = true;
        caughtEnemies.Add(GetId());
        if (countsInDifficulty) DifficultyProgress.AddDefeat();

        stunTimer = 0f;
        if (stars != null) stars.SetActive(false);
        SetIconVisible(false);

        Debug.Log("EnemyChase: '" + name + "' was overpowered and exploded.");

        Vector3 pos = transform.position;

        if (explosionSound != null)
            GameSFX.Play(explosionSound, explosionVolume, 1f);
        else
            PlayHitSound();

        if (explosionPrefab != null) Instantiate(explosionPrefab, pos, Quaternion.identity);

        DropStars(pos);
        DropBatteries(pos);

        StartCoroutine(ExplodeRoutine());
    }

    private void DropStars(Vector3 pos)
    {
        if (!dropsStar || starsDropped <= 0) return;

        if (starPickupPrefab != null)
        {
            for (int i = 0; i < starsDropped; i++)
            {
                Vector2 c = Random.insideUnitCircle * 0.8f;
                Instantiate(starPickupPrefab, pos + new Vector3(c.x, 0.5f, c.y), Quaternion.identity);
            }
        }
        else if (StarManager.Instance != null)
        {
            StarManager.Instance.CollectStar(starsDropped);
        }
        else
        {
            PlayerProgress.AddStars(starsDropped);
        }
    }

    private void DropBatteries(Vector3 pos)
    {
        if (batteriesDropped <= 0) return;

        BatteryDropper dropper = GetComponent<BatteryDropper>();
        if (dropper == null)
        {
            foreach (BatteryDropper d in Object.FindObjectsByType<BatteryDropper>(FindObjectsSortMode.None))
            {
                if (dropper == null) dropper = d;
                if (d.pickupPrefab != null) { dropper = d; break; }
            }
        }
        if (dropper == null) dropper = gameObject.AddComponent<BatteryDropper>();
        dropper.DropAt(pos, batteriesDropped);
    }

    private IEnumerator ExplodeRoutine()
    {
        Vector3 startScale = transform.localScale;
        const float duration = 0.3f;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float x = Mathf.Clamp01(t / duration);
            transform.localScale = startScale * (1f + x * 0.8f);
            if (spriteRenderer != null)
            {
                Color c = Color.Lerp(Color.white, new Color(1f, 0.6f, 0.2f), x);
                c.a = 1f - x;
                spriteRenderer.color = c;
            }
            yield return null;
        }
        Destroy(gameObject);
    }

    private void Flash(Color flashColor)
    {
        if (spriteRenderer == null) return;
        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(FlashRoutine(flashColor));
    }

    private IEnumerator FlashRoutine(Color flashColor)
    {
        const float duration = 0.4f;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            spriteRenderer.color = Color.Lerp(flashColor, baseColor, t / duration);
            yield return null;
        }
        spriteRenderer.color = baseColor;
        flashRoutine = null;
    }

    private void OnEnable()
    {
        if (starGlow != null) starGlow.gameObject.SetActive(true);
    }

    private void OnDisable()
    {
        if (starGlow != null) starGlow.gameObject.SetActive(false);
    }

    private void Start()
    {
        battleStarting = false;

        if (caughtEnemies.Contains(GetId()))
        {
            Destroy(gameObject);
            return;
        }

        startPosition = transform.position;
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer != null) baseColor = spriteRenderer.color;

        if (player == null)
            Debug.LogError("EnemyChase on '" + name + "': no Player assigned! Drag your player into the 'Player' slot in the Inspector.", this);
    }

    private void Update()
    {
        if (player == null) return;
        if (battleStarted) return;

        if (waitForCardUnlock && CardPickup.Locked) return;

        UpdatePowerIcon();

        if (IsPlayerFrozen()) return;

        if (stunTimer > 0f)
        {
            stunTimer -= Time.deltaTime;
            if (stunTimer <= 0f)
            {
                stunTimer = 0f;
                if (stars != null) stars.SetActive(false);
            }
            return;
        }

        float distance = GetDistance(transform.position, player.position);

        if (touchStartsBattle && distance <= touchDistance && Time.timeSinceLevelLoad >= touchGraceTime)
        {
            StartBattle(false);
            return;
        }

        if (!isChasing)
        {
            if (distance <= detectionRange)
            {
                isChasing = true;
                outOfRangeTimer = 0f;
            }
        }
        else
        {
            if (distance > detectionRange)
            {
                outOfRangeTimer += Time.deltaTime;
                if (outOfRangeTimer >= loseSightTime)
                {
                    isChasing = false;
                    outOfRangeTimer = 0f;
                }
            }
            else
            {
                outOfRangeTimer = 0f;
            }
        }

        if (isChasing)
        {
            MoveToward(player.position, stopDistance);
            UpdateFlip(player.position);
        }
        else if (returnToStartWhenLost)
        {
            MoveToward(startPosition, 0.05f);
            UpdateFlip(startPosition);
        }
    }

    private PlayerMovement playerMovement;

    private bool IsPlayerFrozen()
    {
        if (playerMovement == null && player != null)
        {
            playerMovement = player.GetComponentInParent<PlayerMovement>();
            if (playerMovement == null) playerMovement = player.GetComponentInChildren<PlayerMovement>();
        }
        return playerMovement != null && playerMovement.InputLocked;
    }

    private void StartBattle(bool byCard)
    {
        if (byCard && battleStarted)
        {
            if (cardHitGivesAdvantage) AdvantageHit.Arm(scoreReduction);
            return;
        }

        if (battleStarted || battleStarting) return;
        if (player == null) return;

        PlayerPositionSaver saver = player.GetComponentInParent<PlayerPositionSaver>();

        if (saver == null)
        {
            Debug.LogWarning("Player has no PlayerPositionSaver component!", this);
            return;
        }

        battleStarted = true;
        battleStarting = true;
        caughtEnemies.Add(GetId());

        BattleEnemy.Set(spriteRenderer != null ? spriteRenderer.sprite : null);
        if (customBattlePicture)
            BattleEnemy.SetStyle(battleFlip, battleScale, battleOffset, battleImageOffset);

        BattleStarReward.Set(dropsStar);

        stunTimer = 0f;
        if (stars != null) stars.SetActive(false);

        if (byCard && cardHitGivesAdvantage) AdvantageHit.Arm(scoreReduction);
        else AdvantageHit.Disarm();

        Debug.Log("EnemyChase: '" + name + "' started a battle.");

        PlayHitSound();

        if (useBattleTransition)
        {
            BattleTransition.Play(() => saver.EnterBattle(false));
        }
        else
        {
            saver.EnterBattle(false);
        }
    }

    private void PlayHitSound()
    {
        if (hitSound == null) return;

        float pitch = 1f + Random.Range(-hitPitchVariation, hitPitchVariation);
        GameSFX.Play(hitSound, hitVolume, pitch);
    }

    private void UpdateStarGlow()
    {
        bool show = dropsStar && glowWhenDropsStar && !battleStarted && spriteRenderer != null && spriteRenderer.sprite != null;

        if (!show)
        {
            if (starGlow != null && starGlow.gameObject.activeSelf) starGlow.gameObject.SetActive(false);
            return;
        }

        if (starGlow == null) starGlow = CreateStarGlow();
        if (!starGlow.gameObject.activeSelf) starGlow.gameObject.SetActive(true);

        Bounds b = spriteRenderer.sprite.bounds;
        Vector3 center = b.center;
        if (spriteRenderer.flipX) center.x = -center.x;
        if (spriteRenderer.flipY) center.y = -center.y;

        Transform t = starGlow.transform;
        t.localPosition = center;
        t.localRotation = Quaternion.identity;

        float pulse = (Mathf.Sin(Time.time * starGlowPulseSpeed) + 1f) * 0.5f;
        float diameter = Mathf.Min(b.size.x, b.size.y) * starGlowSize * (1f + pulse * 0.08f);
        t.localScale = new Vector3(diameter, diameter, 1f);

        Color c = starGlowColor;
        c.a = Mathf.Lerp(starGlowMinAlpha, starGlowMaxAlpha, pulse);
        starGlow.color = c;

        starGlow.sortingLayerID = spriteRenderer.sortingLayerID;
        starGlow.sortingOrder = spriteRenderer.sortingOrder + 1;
    }

    private SpriteRenderer CreateStarGlow()
    {
        GameObject go = new GameObject("StarGlow");
        go.transform.SetParent(spriteRenderer.transform, false);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetStarGlowSprite();
        return sr;
    }

    private static Sprite GetStarGlowSprite()
    {
        if (starGlowSprite != null) return starGlowSprite;

        const int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        Vector2 c = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Clamp01(Vector2.Distance(new Vector2(x, y), c) / radius);
                float a = Mathf.Pow(1f - d, 1.8f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();

        starGlowSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        return starGlowSprite;
    }

    private void LateUpdate()
    {
        UpdateStarGlow();

        if (!faceCamera || Camera.main == null) return;

        float yaw = Camera.main.transform.eulerAngles.y;
        Quaternion rot = Quaternion.Euler(0f, yaw, 0f);

        if (stunTimer > 0f)
        {
            float elapsed = stunDuration - stunTimer;
            float spinTime = Mathf.Max(0.05f, Mathf.Min(stunSpinTime, stunDuration * 0.6f));

            float spinYaw = 0f;
            float roll = 0f;

            if (elapsed < spinTime)
            {
                float t = elapsed / spinTime;
                float eased = 1f - (1f - t) * (1f - t);
                spinYaw = eased * 360f * stunSpinTurns;
            }
            else
            {
                float fadeOut = Mathf.Clamp01(stunTimer / 0.3f);
                roll = Mathf.Sin(elapsed * 9f) * stunSwayAngle * fadeOut;
            }

            rot = Quaternion.Euler(0f, yaw + spinYaw, 0f) * Quaternion.Euler(0f, 0f, roll);
        }

        transform.rotation = rot;
    }

    private void MoveToward(Vector3 target, float minDistance)
    {
        if (lockYAxis) target.y = transform.position.y;

        if (Vector3.Distance(transform.position, target) <= minDistance) return;

        transform.position = Vector3.MoveTowards(transform.position, target, moveSpeed * Time.deltaTime);
    }

    private void UpdateFlip(Vector3 target)
    {
        if (!flipToFacePlayer || spriteRenderer == null || Camera.main == null) return;

        float side = Vector3.Dot(Camera.main.transform.right, target - transform.position);
        if (Mathf.Abs(side) > 0.01f)
            spriteRenderer.flipX = side < 0f;
    }

    private float GetDistance(Vector3 a, Vector3 b)
    {
        if (lockYAxis)
        {
            a.y = 0f;
            b.y = 0f;
        }
        return Vector3.Distance(a, b);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
    }
}