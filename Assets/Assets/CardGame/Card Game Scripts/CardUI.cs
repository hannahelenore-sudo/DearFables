using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

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
    public float animationSpeed = 0.15f;

    [Header("Card Tint Colors")]
    public Color cardDefaultTint = Color.white;
    public Color cardTintOnSelect = new Color(1f, 0.98f, 0.8f, 1f);

    [Header("Hover Settings")]
    public GameObject valueBubblePrefab;
    public float hoverOffsetY = 20f;
    public float hoverScaleBonus = 0.05f;

    [Header("Hover Bubble Customization")]
    public Vector2 bubbleOffset = new Vector2(0f, 130f);
    public Color bubbleBackgroundColor = new Color(0f, 0f, 0f, 0.85f);
    public Color bubbleTextColor = Color.yellow;

    private RectTransform rectTransform;
    private DeckManager deckManager;
    private Coroutine currentSelectionAnimation;

    private LayoutElement layoutElement;

    private bool isHovered = false;
    private GameObject activeValueBubble;
    private Image bubbleBgImage;
    private TextMeshProUGUI bubbleTextComponent;

    private float currentVisualOffsetY = 0f;

    public bool isAnimatingToLayout = false;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        if (cardImage == null) cardImage = GetComponent<Image>();
        layoutElement = GetComponent<LayoutElement>();
        if (layoutElement == null) layoutElement = gameObject.AddComponent<LayoutElement>();
        deckManager = Object.FindAnyObjectByType<DeckManager>();
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
        if (isAnimatingToLayout) return;

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

    void LateUpdate()
    {
        if (isAnimatingToLayout) return;

        float targetY = isSelected ? selectOffsetY : (isHovered && CanHover() ? hoverOffsetY : 0f);
        if (Mathf.Abs(rectTransform.anchoredPosition.y - targetY) > 0.01f && currentSelectionAnimation == null)
        {
            rectTransform.anchoredPosition = new Vector2(rectTransform.anchoredPosition.x, targetY);
        }
    }

    public void DeselectCard()
    {
        ToggleSelection(false);
    }

    public void LoadCardData(CardData newData)
    {
        cardData = newData;
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
            bubbleTextComponent.fontSize = 22;
            bubbleTextComponent.fontStyle = FontStyles.Bold;

            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
            textRect.anchoredPosition = Vector2.zero;
        }

        RectTransform bubbleRect = activeValueBubble.GetComponent<RectTransform>();
        if (bubbleRect != null)
        {
            bubbleRect.anchorMin = new Vector2(0.5f, 1f);
            bubbleRect.anchorMax = new Vector2(0.5f, 1f);
            bubbleRect.pivot = new Vector2(0.5f, 0.5f);
            bubbleRect.anchoredPosition = bubbleOffset;
            bubbleRect.sizeDelta = new Vector2(70f, 45f);
        }

        UpdateBubbleVisuals();
        activeValueBubble.SetActive(false);
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
            if (cardData != null)
            {
                bubbleTextComponent.text = cardData.primaryValue.ToString();
            }
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
            bubbleTextComponent.text = cardData.primaryValue.ToString();
        }
    }

    private bool CanHover()
    {
        if (isSelected || isAnimatingToLayout) return false;

        if (deckManager != null)
        {
            var scoringField = deckManager.GetType().GetField("isScoring", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var discardingField = deckManager.GetType().GetField("isDiscarding", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

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

        if (activeValueBubble != null)
        {
            UpdateBubbleVisuals();
            activeValueBubble.SetActive(true);
        }

        TriggerAnimation();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;

        if (activeValueBubble != null)
        {
            activeValueBubble.SetActive(false);
        }

        TriggerAnimation();
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
        isSelected = selected;

        if (isSelected)
        {
            isHovered = false;
            if (activeValueBubble != null)
            {
                activeValueBubble.SetActive(false);
            }
        }

        TriggerAnimation();

        if (deckManager != null)
        {
            deckManager.UpdateSelectedTotal();
        }
    }

    private void TriggerAnimation()
    {
        if (isAnimatingToLayout) return;

        if (currentSelectionAnimation != null)
            StopCoroutine(currentSelectionAnimation);

        currentSelectionAnimation = StartCoroutine(AnimateSelectionRoutine());
    }

    private IEnumerator AnimateSelectionRoutine()
    {
        float targetOffsetY = 0f;
        if (isSelected) targetOffsetY += selectOffsetY;
        else if (isHovered && CanHover()) targetOffsetY += hoverOffsetY;

        float totalScale = 1f;
        if (isSelected) totalScale *= selectScale;
        else if (isHovered && CanHover()) totalScale += hoverScaleBonus;

        Vector3 targetScale = Vector3.one * totalScale;
        Color targetColor = isSelected ? cardTintOnSelect : cardDefaultTint;

        float startOffsetY = currentVisualOffsetY;
        Vector3 startScale = transform.localScale;
        Color startColor = cardImage != null ? cardImage.color : Color.white;

        float elapsed = 0f;
        while (elapsed < animationSpeed)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Sin((elapsed / animationSpeed) * Mathf.PI * 0.5f);

            float nextOffsetY = Mathf.Lerp(startOffsetY, targetOffsetY, t);
            currentVisualOffsetY = nextOffsetY;

            rectTransform.anchoredPosition = new Vector2(rectTransform.anchoredPosition.x, currentVisualOffsetY);

            transform.localScale = Vector3.Lerp(startScale, targetScale, t);
            if (cardImage != null) cardImage.color = Color.Lerp(startColor, targetColor, t);

            yield return null;
        }

        currentVisualOffsetY = targetOffsetY;
        rectTransform.anchoredPosition = new Vector2(rectTransform.anchoredPosition.x, targetOffsetY);

        transform.localScale = targetScale;
        if (cardImage != null) cardImage.color = targetColor;

        if (!isSelected)
        {
            ResetBaselinePosition();
        }

        currentSelectionAnimation = null;
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

    public IEnumerator MoveToAnchoredPosition(Vector2 targetAnchoredPos, float duration)
    {
        isAnimatingToLayout = true;
        if (activeValueBubble != null) activeValueBubble.SetActive(false);

        Vector2 startPos = rectTransform.anchoredPosition;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            t = t * t * (3f - 2f * t);

            rectTransform.anchoredPosition = Vector2.Lerp(startPos, targetAnchoredPos, t);
            yield return null;
        }

        rectTransform.anchoredPosition = targetAnchoredPos;
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