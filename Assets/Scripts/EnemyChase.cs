using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

/// <summary>
/// Put this on a GameObject with a SpriteRenderer (your enemy image).
/// - Player comes within detectionRange  -> enemy starts chasing.
/// - Player leaves the range for loseSightTime seconds -> enemy stops chasing.
/// - If the player comes back into range before the timer runs out, the timer resets.
/// - A card thrown by CardShooter hits the enemy (CardProjectile calls HitByCard) -> battle starts.
/// - A thrown Tsinelas stuns the enemy (Stun): it spins, sees stars and can't move or catch you for a while.
/// - A thrown Small Rock clears the player's current Mood (MoodManager.Clear).
/// - Optionally, the enemy touching the player also starts the battle (Touch Starts Battle).
/// - Battle start = hit sound, Pokemon-style transition, then the card game.
/// </summary>
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

    private static bool battleStarting; // stops two enemies starting a battle at once
    private bool isChasing;
    private bool battleStarted;
    private float outOfRangeTimer;
    private Vector3 startPosition;
    private SpriteRenderer spriteRenderer;
    private Color baseColor = Color.white;

    // Stun
    private float stunTimer;
    private float stunDuration;
    private DizzyStars stars;
    private Coroutine flashRoutine;

    /// <summary>True while a Tsinelas has this enemy stunned.</summary>
    public bool IsStunned => stunTimer > 0f;

    // Remembers which enemies already caught the player. Static = survives scene loads.
    private static readonly HashSet<string> caughtEnemies = new HashSet<string>();

    /// <summary>Call this to bring all enemies back (e.g. from a "New Game" button).</summary>
    public static void ResetCaughtEnemies()
    {
        caughtEnemies.Clear();
    }

    private string GetId()
    {
        string id = string.IsNullOrEmpty(enemyId) ? name : enemyId;
        return SceneManager.GetActiveScene().name + "/" + id;
    }

    /// <summary>Called by CardProjectile when the thrown card reaches this enemy.</summary>
    public void HitByCard()
    {
        if (!startBattleOnCardHit) return;
        StartBattle(true);
    }

    /// <summary>Called by CardProjectile when a thrown Tsinelas reaches this enemy.</summary>
    public void Stun(float duration)
    {
        if (battleStarted || duration <= 0f) return;

        stunDuration = duration;
        stunTimer = duration; // hitting it again restarts the spin

        if (stars == null && spriteRenderer != null)
        {
            stars = GetComponent<DizzyStars>();
            if (stars == null) stars = gameObject.AddComponent<DizzyStars>();
            stars.Init(spriteRenderer);
        }
        if (stars != null) stars.SetActive(true);

        Flash(new Color(1f, 0.9f, 0.5f)); // "bonk"
    }

    /// <summary>Called by CardProjectile when a thrown Small Rock reaches this enemy.</summary>
    public void RemoveMoods()
    {
        if (battleStarted) return;

        // Moods are the player's (Sugar Rush, Angry...): the rock knocks the current one off
        bool hadMood = !MoodManager.IsNeutral;
        MoodManager.Clear();

        if (onMoodsRemoved != null) onMoodsRemoved.Invoke();

        // Cyan flash if a mood was actually removed, a dull grey one if there was nothing to remove
        Flash(hadMood ? new Color(0.6f, 0.95f, 1f) : new Color(0.7f, 0.7f, 0.7f));
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

    private void Start()
    {
        battleStarting = false; // fresh scene -> allow battles again

        // This enemy already caught the player earlier -> it's gone
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
        // Only ever uses the player assigned in the Inspector
        if (player == null) return;
        if (battleStarted) return;

        // Stunned: can't move, chase or catch the player
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

        // Touched the player (optional) -> start the battle
        if (touchStartsBattle && distance <= touchDistance && Time.timeSinceLevelLoad >= touchGraceTime)
        {
            StartBattle(false);
            return;
        }

        if (!isChasing)
        {
            // Player walked into range -> start chasing
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
                // Out of range: count up, give up after loseSightTime
                outOfRangeTimer += Time.deltaTime;
                if (outOfRangeTimer >= loseSightTime)
                {
                    isChasing = false;
                    outOfRangeTimer = 0f;
                }
            }
            else
            {
                // Back in range: reset the timer
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

    private void StartBattle(bool byCard)
    {
        // A card that lands while the battle is already starting (the enemy touched you a moment before) still counts
        if (byCard && battleStarted)
        {
            if (cardHitGivesAdvantage) AdvantageHit.Arm(scoreReduction);
            return;
        }

        // Already fighting, or another enemy already started a battle this frame
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
        caughtEnemies.Add(GetId()); // gone when the player comes back

        // Stun is over, the fight begins
        stunTimer = 0f;
        if (stars != null) stars.SetActive(false);

        // Hit it with a card first = advantage (smaller score to beat). Touching it = no advantage.
        if (byCard && cardHitGivesAdvantage) AdvantageHit.Arm(scoreReduction);
        else AdvantageHit.Disarm();

        Debug.Log("EnemyChase: '" + name + "' started a battle.");

        PlayHitSound();

        if (useBattleTransition)
        {
            // Flash + bars, then enter the battle once the screen is black
            BattleTransition.Play(() => saver.EnterBattle(false)); // false = not started from the zone
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

    private void LateUpdate()
    {
        if (!faceCamera || Camera.main == null) return;

        // Turn only around the Y axis so the sprite stays upright
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
                // Fast cartoon spin that slows down (ends on a full turn, so it lands facing the camera)
                float t = elapsed / spinTime;
                float eased = 1f - (1f - t) * (1f - t);
                spinYaw = eased * 360f * stunSpinTurns;
            }
            else
            {
                // Dazed sway, easing out at the end of the stun
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

        // Is the target to the left or right from the camera's point of view?
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

    // Draws the detection range in the Scene view when the enemy is selected
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
    }
}