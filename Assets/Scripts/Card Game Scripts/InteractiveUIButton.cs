using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class InteractiveUIButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("Hover Settings")]
    [Tooltip("How much larger the button gets when hovered (1.1 = 10% bigger).")]
    public float hoverScaleMultiplier = 1.1f;
    public float hoverTransitionSpeed = 10f;

    [Header("Cannot Do / Wiggle Settings")]
    [Tooltip("Check this box in the Inspector if you want the button to play a 'cannot do' wiggle animation when clicked.")]
    public bool enableWiggleOnUnsupportedClick = true;
    public float wiggleAngle = 8f;
    public float wiggleDuration = 0.25f;

    private RectTransform rectTransform;
    private Vector3 originalScale;
    private Quaternion originalRotation;
    private bool isHovered = false;
    private Coroutine currentAnimationRoutine;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        originalScale = rectTransform.localScale;
        originalRotation = rectTransform.localRotation;
    }

    private void Update()
    {
        Vector3 targetScale = isHovered ? originalScale * hoverScaleMultiplier : originalScale;
        if (currentAnimationRoutine == null)
        {
            rectTransform.localScale = Vector3.Lerp(rectTransform.localScale, targetScale, Time.unscaledDeltaTime * hoverTransitionSpeed);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (enableWiggleOnUnsupportedClick)
        {
            if (currentAnimationRoutine != null)
            {
                StopCoroutine(currentAnimationRoutine);
            }
            currentAnimationRoutine = StartCoroutine(WiggleRoutine());
        }
    }

    private IEnumerator WiggleRoutine()
    {
        float elapsed = 0f;
        Vector3 baseScale = isHovered ? originalScale * hoverScaleMultiplier : originalScale;

        while (elapsed < wiggleDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / wiggleDuration);

            float currentWiggle = Mathf.Sin(progress * Mathf.PI * 6f) * wiggleAngle * (1f - progress);

            rectTransform.localRotation = Quaternion.Euler(0, 0, currentWiggle);
            yield return null;
        }

        rectTransform.localRotation = originalRotation;
        currentAnimationRoutine = null;
    }
}