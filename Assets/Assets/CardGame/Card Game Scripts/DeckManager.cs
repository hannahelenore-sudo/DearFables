using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DeckManager : MonoBehaviour
{
    [Header("Deck Containers")]
    public List<CardData> drawPile = new List<CardData>();
    public List<CardData> hand = new List<CardData>();
    public List<CardData> discardPile = new List<CardData>();

    [Header("Deck & Hand Limits")]
    public int totalDeckCards = 10;
    public int maxHandSize = 7;
    public int maxDiscardsAllowed = 3;
    public int maxCardsToDiscard = 5;

    private int cardsRemainingInRound;
    private int remainingDiscards;

    [Header("UI Text Displays")]
    public TextMeshProUGUI discardsRemainingText;
    public TextMeshProUGUI handCountText;
    public TextMeshProUGUI selectedCardsText;
    public TextMeshProUGUI totalAmountOfCardsLeftText;

    [Header("Target Score Settings")]
    public int targetScore = 100;
    public int currentRemainingScore;
    public TextMeshProUGUI targetScoreText;
    public TextMeshProUGUI battleTargetScoreText;

    [Header("Manager References")]
    public BattleManager battleManager;

    [Header("Visual References")]
    public Transform handParentContainer;
    public GameObject cardPrefab;
    public TextMeshProUGUI totalValueText;
    public GameObject confirmButton;
    public GameObject discardButton;
    public GameObject backButton;

    [Header("Discard & Scoring Settings")]
    public float discardFlyDuration = 0.35f;
    public float delayBetweenDiscards = 0.15f;
    public float scoringCenterOffsetY = 200f;
    public float scoringCardSpacing = 300f;
    public float remainingHandOffsetY = -150f;
    public float remainingHandSpacing = 220f;

    [Header("Camera References")]
    public Camera gameCamera;
    public Camera menuCamera;

    private List<CardUI> spawnedCardUIList = new List<CardUI>();
    private bool isScoring = false;
    private bool isDiscarding = false;

    private Button confirmButtonComp;
    private Button discardButtonComp;

    void Awake()
    {
        currentRemainingScore = targetScore;
        if (battleManager == null)
        {
            battleManager = Object.FindAnyObjectByType<BattleManager>();
        }
    }

    void Start()
    {
        ResetRoundState();

        if (totalValueText != null) totalValueText.text = "";

        if (battleManager != null)
        {
            targetScore = battleManager.battleTargetScore;
        }
        currentRemainingScore = targetScore;
        UpdateTargetScoreDisplay();

        if (confirmButton != null)
        {
            confirmButton.SetActive(true);
            confirmButtonComp = confirmButton.GetComponent<Button>();
            if (confirmButtonComp != null)
            {
                confirmButtonComp.onClick.RemoveAllListeners();
                confirmButtonComp.onClick.AddListener(OnConfirmClicked);
                confirmButtonComp.interactable = false;
            }
        }

        if (discardButton != null)
        {
            discardButton.SetActive(true);
            discardButtonComp = discardButton.GetComponent<Button>();
            if (discardButtonComp != null)
            {
                discardButtonComp.onClick.RemoveAllListeners();
                discardButtonComp.onClick.AddListener(OnDiscardButtonClicked);
                discardButtonComp.interactable = false;
            }
        }

        if (backButton != null)
        {
            backButton.SetActive(true);
            Button btn = backButton.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(OnBackButtonClicked);
            }
        }
    }

    private void Update()
    {
        CheckAndUpdateUIState();
    }

    private void CheckAndUpdateUIState()
    {
        if (battleManager != null && battleManager.isOpeningSequenceActive)
        {
            if (battleTargetScoreText != null && battleTargetScoreText.gameObject.activeSelf)
                battleTargetScoreText.gameObject.SetActive(false);

            if (targetScoreText != null && targetScoreText.gameObject.activeSelf)
                targetScoreText.gameObject.SetActive(false);
        }
    }

    private void ResetRoundState()
    {
        // Only count available cards (excluding discardPile so discarded cards remain removed)
        cardsRemainingInRound = Mathf.Min(totalDeckCards, drawPile.Count + hand.Count);
        remainingDiscards = maxDiscardsAllowed;

        UpdateDiscardTextDisplay();
        UpdateHandCountDisplay();
        UpdateSelectedCountDisplay(0);
        UpdateDeckLeftDisplay();
        ShuffleDrawPile();
    }

    public void OpenCardGameView()
    {
        SetCameraState(inCardView: true);

        if (backButton != null)
        {
            backButton.SetActive(true);
        }

        if (hand.Count == 0 && cardsRemainingInRound > 0)
        {
            int drawAmount = Mathf.Min(maxHandSize, cardsRemainingInRound);
            DrawCards(drawAmount);
        }
        else if (spawnedCardUIList.Count == 0 && hand.Count > 0)
        {
            foreach (CardData cardData in hand)
            {
                CreateCardUI(cardData);
            }
            RebuildLayout();
        }
        else
        {
            RebuildLayout();
        }

        DeselectAllCards();
    }

    public void SetCameraState(bool inCardView)
    {
        if (gameCamera != null) gameCamera.gameObject.SetActive(!inCardView);
        if (menuCamera != null) menuCamera.gameObject.SetActive(inCardView);

        if (backButton != null) backButton.SetActive(inCardView);

        if (handParentContainer != null)
        {
            Canvas parentCanvas = handParentContainer.GetComponentInParent<Canvas>(true);
            if (parentCanvas != null)
            {
                parentCanvas.gameObject.SetActive(inCardView);
            }
        }
    }

    public void OnBackButtonClicked()
    {
        if (isScoring || isDiscarding) return;

        DeselectAllCards();
        SetCameraState(inCardView: false);
    }

    public void DeselectAllCards()
    {
        foreach (CardUI card in spawnedCardUIList)
        {
            if (card != null)
            {
                card.DeselectCard();
            }
        }
        UpdateSelectedTotal();
    }

    public void UpdateTargetScoreDisplay()
    {
        string scoreString = "Score To Beat:\n" + Mathf.Max(0, currentRemainingScore).ToString();

        if (targetScoreText != null && (battleManager == null || !battleManager.isOpeningSequenceActive))
        {
            targetScoreText.text = scoreString;
            targetScoreText.color = Color.black;
        }

        if (battleTargetScoreText != null && (battleManager == null || !battleManager.isOpeningSequenceActive))
        {
            battleTargetScoreText.text = scoreString;
        }

        if (battleManager != null)
        {
            battleManager.UpdateBattleTargetScoreDisplay(currentRemainingScore);
        }
    }

    private void UpdateDiscardTextDisplay()
    {
        if (discardsRemainingText != null)
            discardsRemainingText.text = "Discards: " + remainingDiscards.ToString() + "/" + maxDiscardsAllowed.ToString();
    }

    private void UpdateHandCountDisplay()
    {
        if (handCountText != null)
        {
            handCountText.text = hand.Count.ToString() + "/" + maxHandSize.ToString();
        }
    }

    private void UpdateSelectedCountDisplay(int count)
    {
        if (selectedCardsText != null)
        {
            selectedCardsText.text = count.ToString() + "/" + maxCardsToDiscard.ToString();
        }
    }

    private void UpdateDeckLeftDisplay()
    {
        if (totalAmountOfCardsLeftText != null)
        {
            totalAmountOfCardsLeftText.text = cardsRemainingInRound.ToString() + "/" + totalDeckCards.ToString();
        }
    }

    public void DrawCards(int count)
    {
        StartCoroutine(DrawCardsRoutine(count));
    }

    private IEnumerator DrawCardsRoutine(int count)
    {
        HorizontalLayoutGroup layoutGroup = handParentContainer.GetComponent<HorizontalLayoutGroup>();
        if (layoutGroup != null) layoutGroup.enabled = true;

        for (int i = 0; i < count; i++)
        {
            if (drawPile.Count == 0 || cardsRemainingInRound <= 0 || hand.Count >= maxHandSize)
                yield break;

            CardData drawnCard = drawPile[0];
            drawPile.RemoveAt(0);
            hand.Add(drawnCard);
            cardsRemainingInRound--;

            CreateCardUI(drawnCard);
            UpdateHandCountDisplay();
            UpdateDeckLeftDisplay();

            yield return new WaitForSeconds(0.12f);
        }

        RebuildLayout();
        UpdateSelectedTotal();
    }

    private void CreateCardUI(CardData data)
    {
        if (cardPrefab != null && handParentContainer != null)
        {
            GameObject newCardObj = Instantiate(cardPrefab, handParentContainer);
            CardUI cardUI = newCardObj.GetComponent<CardUI>();
            if (cardUI != null)
            {
                cardUI.LoadCardData(data);
                spawnedCardUIList.Add(cardUI);

                LayoutElement le = newCardObj.GetComponent<LayoutElement>();
                if (le != null) le.ignoreLayout = false;
            }
        }
    }

    private void RebuildLayout()
    {
        spawnedCardUIList.RemoveAll(item => item == null);

        HorizontalLayoutGroup layoutGroup = handParentContainer.GetComponent<HorizontalLayoutGroup>();
        if (layoutGroup != null) layoutGroup.enabled = true;

        foreach (CardUI card in spawnedCardUIList)
        {
            if (card != null)
            {
                if (card.transform.parent != handParentContainer) card.transform.SetParent(handParentContainer, false);
                LayoutElement le = card.GetComponent<LayoutElement>();
                if (le != null) le.ignoreLayout = false;
            }
        }

        Canvas.ForceUpdateCanvases();
        if (handParentContainer is RectTransform handRect) LayoutRebuilder.ForceRebuildLayoutImmediate(handRect);
    }

    public int GetSelectedCardCount()
    {
        int count = 0;
        foreach (CardUI cardUI in spawnedCardUIList)
        {
            if (cardUI != null && cardUI.isSelected) count++;
        }
        return count;
    }

    public void UpdateSelectedTotal()
    {
        if (isScoring || isDiscarding) return;

        int selectedCount = GetSelectedCardCount();
        UpdateSelectedCountDisplay(selectedCount);
        UpdateHandCountDisplay();

        if (confirmButtonComp != null)
            confirmButtonComp.interactable = (selectedCount >= 1 && selectedCount <= maxCardsToDiscard);

        if (discardButtonComp != null)
            discardButtonComp.interactable = (selectedCount >= 1 && selectedCount <= maxCardsToDiscard && remainingDiscards > 0);
    }

    public void OnConfirmClicked()
    {
        if (isScoring || isDiscarding) return;
        StartCoroutine(ScoreAndDiscardRoutine());
    }

    public void OnDiscardButtonClicked()
    {
        if (isScoring || isDiscarding || remainingDiscards <= 0) return;
        StartCoroutine(DiscardAndRedrawRoutine());
    }

    /// <summary>
    /// RECYCLES: Selected cards go back into the draw pile, get reshuffled, and replacement cards are drawn.
    /// </summary>
    private IEnumerator DiscardAndRedrawRoutine()
    {
        isDiscarding = true;
        remainingDiscards--;
        UpdateDiscardTextDisplay();

        if (confirmButtonComp != null) confirmButtonComp.interactable = false;
        if (discardButtonComp != null) discardButtonComp.interactable = false;

        List<CardUI> selectedCards = new List<CardUI>();
        List<CardUI> remainingCards = new List<CardUI>();

        foreach (CardUI cardUI in spawnedCardUIList)
        {
            if (cardUI != null)
            {
                if (cardUI.isSelected) selectedCards.Add(cardUI);
                else remainingCards.Add(cardUI);
            }
        }

        if (selectedCards.Count == 0)
        {
            isDiscarding = false;
            yield break;
        }

        foreach (CardUI card in selectedCards)
        {
            if (card != null)
            {
                LayoutElement le = card.GetComponent<LayoutElement>();
                if (le != null) le.ignoreLayout = true;
            }
        }

        float discardTargetY = -(Screen.height * 0.75f);
        foreach (CardUI card in selectedCards)
        {
            if (card != null)
            {
                RectTransform cardRect = card.GetComponent<RectTransform>();
                if (cardRect != null)
                {
                    Vector2 offscreenPos = new Vector2(cardRect.anchoredPosition.x, discardTargetY);
                    StartCoroutine(card.MoveToAnchoredPosition(offscreenPos, discardFlyDuration));
                }
            }
        }

        yield return new WaitForSeconds(discardFlyDuration + 0.05f);

        int recycledCount = selectedCards.Count;
        foreach (CardUI card in selectedCards)
        {
            if (card != null)
            {
                spawnedCardUIList.Remove(card);
                if (hand.Contains(card.cardData)) hand.Remove(card.cardData);

                // Return cards to draw pile instead of discard pile
                drawPile.Add(card.cardData);
                cardsRemainingInRound++;

                Destroy(card.gameObject);
            }
        }

        // Reshuffle draw pile with returned cards
        ShuffleDrawPile();

        spawnedCardUIList.RemoveAll(item => item == null);
        foreach (CardUI card in remainingCards)
        {
            if (card != null) card.DeselectCard();
        }

        RebuildLayout();
        UpdateHandCountDisplay();

        int cardsToDraw = Mathf.Min(recycledCount, cardsRemainingInRound);
        if (cardsToDraw > 0)
        {
            yield return StartCoroutine(DrawCardsRoutine(cardsToDraw));
        }

        isDiscarding = false;
        UpdateSelectedTotal();
    }

    /// <summary>
    /// PLAYS: Scores points, permanently moves played cards to discardPile, and returns to battle without recycling discarded cards.
    /// </summary>
    /// <summary>
    /// PLAYS: Scores points, permanently moves played cards to discardPile, and returns to battle without recycling discarded cards.
    /// </summary>
    private IEnumerator ScoreAndDiscardRoutine()
    {
        isScoring = true;

        if (confirmButtonComp != null) confirmButtonComp.interactable = false;
        if (discardButtonComp != null) discardButtonComp.interactable = false;

        List<CardUI> selectedCards = new List<CardUI>();
        List<CardUI> remainingCards = new List<CardUI>();

        foreach (CardUI cardUI in spawnedCardUIList)
        {
            if (cardUI != null)
            {
                if (cardUI.isSelected) selectedCards.Add(cardUI);
                else remainingCards.Add(cardUI);
            }
        }

        if (selectedCards.Count == 0)
        {
            isScoring = false;
            yield break;
        }

        HorizontalLayoutGroup layoutGroup = handParentContainer.GetComponent<HorizontalLayoutGroup>();
        if (layoutGroup != null) layoutGroup.enabled = false;

        foreach (CardUI card in spawnedCardUIList)
        {
            if (card != null)
            {
                LayoutElement layoutElement = card.GetComponent<LayoutElement>();
                if (layoutElement != null) layoutElement.ignoreLayout = true;
            }
        }

        // Animate unselected cards back
        if (remainingCards.Count > 0)
        {
            float handStartX = -((remainingCards.Count - 1) * remainingHandSpacing / 2f);

            for (int i = 0; i < remainingCards.Count; i++)
            {
                CardUI card = remainingCards[i];
                Vector2 newHandPos = new Vector2(handStartX + (i * remainingHandSpacing), remainingHandOffsetY);
                StartCoroutine(card.MoveToAnchoredPosition(newHandPos, 0.3f));
            }
        }

        // Animate selected cards to scoring center
        float startX = -((selectedCards.Count - 1) * scoringCardSpacing / 2f);

        for (int i = 0; i < selectedCards.Count; i++)
        {
            CardUI card = selectedCards[i];

            if (card.transform.parent != handParentContainer)
            {
                card.transform.SetParent(handParentContainer, true);
            }

            card.transform.SetAsLastSibling();

            Vector2 targetAnchoredPos = new Vector2(startX + (i * scoringCardSpacing), scoringCenterOffsetY);
            StartCoroutine(card.MoveToAnchoredPosition(targetAnchoredPos, 0.35f));
        }

        Canvas.ForceUpdateCanvases();
        yield return new WaitForSeconds(0.4f);

        // --- STEP 1: CALCULATE BASE POINTS ---
        int baseScore = 0;
        if (totalValueText != null) totalValueText.text = "0";

        foreach (CardUI card in selectedCards)
        {
            if (card.cardData != null)
            {
                Vector3 origScale = card.transform.localScale;
                card.transform.localScale = origScale * 1.25f;

                baseScore += card.cardData.primaryValue;
                if (totalValueText != null) totalValueText.text = baseScore.ToString();

                yield return new WaitForSeconds(0.15f);
                card.transform.localScale = origScale;
                yield return new WaitForSeconds(0.2f);
            }
        }

        // --- STEP 2: EXECUTE INDIVIDUAL CARD SCRIPT LOGIC ---
        int finalScore = baseScore;
        foreach (CardUI card in selectedCards)
        {
            if (card.cardData != null)
            {
                // Calls each card's specific script logic (e.g. SolarCard, WindCard, etc.)
                finalScore = card.cardData.EvaluateScore(finalScore, selectedCards);
            }
        }

        // Update score UI to show final calculated score
        if (totalValueText != null) totalValueText.text = finalScore.ToString();

        currentRemainingScore -= finalScore;
        UpdateTargetScoreDisplay();

        yield return new WaitForSeconds(0.4f);

        // --- STEP 3: DISCARD ANIMATIONS ---
        float discardTargetY = -(Screen.height * 0.75f);

        foreach (CardUI card in selectedCards)
        {
            if (card != null)
            {
                RectTransform cardRect = card.GetComponent<RectTransform>();
                if (cardRect != null)
                {
                    Vector2 offscreenPos = new Vector2(cardRect.anchoredPosition.x, discardTargetY);
                    StartCoroutine(card.MoveToAnchoredPosition(offscreenPos, discardFlyDuration));
                }
            }
        }

        yield return new WaitForSeconds(delayBetweenDiscards);

        foreach (CardUI card in remainingCards)
        {
            if (card != null)
            {
                RectTransform cardRect = card.GetComponent<RectTransform>();
                if (cardRect != null)
                {
                    Vector2 offscreenPos = new Vector2(cardRect.anchoredPosition.x, discardTargetY);
                    StartCoroutine(card.MoveToAnchoredPosition(offscreenPos, discardFlyDuration));
                }
            }
        }

        yield return new WaitForSeconds(discardFlyDuration + 0.05f);

        // --- STEP 4: CLEANUP & STATE RESET ---
        List<CardUI> allCards = new List<CardUI>(spawnedCardUIList);
        foreach (CardUI card in allCards)
        {
            if (card != null)
            {
                if (hand.Contains(card.cardData)) hand.Remove(card.cardData);
                // Played cards go to discardPile PERMANENTLY
                discardPile.Add(card.cardData);
                Destroy(card.gameObject);
            }
        }

        spawnedCardUIList.Clear();
        hand.Clear();

        if (layoutGroup != null) layoutGroup.enabled = true;

        // Reset state for new round WITHOUT recycling discard pile back to draw pile
        ResetRoundState();

        SetCameraState(inCardView: false);

        if (currentRemainingScore <= 0)
        {
            if (battleManager != null)
            {
                battleManager.TriggerVictoryCondition();
            }
        }
        else
        {
            if (battleManager != null)
            {
                battleManager.OnHandCompletedAndReturned();
            }
        }

        isScoring = false;
    }

    public void ShuffleDrawPile()
    {
        for (int i = 0; i < drawPile.Count; i++)
        {
            CardData temp = drawPile[i];
            int randomIndex = Random.Range(i, drawPile.Count);
            drawPile[i] = drawPile[randomIndex];
            drawPile[randomIndex] = temp;
        }
    }

    /// <summary>
    /// Resets full deck for match restart or new game.
    /// </summary>
    public void ResetFullDeckForNewGame()
    {
        if (discardPile.Count > 0)
        {
            drawPile.AddRange(discardPile);
            discardPile.Clear();
        }
        ResetRoundState();
    }
}