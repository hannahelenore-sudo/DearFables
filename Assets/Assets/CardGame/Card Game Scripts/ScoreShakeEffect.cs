using System.Collections;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class ScoreShakeEffect : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private Color originalColor;
    private Vector3 originalLocalPosition;
    private Quaternion originalLocalRotation;

    [Header("Shake Settings")]
    [Tooltip("How intense the shaking effect is.")]
    public float shakeMagnitude = 0.1f;

    [Header("Color Settings")]
    [Tooltip("The color the sprite turns into while counting down.")]
    public Color flashColor = Color.red;

    [Header("Fall Down Settings")]
    public float fallDuration = 0.8f;
    public float fallDistance = 8f;     // How far down the object drops
    public float maxTiltAngle = 45f;    // Max rotation tilt as it falls like bread

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
        }
        originalLocalPosition = transform.localPosition;
        originalLocalRotation = transform.localRotation;
    }

    /// <summary>
    /// Call this method from DeckManager to start the shake and red tint effect matching the duration.
    /// </summary>
    public void TriggerScoreEffect(float duration)
    {
        StopAllCoroutines();
        StartCoroutine(ShakeAndRedRoutine(duration));
    }

    private IEnumerator ShakeAndRedRoutine(float duration)
    {
        // Turn red and start shaking
        if (spriteRenderer != null)
        {
            spriteRenderer.color = flashColor;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;

            // Generate a random shake offset
            float offsetX = Random.Range(-1f, 1f) * shakeMagnitude;
            float offsetY = Random.Range(-1f, 1f) * shakeMagnitude;

            transform.localPosition = originalLocalPosition + new Vector3(offsetX, offsetY, 0f);

            yield return null;
        }

        // Reset back to original position and color when countdown ends
        transform.localPosition = originalLocalPosition;
        if (spriteRenderer != null)
        {
            spriteRenderer.color = originalColor;
        }
    }

    /// <summary>
    /// Triggers the bread-like falling down animation when the score hits zero.
    /// </summary>
    public void TriggerFallDown()
    {
        StopAllCoroutines();
        StartCoroutine(FallDownRoutine());
    }

    private IEnumerator FallDownRoutine()
    {
        Vector3 startPos = transform.localPosition;

        // Pick a random side drift and tilt angle for the bread fall
        float randomXOffset = Random.Range(-2f, 2f);
        Vector3 targetPos = startPos + new Vector3(randomXOffset, -fallDistance, 0f);
        float targetTilt = Random.Range(-maxTiltAngle, maxTiltAngle);

        float elapsed = 0f;
        while (elapsed < fallDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / fallDuration);

            // Quadratic ease-in (t * t) simulates gravity acceleration
            float gravityEase = t * t;

            transform.localPosition = Vector3.Lerp(startPos, targetPos, gravityEase);
            transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(0f, targetTilt, t));

            yield return null;
        }

        transform.localPosition = targetPos;
        transform.localRotation = Quaternion.Euler(0f, 0f, targetTilt);
    }
}