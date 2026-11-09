using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// One clickable boon card. Put this on a card object that has a background Image
/// (a Button is added automatically if missing). BoonSelectionUI drives it.
/// </summary>
public class BoonCardUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("References")]
    [Tooltip("The card's background image (assign any sprite you like).")]
    public Image cardBackground;
    [Tooltip("Where the boon's icon is shown.")]
    public Image iconImage;
    public TMP_Text nameText;
    public TMP_Text descriptionText;

    [Header("Hover")]
    public float hoverScale = 1.07f;
    public float hoverSpeed = 12f;

    public BoonData Boon { get; private set; }

    public Action<BoonCardUI> onClicked;
    public Action<BoonCardUI> onHovered;

    private Button button;
    private CanvasGroup group;
    private Vector3 baseScale = Vector3.one;
    private bool interactable;
    private bool hovered;
    private bool busy;

    private void Awake()
    {
        baseScale = transform.localScale;

        group = GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();

        button = GetComponent<Button>();
        if (button == null)
        {
            button = gameObject.AddComponent<Button>();
            if (cardBackground != null) button.targetGraphic = cardBackground;
        }

        button.onClick.RemoveListener(HandleClick);
        button.onClick.AddListener(HandleClick);
    }

    private void OnDestroy()
    {
        if (button != null) button.onClick.RemoveListener(HandleClick);
    }

    private void Update()
    {
        if (busy) return;

        Vector3 target = (interactable && hovered) ? baseScale * hoverScale : baseScale;
        float t = 1f - Mathf.Exp(-hoverSpeed * Time.unscaledDeltaTime);
        transform.localScale = Vector3.Lerp(transform.localScale, target, t);
    }

    public void Show(BoonData boon)
    {
        Boon = boon;

        if (nameText != null) nameText.text = boon.boonName;
        if (descriptionText != null) descriptionText.text = boon.GetDescription();

        if (iconImage != null)
        {
            iconImage.sprite = boon.icon;
            iconImage.enabled = boon.icon != null;
        }
    }

    public void SetInteractable(bool value)
    {
        interactable = value;
        if (button != null) button.interactable = value;
        if (!value) hovered = false;
    }

    /// <summary>Hides the card (scale 0) so it can pop in.</summary>
    public void PrepareHidden()
    {
        busy = true;
        hovered = false;
        if (group != null) group.alpha = 1f;
        transform.localScale = Vector3.zero;
    }

    public IEnumerator PlayAppear(float duration)
    {
        busy = true;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            // ease out back: small overshoot, then settle
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float eased = 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);

            transform.localScale = baseScale * Mathf.Max(0f, eased);
            yield return null;
        }

        transform.localScale = baseScale;
        busy = false;
    }

    /// <summary>Played on the card the player picked.</summary>
    public IEnumerator PlayChosen()
    {
        busy = true;
        Vector3 start = transform.localScale;
        Vector3 peak = baseScale * 1.15f;

        float elapsed = 0f;
        float duration = 0.15f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            transform.localScale = Vector3.Lerp(start, peak, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        elapsed = 0f;
        Vector3 settle = baseScale * 1.05f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            transform.localScale = Vector3.Lerp(peak, settle, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        transform.localScale = settle;
    }

    /// <summary>Played on the cards that were not picked.</summary>
    public IEnumerator PlayDismissed(float duration)
    {
        busy = true;
        Vector3 start = transform.localScale;
        Vector3 end = baseScale * 0.85f;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            transform.localScale = Vector3.Lerp(start, end, t);
            if (group != null) group.alpha = 1f - t;
            yield return null;
        }

        if (group != null) group.alpha = 0f;
    }

    private void HandleClick()
    {
        if (!interactable) return;
        onClicked?.Invoke(this);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovered = true;
        if (interactable) onHovered?.Invoke(this);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
    }
}
