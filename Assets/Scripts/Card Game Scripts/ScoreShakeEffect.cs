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
    public float fallDistance = 8f;
    public float maxTiltAngle = 45f;

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

    public void TriggerScoreEffect(float duration)
    {
        StopAllCoroutines();
        StartCoroutine(ShakeAndRedRoutine(duration));
    }

    private IEnumerator ShakeAndRedRoutine(float duration)
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.color = flashColor;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;

            float offsetX = Random.Range(-1f, 1f) * shakeMagnitude;
            float offsetY = Random.Range(-1f, 1f) * shakeMagnitude;

            transform.localPosition = originalLocalPosition + new Vector3(offsetX, offsetY, 0f);

            yield return null;
        }

        transform.localPosition = originalLocalPosition;
        if (spriteRenderer != null)
        {
            spriteRenderer.color = originalColor;
        }
    }

    public void TriggerFallDown()
    {
        StopAllCoroutines();
        StartCoroutine(FallDownRoutine());
    }

    private IEnumerator FallDownRoutine()
    {
        Vector3 startPos = transform.localPosition;

        float randomXOffset = Random.Range(-2f, 2f);
        Vector3 targetPos = startPos + new Vector3(randomXOffset, -fallDistance, 0f);
        float targetTilt = Random.Range(-maxTiltAngle, maxTiltAngle);

        float elapsed = 0f;
        while (elapsed < fallDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / fallDuration);

            float gravityEase = t * t;

            transform.localPosition = Vector3.Lerp(startPos, targetPos, gravityEase);
            transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(0f, targetTilt, t));

            yield return null;
        }

        transform.localPosition = targetPos;
        transform.localRotation = Quaternion.Euler(0f, 0f, targetTilt);
    }
}