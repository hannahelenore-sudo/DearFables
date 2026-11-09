using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Put this on a GameObject with a SpriteRenderer (your enemy image).
/// - Player comes within detectionRange  -> enemy starts chasing.
/// - Player leaves the range for loseSightTime seconds -> enemy stops chasing.
/// - If the player comes back into range before the timer runs out, the timer resets.
/// - A card thrown by CardShooter hits the enemy (CardProjectile calls HitByCard) -> battle starts.
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
    public bool touchStartsBattle = false;
    [Tooltip("Player this close counts as touching. Keep it a bit larger than Stop Distance.")]
    public float touchDistance = 0.8f;
    [Tooltip("Touching is ignored for this many seconds after the scene loads, so the player isn't instantly sent back into battle.")]
    public float touchGraceTime = 2f;
    [Tooltip("Play the Pokemon-style transition before the card game. Needs BattleTransition.cs in the project.")]
    public bool useBattleTransition = true;

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
        StartBattle();
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

        if (player == null)
            Debug.LogError("EnemyChase on '" + name + "': no Player assigned! Drag your player into the 'Player' slot in the Inspector.", this);
    }

    private void Update()
    {
        // Only ever uses the player assigned in the Inspector
        if (player == null) return;
        if (battleStarted) return;

        float distance = GetDistance(transform.position, player.position);

        // Touched the player (optional) -> start the battle
        if (touchStartsBattle && distance <= touchDistance && Time.timeSinceLevelLoad >= touchGraceTime)
        {
            StartBattle();
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

    private void StartBattle()
    {
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
        transform.rotation = Quaternion.Euler(0f, Camera.main.transform.eulerAngles.y, 0f);
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