using System.Collections;
using UnityEngine;
using TMPro;

public class StarCounterUI : MonoBehaviour
{
    [Header("References")]
    public TextMeshProUGUI starCountText;
    [Tooltip("Optional: the spinning star icon (the one with RotateStar on it) to pop when stars are gained.")]
    public Transform starIcon;

    [Header("Settings")]
    public string prefix = "x ";
    public float startDelay = 0.5f;
    public float countDuration = 0.8f;
    public float popScale = 1.4f;

    private void Start()
    {
        int total = PlayerProgress.TotalStars;
        int gained = PlayerProgress.LastGained;

        if (gained > 0)
        {
            SetText(total - gained);
            StartCoroutine(CountUp(total - gained, total));
            PlayerProgress.ClearLastGained();
        }
        else
        {
            SetText(total);
        }
    }

    private IEnumerator CountUp(int from, int to)
    {
        yield return new WaitForSeconds(startDelay);

        float elapsed = 0f;
        while (elapsed < countDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / countDuration);
            SetText(Mathf.RoundToInt(Mathf.Lerp(from, to, t)));
            yield return null;
        }
        SetText(to);

        if (starIcon != null)
            yield return StartCoroutine(Pop(starIcon));
    }

    private IEnumerator Pop(Transform target)
    {
        Vector3 original = target.localScale;
        float elapsed = 0f;
        const float duration = 0.3f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            target.localScale = original * Mathf.Lerp(1f, popScale, Mathf.Sin(t * Mathf.PI));
            yield return null;
        }
        target.localScale = original;
    }

    private void SetText(int value)
    {
        if (starCountText != null)
            starCountText.text = prefix + value;
    }
}