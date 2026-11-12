using System.Collections;
using UnityEngine;

/// <summary>
/// A battery lying in the world that the player collects, like a Mario coin: it pops out in an arc, bounces,
/// then spins and bobs until you walk near it (it also gets pulled toward you when you're close).
/// Make one with Tools > Battery > Create Battery Pickup Prefab, then swap the sprite.
/// BatteryDropper spawns these.
/// </summary>
public class BatteryPickup : MonoBehaviour
{
    [Tooltip("How many batteries it gives.")]
    public int value = 1;

    [Header("Pop out")]
    public float gravity = 20f;
    [Tooltip("How much of its speed it keeps when it bounces on the ground.")]
    [Range(0f, 0.9f)] public float bounce = 0.45f;

    [Header("Idle")]
    public float bobHeight = 0.12f;
    public float bobSpeed = 3f;
    [Tooltip("Spins like a coin (the sprite squashes sideways and back).")]
    public bool spin = true;
    public float spinSpeed = 4f;
    [Tooltip("Always faces the camera.")]
    public bool faceCamera = true;

    [Header("Collecting")]
    [Tooltip("How close the player must be to collect it.")]
    public float collectRadius = 1.1f;
    [Tooltip("Within this distance it flies toward the player. 0 = no magnet.")]
    public float magnetRadius = 3f;
    public float magnetSpeed = 12f;
    [Tooltip("Can't be collected for this long after it appears, so you see it pop out.")]
    public float collectDelay = 0.5f;
    [Tooltip("Disappears after this many seconds. 0 = never.")]
    public float lifetime = 0f;

    [Header("Audio")]
    public AudioClip collectSound;
    [Range(0f, 1f)] public float collectVolume = 1f;

    private Vector3 velocity;
    private float groundY;
    private bool landed;
    private bool collected;
    private float age;
    private float baseY;
    private float phase;
    private Vector3 baseScale = Vector3.one;
    private static PlayerMovement player;

    private void Awake()
    {
        baseScale = transform.localScale;
        groundY = transform.position.y;
        baseY = groundY;
        phase = Random.value * 10f;
        landed = true; // stays put (and bobs) unless Launch() is called
    }

    /// <summary>Throws the battery into the air with this velocity (called by BatteryDropper).</summary>
    public void Launch(Vector3 initialVelocity, float groundHeight)
    {
        velocity = initialVelocity;
        groundY = groundHeight;
        landed = false;
    }

    private void Update()
    {
        if (collected) return;

        float dt = Time.deltaTime;
        age += dt;

        if (lifetime > 0f && age >= lifetime)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 pos = transform.position;

        if (!landed)
        {
            velocity.y -= gravity * dt;
            pos += velocity * dt;

            if (pos.y <= groundY && velocity.y < 0f)
            {
                pos.y = groundY;
                if (Mathf.Abs(velocity.y) > 2f)
                {
                    velocity.y = -velocity.y * bounce;
                    velocity.x *= 0.6f;
                    velocity.z *= 0.6f;
                }
                else
                {
                    landed = true;
                    velocity = Vector3.zero;
                    baseY = groundY;
                }
            }
        }
        else
        {
            // Bob up and down on the ground
            float bob = (Mathf.Sin(Time.time * bobSpeed + phase) + 1f) * 0.5f * bobHeight;
            pos.y = baseY + bob;
        }

        // Player: collect / magnet
        if (age >= collectDelay && FindPlayer())
        {
            Vector3 toPlayer = player.transform.position - pos;
            toPlayer.y = 0f;
            float dist = toPlayer.magnitude;

            if (dist <= collectRadius)
            {
                transform.position = pos;
                Collect();
                return;
            }

            if (landed && magnetRadius > 0f && dist <= magnetRadius)
            {
                float pull = 1f - dist / magnetRadius; // pulls harder the closer you are
                pos += toPlayer.normalized * (magnetSpeed * (0.4f + pull) * dt);
            }
        }

        transform.position = pos;

        // Looks
        if (faceCamera && Camera.main != null)
            transform.rotation = Camera.main.transform.rotation;

        if (spin)
        {
            float flip = Mathf.Max(0.08f, Mathf.Abs(Mathf.Cos(Time.time * spinSpeed + phase)));
            transform.localScale = new Vector3(baseScale.x * flip, baseScale.y, baseScale.z);
        }
    }

    private bool FindPlayer()
    {
        if (player == null) player = Object.FindAnyObjectByType<PlayerMovement>();
        return player != null;
    }

    private void Collect()
    {
        collected = true;
        BatteryManager.Add(value);

        if (collectSound != null)
            AudioSource.PlayClipAtPoint(collectSound, transform.position, collectVolume);

        StartCoroutine(CollectEffect());
    }

    // Quick pop upward and fade, then gone
    private IEnumerator CollectEffect()
    {
        SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>();
        Color c = sr != null ? sr.color : Color.white;
        Vector3 start = transform.position;
        Vector3 startScale = transform.localScale;

        const float duration = 0.25f;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float x = Mathf.Clamp01(t / duration);

            transform.position = start + Vector3.up * (x * 0.8f);
            transform.localScale = startScale * (1f + x * 0.6f);
            if (sr != null) { Color f = c; f.a = 1f - x; sr.color = f; }
            yield return null;
        }

        Destroy(gameObject);
    }
}
