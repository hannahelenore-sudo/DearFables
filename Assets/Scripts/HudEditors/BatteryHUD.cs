using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// Battery icon + how many batteries you have. The number counts up/down and the icon bounces.
/// Build it with  Tools > HUD > Create Player HUD  and then just swap the icon sprite.
/// </summary>
public class BatteryHUD : MonoBehaviour
{
    public TMP_Text countText;
    [Tooltip("The battery icon. It bounces when you gain batteries.")]
    public RectTransform icon;

    [Header("Look")]
    public bool outlineText = true;
    public Color gainColor = new Color(0.4f, 1f, 0.4f);
    public Color spendColor = new Color(1f, 0.85f, 0.3f);
    public Color cantAffordColor = new Color(1f, 0.3f, 0.3f);
    [Tooltip("Seconds per +1 while the number counts.")]
    public float countStep = 0.04f;

    private static BatteryHUD instance;
    private int shown;
    private Color baseColor = Color.white;
    private Vector3 iconBaseScale = Vector3.one;
    private Coroutine countCo, flashCo, punchCo, shakeCo;

    private void Awake()
    {
        instance = this;
        if (countText != null)
        {
            baseColor = countText.color;
            if (outlineText)
            {
                countText.outlineWidth = 0.3f;
                countText.outlineColor = new Color32(0, 0, 0, 255);
            }
        }
        if (icon != null) iconBaseScale = icon.localScale;
    }

    private void OnEnable()
    {
        instance = this;
        BatteryManager.Changed += OnChanged;
        shown = BatteryManager.Count;
        SetText(shown);
    }

    private void OnDisable()
    {
        BatteryManager.Changed -= OnChanged;
        if (instance == this) instance = null;
    }

    private void OnChanged(int delta)
    {
        if (countCo != null) StopCoroutine(countCo);
        countCo = StartCoroutine(CountTo(BatteryManager.Count));

        Flash(delta > 0 ? gainColor : spendColor);
        if (delta > 0) Punch();
    }

    /// <summary>Flashes the number red and shakes the icon (the shop calls this when you can't afford something).</summary>
    public static void FlashCantAfford()
    {
        if (instance == null) return;
        instance.Flash(instance.cantAffordColor);
        instance.Shake();
    }

    private void SetText(int value)
    {
        if (countText != null) countText.text = value.ToString();
    }

    private IEnumerator CountTo(int target)
    {
        while (shown != target)
        {
            shown += shown < target ? 1 : -1;
            SetText(shown);
            yield return new WaitForSecondsRealtime(countStep);
        }
        countCo = null;
    }

    private void Flash(Color c)
    {
        if (countText == null || !isActiveAndEnabled) return;
        if (flashCo != null) StopCoroutine(flashCo);
        flashCo = StartCoroutine(FlashRoutine(c));
    }

    private IEnumerator FlashRoutine(Color c)
    {
        const float duration = 0.5f;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            countText.color = Color.Lerp(c, baseColor, Mathf.Clamp01(t / duration));
            yield return null;
        }
        countText.color = baseColor;
        flashCo = null;
    }

    private void Punch()
    {
        if (icon == null || !isActiveAndEnabled) return;
        if (punchCo != null) StopCoroutine(punchCo);
        punchCo = StartCoroutine(PunchRoutine());
    }

    private IEnumerator PunchRoutine()
    {
        const float duration = 0.3f;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float x = Mathf.Clamp01(t / duration);
            icon.localScale = iconBaseScale * (1f + 0.3f * Mathf.Sin(x * Mathf.PI));
            yield return null;
        }
        icon.localScale = iconBaseScale;
        punchCo = null;
    }

    private void Shake()
    {
        if (icon == null || !isActiveAndEnabled) return;
        if (shakeCo != null) StopCoroutine(shakeCo);
        shakeCo = StartCoroutine(ShakeRoutine());
    }

    private IEnumerator ShakeRoutine()
    {
        const float duration = 0.35f;
        Vector2 start = icon.anchoredPosition;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float x = Mathf.Clamp01(t / duration);
            icon.anchoredPosition = start + new Vector2(Mathf.Sin(x * Mathf.PI * 8f) * 8f * (1f - x), 0f);
            yield return null;
        }
        icon.anchoredPosition = start;
        shakeCo = null;
    }
}
