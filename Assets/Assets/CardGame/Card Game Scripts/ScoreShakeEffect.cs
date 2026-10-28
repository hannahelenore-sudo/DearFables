using System.Collections;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class ScoreShakeEffect : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private Color originalColor;
    private Vector3 originalLocalPosition;

    [Header("Shake Settings")]
    [Tooltip("How intense the shaking effect is.")]
    public float shakeMagnitude = 0.1f;

    [Header("Color Settings")]
    [Tooltip("The color the sprite turns into while counting down.")]
    public Color flashColor = Color.red;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
        }
        originalLocalPosition = transform.localPosition;
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
}