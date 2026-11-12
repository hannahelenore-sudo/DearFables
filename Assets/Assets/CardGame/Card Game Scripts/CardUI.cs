using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// One card in the hand. DeckManager decides WHERE the card belongs (slotPosition / slotRotation);
/// this script makes it glide there with a springy, Balatro-style feel, and adds the
/// lift / scale / tilt for hover and selection on top of that slot.
/// </summary>
public class CardUI : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Data Reference")]
    public CardData cardData;

    [Header("Visual Reference")]
    public Image cardImage;

    [Header("Selection Settings")]
    public bool isSelected = false;
    public float selectOffsetY = 40f;
    public float selectScale = 1.08f;

    [Header("Card Tint Colors")]
    public Color cardDefaultTint = Color.white;
    public Color cardTintOnSelect = new Color(1f, 0.98f, 0.8f, 1f);
    [Tooltip("How fast the tint fades between normal and selected.")]
    public float tintSpeed = 14f;

    [Header("Balatro Motion")]
    [Tooltip("Higher = snappier. Lower = floatier.")]
    public float springStiffness = 320f;
    [Tooltip("Higher = less wobble. ~24 gives a small, pleasant overshoot.")]
    public float springDamping = 24f;
    [Tooltip("Little bounce kick when a card is selected.")]
    public float selectPopKick = 2f;
    [Tooltip("How far (degrees) a hovered card tilts toward the mouse.")]
    public float hoverTiltDegrees = 7f;

    [Header("Hover Settings")]
    public GameObject valueBubblePrefab;
    public float hoverOffsetY = 20f;
    public float hoverScaleBonus = 0.05f;

    [Header("Hover Bubble Customization")]
    public Vector2 bubbleOffset = new Vector2(0f, 35f); // Lowered closer to the card
    public Color bubbleBackgroundColor = new Color(0f, 0f, 0f, 0.85f);
    public Color bubbleTextColor = Color.yellow;

    // ---- set by DeckManager.RebuildLayout ----
    [HideInInspector] public bool followSlot = false;
    [HideInInspector] public Vector2 slotPosition;
    [HideInInspector] public float slotRotation;

    private RectTransform rectTransform;
    private DeckManager deckManager;
    private LayoutElement layoutElement;
    private Canvas parentCanvas;

    private bool isHovered = false;
    private int siblingBeforeHover = -1;
    private GameObject activeValueBubble;
    private Image bubbleBgImage;
    private TextMeshProUGUI bubbleTextComponent;
    private Image bubbleIconImage;

    // spring state
    private Vector2 posVel;
    private float scaleVel, rotVel;
    private float curScale = 1f, curRot = 0f;

    private float currentVisualOffsetY = 0f; // legacy (AnimateToLayout)
    public bool isAnimatingToLayout = false;

    private static System.Reflection.FieldInfo scoringField;
    private static System.Reflection.FieldInfo discardingField;

    void Awake()
    {
        EnsureRefs();
        layoutElement = GetComponent<LayoutElement>();
        if (layoutElement == null) layoutElement = gameObject.AddComponent<LayoutElement>();
        deckManager = Object.FindAnyObjectByType<DeckManager>();
    }

    private void EnsureRefs()
    {
        if (rectTransform == null) rectTransform = GetComponent<RectTransform>();
        if (cardImage == null) cardImage = GetComponent<Image>();
    }

    void Start()
    {
        CreateHoverBubbleUI();
        StartCoroutine(InitializeSelectionState());
    }

    private IEnumerator InitializeSelectionState()
    {
        yield return new WaitForEndOfFrame();
        ApplySelectionStateImmediately();
    }

    public void ApplySelectionStateImmediately()
    {
        if (isAnimatingToLayout || followSlot) return; // slot-following cards handle this every frame

        if (isSelected)
        {
            currentVisualOffsetY = selectOffsetY;
            if (rectTransform != null)
            {
                rectTransform.anchoredPosition = new Vector2(rectTransform.anchoredPosition.x, selectOffsetY);
            }
            transform.localScale = Vector3.one * selectScale;
            if (cardImage != null)
            {
                cardImage.color = cardTintOnSelect;
            }
        }
        else
        {
            ResetBaselinePosition();
        }
    }

    // ------------------------------------------------------------------
    // Spring motion toward the slot (runs every frame)
    // ------------------------------------------------------------------

    void LateUpdate()
    {
        UpdateBubbleOrientation();

        if (!followSlot || isAnimatingToLayout) return;
        EnsureRefs();

        float dt = Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);

        bool hoverLift = isHovered && !isSelected && CanHover();

        float lift = isSelected ? selectOffsetY : (hoverLift ? hoverOffsetY : 0f);
        float targetScale = isSelected ? selectScale : (hoverLift ? 1f + hoverScaleBonus : 1f);
        float targetRot = slotRotation + (hoverLift ? GetPointerTilt() : 0f);
        Vector2 targetPos = slotPosition + new Vector2(0f, lift);

        // position
        Vector2 pos = rectTransform.anchoredPosition;
        posVel += ((targetPos - pos) * springStiffness - posVel * springDamping) * dt;
        pos += posVel * dt;
        rectTransform.anchoredPosition = pos;

        // scale
        scaleVel += ((targetScale - curScale) * springStiffness - scaleVel * springDamping) * dt;
        curScale += scaleVel * dt;
        transform.localScale = Vector3.one * curScale;

        // rotation
        rotVel += ((targetRot - curRot) * springStiffness - rotVel * springDamping) * dt;
        curRot += rotVel * dt;
        rectTransform.localRotation = Quaternion.Euler(0f, 0f, curRot);

        // tint
        if (cardImage != null)
        {
            Color targetColor = isSelected ? cardTintOnSelect : cardDefaultTint;
            cardImage.color = Color.Lerp(cardImage.color, targetColor, 1f - Mathf.Exp(-tintSpeed * dt));
        }
    }

    private float GetPointerTilt()
    {
        if (parentCanvas == null) parentCanvas = GetComponentInParent<Canvas>();
        Camera cam = (parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay) ? parentCanvas.worldCamera : null;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, Input.mousePosition, cam, out Vector2 local))
        {
            float half = Mathf.Max(1f, rectTransform.rect.width * 0.5f);
            return -Mathf.Clamp(local.x / half, -1f, 1f) * hoverTiltDegrees;
        }
        return 0f;
    }

    /// <summary>Instantly places the card (used for spawning a card at the deck before it flies into the hand).</summary>
    public void SnapTo(Vector2 pos, float scale, float rotZ)
    {
        EnsureRefs();
        posVel = Vector2.zero;
        scaleVel = 0f;
        rotVel = 0f;
        curScale = scale;
        curRot = rotZ;

        rectTransform.anchoredPosition = pos;
        transform.localScale = Vector3.one * scale;
        rectTransform.localRotation = Quaternion.Euler(0f, 0f, rotZ);
        if (cardImage != null) cardImage.color = isSelected ? cardTintOnSelect : cardDefaultTint;
    }

    public void SnapToSlot()
    {
        SnapTo(slotPosition, 1f, slotRotation);
    }

    /// <summary>
    /// Call after something else (dragging) moved the card, right before followSlot is turned back on.
    /// The spring continues from where the card really is, so it glides into its slot instead of jumping.
    /// </summary>
    public void SyncSpringState()
    {
        EnsureRefs();
        posVel = Vector2.zero;
        scaleVel = 0f;
        rotVel = 0f;
        curScale = transform.localScale.x;
        curRot = Mathf.DeltaAngle(0f, rectTransform.localEulerAngles.z);
    }

    public void DeselectCard()
    {
        ToggleSelection(false);
    }

    public void LoadCardData(CardData newData)
    {
        cardData = newData;
        EnsureRefs();
        if (cardImage != null && cardData != null && cardData.cardArt != null)
        {
            cardImage.sprite = cardData.cardArt;
        }

        UpdateBubbleText();
        StartCoroutine(InitializeSelectionState());
    }

    private void CreateHoverBubbleUI()
    {
        if (valueBubblePrefab != null)
        {
            activeValueBubble = Instantiate(valueBubblePrefab, transform);
            bubbleBgImage = activeValueBubble.GetComponent<Image>();
            bubbleTextComponent = activeValueBubble.GetComponentInChildren<TextMeshProUGUI>();
            bubbleIconImage = activeValueBubble.GetComponentInChildren<Image>();
        }
        else
        {
            activeValueBubble = new GameObject("HoverValueBubble", typeof(RectTransform));
            activeValueBubble.transform.SetParent(transform, false);

            bubbleBgImage = activeValueBubble.AddComponent<Image>();

            GameObject textObj = new GameObject("BubbleText", typeof(RectTransform));
            textObj.transform.SetParent(activeValueBubble.transform, false);

            bubbleTextComponent = textObj.AddComponent<TextMeshProUGUI>();
            bubbleTextComponent.alignment = TextAlignmentOptions.Center;
            bubbleTextComponent.fontSize = 20;
            bubbleTextComponent.fontStyle = FontStyles.Bold;

            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
            textRect.anchoredPosition = Vector2.zero;

            GameObject iconObj = new GameObject("BubbleIcon", typeof(RectTransform));
            iconObj.transform.SetParent(activeValueBubble.transform, false);
            bubbleIconImage = iconObj.AddComponent<Image>();
            bubbleIconImage.gameObject.SetActive(false);
        }

        RectTransform bubbleRect = activeValueBubble.GetComponent<RectTransform>();
        if (bubbleRect != null)
        {
            bubbleRect.anchorMin = new Vector2(0.5f, 1f);
            bubbleRect.anchorMax = new Vector2(0.5f, 1f);
            bubbleRect.pivot = new Vector2(0.5f, 0.5f);
            bubbleRect.anchoredPosition = bubbleOffset;
            bubbleRect.sizeDelta = new Vector2(220f, 95f);
        }

        UpdateBubbleVisuals();
        activeValueBubble.SetActive(false);
    }

    // Keeps the tooltip upright while the card tilts
    private void UpdateBubbleOrientation()
    {
        if (activeValueBubble != null && activeValueBubble.activeSelf)
        {
            activeValueBubble.transform.rotation = transform.parent != null ? transform.parent.rotation : Quaternion.identity;
        }
    }

    private void UpdateBubbleVisuals()
    {
        if (bubbleBgImage != null)
        {
            bubbleBgImage.color = bubbleBackgroundColor;
        }

        if (bubbleTextComponent != null)
        {
            bubbleTextComponent.color = bubbleTextColor;
            UpdateBubbleText();
        }

        if (bubbleIconImage != null)
        {
            bubbleIconImage.gameObject.SetActive(false);
        }

        if (activeValueBubble != null)
        {
            RectTransform bubbleRect = activeValueBubble.GetComponent<RectTransform>();
            if (bubbleRect != null)
            {
                bubbleRect.anchoredPosition = bubbleOffset;
            }
        }
    }

    private void UpdateBubbleText()
    {
        if (bubbleTextComponent != null && cardData != null)
        {
            if (cardData is SolarCard solar)
            {
                bubbleTextComponent.text = $"{solar.GetEffectiveValue()}\n<size=85%>(X1.5 if 5 solar cards are selected)</size>";
            }
            else if (cardData is WindCard wind)
            {
                int boost = wind.GetEffectiveValue() - wind.primaryValue; // Wind Power boon + mood Power
                if (boost > 0)
                    bubbleTextComponent.text = $"{wind.GetEffectiveValue()}\n<size=85%>({wind.primaryValue} base +{boost} Wind Power)</size>";
                else
                    bubbleTextComponent.text = wind.primaryValue.ToString();
            }
            else
            {
                bubbleTextComponent.text = cardData.GetEffectiveValue().ToString();
            }
        }
    }

    private bool CanHover()
    {
        if (isSelected || isAnimatingToLayout) return false;

        if (deckManager != null)
        {
            if (scoringField == null)
                scoringField = typeof(DeckManager).GetField("isScoring", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (discardingField == null)
                discardingField = typeof(DeckManager).GetField("isDiscarding", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            bool isScoring = scoringField != null && (bool)scoringField.GetValue(deckManager);
            bool isDiscarding = discardingField != null && (bool)discardingField.GetValue(deckManager);

            if (isScoring || isDiscarding) return false;
        }

        return true;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!CanHover()) return;

        isHovered = true;
        scaleVel += selectPopKick * 0.4f; // tiny bounce on hover

        // Draw the hovered card on top of its overlapping neighbours
        siblingBeforeHover = transform.GetSiblingIndex();
        transform.SetAsLastSibling();

        if (activeValueBubble != null)
        {
            UpdateBubbleVisuals();
            activeValueBubble.SetActive(true);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
        RestoreSiblingOrder();

        if (activeValueBubble != null)
        {
            activeValueBubble.SetActive(false);
        }
    }

    private void RestoreSiblingOrder()
    {
        if (siblingBeforeHover >= 0 && transform.parent != null)
        {
            transform.SetSiblingIndex(Mathf.Min(siblingBeforeHover, transform.parent.childCount - 1));
        }
        siblingBeforeHover = -1;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (deckManager == null || isAnimatingToLayout) return;
        if (!CanHover() && !isSelected) return;

        if (!isSelected && deckManager.GetSelectedCardCount() >= 5) return;

        ToggleSelection(!isSelected);
    }

    public void ToggleSelection(bool selected)
    {
        bool changed = isSelected != selected;
        isSelected = selected;

        if (isSelected)
        {
            isHovered = false;
            RestoreSiblingOrder();
            if (activeValueBubble != null)
            {
                activeValueBubble.SetActive(false);
            }
        }

        // Balatro-style "pop": a little kick of scale and wobble when the selection changes
        if (changed && followSlot && !isAnimatingToLayout)
        {
            scaleVel += isSelected ? selectPopKick : -selectPopKick * 0.5f;
            rotVel += Random.Range(-60f, 60f);
        }

        if (deckManager != null)
        {
            deckManager.UpdateSelectedTotal();
        }
    }

    public void ResetBaselinePosition()
    {
        currentVisualOffsetY = 0f;
        if (rectTransform != null)
        {
            rectTransform.anchoredPosition = new Vector2(rectTransform.anchoredPosition.x, 0f);
        }
        transform.localScale = Vector3.one;
        if (cardImage != null) cardImage.color = cardDefaultTint;
    }

    /// <summary>
    /// Smoothly flies the card to a position (used for scoring and discarding). Optionally shrinks / rotates it on the way.
    /// While this runs the card stops following its hand slot.
    /// </summary>
    public IEnumerator MoveToAnchoredPosition(Vector2 targetAnchoredPos, float duration, float endScale = 1f, float endRotation = 0f)
    {
        isAnimatingToLayout = true;
        if (activeValueBubble != null) activeValueBubble.SetActive(false);
        EnsureRefs();

        Vector2 startPos = rectTransform.anchoredPosition;
        float startScale = transform.localScale.x;
        float startRot = Mathf.DeltaAngle(0f, rectTransform.localEulerAngles.z);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = t * t * (3f - 2f * t);

            rectTransform.anchoredPosition = Vector2.Lerp(startPos, targetAnchoredPos, t);
            transform.localScale = Vector3.one * Mathf.Lerp(startScale, endScale, t);
            rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(startRot, endRotation, t));
            yield return null;
        }

        rectTransform.anchoredPosition = targetAnchoredPos;
        transform.localScale = Vector3.one * endScale;
        rectTransform.localRotation = Quaternion.Euler(0f, 0f, endRotation);
    }

    public void AnimateToLayout(Vector3 spawnPosition, float duration = 0.35f)
    {
        StartCoroutine(AnimateRoutine(spawnPosition, duration));
    }

    private IEnumerator AnimateRoutine(Vector3 startPos, float duration)
    {
        isAnimatingToLayout = true;
        layoutElement.ignoreLayout = true;
        Canvas.ForceUpdateCanvases();
        Vector3 targetPos = transform.position;

        transform.position = startPos;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            t = t * t * (3f - 2f * t);

            transform.position = Vector3.Lerp(startPos, targetPos, t);
            yield return null;
        }

        transform.position = targetPos;
        layoutElement.ignoreLayout = false;
        currentVisualOffsetY = 0f;
        Canvas.ForceUpdateCanvases();

        isAnimatingToLayout = false;
        ApplySelectionStateImmediately();
    }
}