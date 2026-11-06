using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class FadeIn : MonoBehaviour
{
    [Header("Fade Settings")]
    public float fadeDuration = 2f;
    [Tooltip("Minimum time to stay on black before fading.")]
    public float minHoldTime = 0.5f;
    [Tooltip("Never wait longer than this for the scene to settle.")]
    public float maxHoldTime = 3f;

    [Header("Options")]
    public Color fadeColor = Color.black;
    public bool disableWhenDone = true;

    private Image image;

    private void Awake()
    {
        image = GetComponent<Image>();
        SetAlpha(1f);
        image.raycastTarget = true;
    }

    private IEnumerator Start()
    {
        // Stay black while the scene finishes spawning and loading
        float held = 0f;
        int smoothFrames = 0;

        while (held < maxHoldTime)
        {
            float dt = Time.unscaledDeltaTime;
            held += dt;

            // Count consecutive frames that are fast (under ~25ms = 40fps+)
            smoothFrames = dt < 0.025f ? smoothFrames + 1 : 0;

            if (held >= minHoldTime && smoothFrames >= 10)
                break;

            yield return null;
        }

        // Fade using a smootherstep curve (softer start and end than linear)
        float timer = 0f;
        while (timer < fadeDuration)
        {
            timer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(timer / fadeDuration);
            float eased = t * t * t * (t * (t * 6f - 15f) + 10f);
            SetAlpha(1f - eased);
            yield return null;
        }

        SetAlpha(0f);
        image.raycastTarget = false;

        if (disableWhenDone)
            gameObject.SetActive(false);
    }

    private void SetAlpha(float alpha)
    {
        Color c = fadeColor;
        c.a = alpha;
        image.color = c;
    }
}