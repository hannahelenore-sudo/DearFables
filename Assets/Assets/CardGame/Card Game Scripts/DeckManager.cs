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
    [Tooltip("Text object shown at the top of the card game screen when a roll bonus/penalty is active.")]
    public TextMeshProUGUI cardGameNotificationText;

    [Header("Target Score Settings")]
    public int targetScore = 100;
    public int currentRemainingScore;
    public TextMeshProUGUI targetScoreText;
    public TextMeshProUGUI battleTargetScoreText;

    [Header("Scoring Animation Settings")]
    [Tooltip("Choose how many seconds the score countdown and shake animation take.")]
    public float scoreCountdownDuration = 1.0f;
    [Tooltip("Delay in seconds before switching cameras after score calculation and pop animation.")]
    public float cameraSwitchDelay = 0.6f;

    [Header("Effect References")]
    [Tooltip("Attach a ScoreShakeEffect component here to shake/tint a sprite during scoring.")]
    public ScoreShakeEffect scoreShakeEffect;

    [Header("Manager References")]
    public BattleManager battleManager;

    [Header("Visual References")]
    public Transform handParentContainer;
    public GameObject cardPrefab;
    public TextMeshProUGUI totalValueText;
    public TextMeshProUGUI balatroScoreText;
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

    [Header("Balatro Score Display")]
    public Color chipsColor = new Color(0.30f, 0.65f, 1f);
    public Color multColor = new Color(1f, 0.30f, 0.30f);
    [Tooltip("ON: the high roll bonus MULTIPLIES the current mult (Balatro-style X mult). OFF: adds on top like the old behaviour.")]
    public bool rollBonusIsMultiplicative = true;
    [Tooltip("How big the score text pops when chips or mult change.")]
    public float scorePulseScale = 1.3f;

    [Header("Sound Effects")]
    [Range(0f, 1f)] public float sfxVolume = 1f;
    public AudioClip openCardViewSound;
    public AudioClip backButtonSound;
    public AudioClip drawCardSound;
    public AudioClip cardSelectSound;
    public AudioClip cardDeselectSound;
    public AudioClip discardSound;
    public AudioClip confirmSound;
    public AudioClip cardSlideSound;
    [Tooltip("Played for every scored card. The pitch rises with each card (Balatro style).")]
    public AudioClip cardScoreSound;
    [Tooltip("Fallback used when the Solar/Wind card asset has no Bonus Sound of its own.")]
    public AudioClip setBonusSound;
    public AudioClip highRollBonusSound;
    public AudioClip lowRollPenaltySound;
    public AudioClip finalScoreSound;
    public AudioClip cameraSwitchSound;
    public AudioClip countdownTickSound;
    public float countdownTickInterval = 0.06f;
    public AudioClip countdownEndSound;

    private List<CardUI> spawnedCardUIList = new List<CardUI>();
    private bool isScoring = false;
    private bool isDiscarding = false;

    private Button confirmButtonComp;
    private Button discardButtonComp;

    private Transform originalTotalValueParent;

    private int lastSelectedCount = 0;
    private bool suppressSelectSounds = false;
    private Vector3 balatroBaseScale = Vector3.one;

    void Awake()
    {
        if (balatroScoreText != null)
        {
            balatroBaseScale = balatroScoreText.transform.localScale;
        }

        currentRemainingScore = targetScore;
        if (battleManager == null)
        {
            battleManager = Object.FindAnyObjectByType<BattleManager>();
        }

        if (totalValueText != null)
        {
            originalTotalValueParent = totalValueText.transform.parent;
        }
    }

    void Start()
    {
        ResetRoundState();

        if (totalValueText != null) totalValueText.text = "";

        if (balatroScoreText != null)
        {
            SetBalatroText(1, 1f);
            balatroScoreText.gameObject.SetActive(true);
        }

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
        lastSelectedCount = 0;
        cardsRemainingInRound = Mathf.Min(totalDeckCards, drawPile.Count + hand.Count);
        remainingDiscards = maxDiscardsAllowed;

        UpdateDiscardTextDisplay();
        UpdateHandCountDisplay();
        UpdateSelectedCountDisplay(0);
        UpdateDeckLeftDisplay();
        ShuffleDrawPile();

        if (balatroScoreText != null)
        {
            SetBalatroText(1, 1f);
            balatroScoreText.gameObject.SetActive(true);
        }
    }

    public void OpenCardGameView()
    {
        SetCameraState(inCardView: true);
        PlaySFX(openCardViewSound);

        if (backButton != null)
        {
            backButton.SetActive(true);
        }

        if (cardGameNotificationText != null)
        {
            if (PlayerRollUI.isHighRollActive)
            {
                cardGameNotificationText.text = "X1.5 on this round";
                cardGameNotificationText.gameObject.SetActive(true);
            }
            else if (PlayerRollUI.isLowRollActive)
            {
                cardGameNotificationText.text = "-10% on the total score";
                cardGameNotificationText.gameObject.SetActive(true);
            }
            else
            {
                cardGameNotificationText.text = "";
                cardGameNotificationText.gameObject.SetActive(false);
            }
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

        PlaySFX(backButtonSound);
        DeselectAllCards();
        SetCameraState(inCardView: false);
    }

    public void DeselectAllCards()
    {
        suppressSelectSounds = true;

        foreach (CardUI card in spawnedCardUIList)
        {
            if (card != null)
            {
                card.DeselectCard();
            }
        }
        UpdateSelectedTotal();

        suppressSelectSounds = false;
    }

    public void UpdateTargetScoreDisplay()
    {
        string scoreString = Mathf.Max(0, currentRemainingScore).ToString();

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
            PlaySFX(drawCardSound, Random.Range(0.95f, 1.08f));
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

        if (!suppressSelectSounds)
        {
            if (selectedCount > lastSelectedCount) PlaySFX(cardSelectSound);
            else if (selectedCount < lastSelectedCount) PlaySFX(cardDeselectSound);
        }
        lastSelectedCount = selectedCount;

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

        PlaySFX(discardSound);
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

                drawPile.Add(card.cardData);
                cardsRemainingInRound++;

                Destroy(card.gameObject);
            }
        }

        ShuffleDrawPile();

        spawnedCardUIList.RemoveAll(item => item == null);
        foreach (CardUI card in remainingCards)
        {
            if (card != null) card.DeselectCard();
        }
        lastSelectedCount = 0;

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

    private IEnumerator ScoreAndDiscardRoutine()
    {
        isScoring = true;

        if (confirmButtonComp != null) confirmButtonComp.interactable = false;
        if (discardButtonComp != null) discardButtonComp.interactable = false;

        // Prevent button interactions on Battle View during scoring and transition animations
        if (battleManager != null)
        {
            battleManager.SetBattleUIInteractable(false);
        }

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
            if (battleManager != null) battleManager.SetBattleUIInteractable(true);
            yield break;
        }

        PlaySFX(confirmSound);

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

        PlaySFX(cardSlideSound);
        Canvas.ForceUpdateCanvases();
        yield return new WaitForSeconds(0.4f);

        // --- STEP 1: ADD UP CHIPS CARD BY CARD (mult stays at 1 until the effects trigger, like Balatro) ---
        float setMult = 1f;
        bool hasSetBonus = false;
        AudioClip setBonusClip = null;
        float setBonusVolume = 1f;

        if (selectedCards.Count > 0 && selectedCards[0].cardData != null)
        {
            CardData firstData = selectedCards[0].cardData;

            if (firstData is SolarCard solar)
            {
                if (CountCardsOfType<SolarCard>(selectedCards) >= solar.requiredCards)
                {
                    setMult = solar.multiplier;
                    hasSetBonus = true;
                    setBonusClip = solar.bonusSound;
                    setBonusVolume = solar.bonusVolume;
                }
            }
            else if (firstData is WindCard wind)
            {
                if (CountCardsOfType<WindCard>(selectedCards) >= wind.requiredCards)
                {
                    setMult = wind.multiplier;
                    hasSetBonus = true;
                    setBonusClip = wind.bonusSound;
                    setBonusVolume = wind.bonusVolume;
                }
            }
        }

        int baseScore = 0;
        float currentMult = 1f;

        if (balatroScoreText != null)
        {
            balatroScoreText.gameObject.SetActive(true);
            SetBalatroText(0, currentMult);
        }

        int scoredIndex = 0;
        foreach (CardUI card in selectedCards)
        {
            if (card.cardData != null)
            {
                Vector3 origScale = card.transform.localScale;
                card.transform.localScale = origScale * 1.25f;

                baseScore += card.cardData.primaryValue;
                SetBalatroText(baseScore, currentMult);
                StartCoroutine(PulseBalatroText());

                // Pitch climbs with every scored card
                PlaySFX(cardScoreSound, Mathf.Min(1f + scoredIndex * 0.08f, 2f));
                scoredIndex++;

                yield return new WaitForSeconds(0.15f);
                card.transform.localScale = origScale;
                yield return new WaitForSeconds(0.2f);
            }
        }

        yield return new WaitForSeconds(0.3f);

        // --- STEP 2: MULT EFFECTS TRIGGER ONE AFTER ANOTHER ---
        if (hasSetBonus)
        {
            currentMult *= setMult;
            SetBalatroText(baseScore, currentMult);
            StartCoroutine(PulseBalatroText());
            PlaySFX(setBonusClip != null ? setBonusClip : setBonusSound, 1f, setBonusVolume);
            yield return new WaitForSeconds(0.6f);
        }

        if (PlayerRollUI.isHighRollActive)
        {
            if (rollBonusIsMultiplicative)
            {
                currentMult *= 1.5f; // X1.5 stacks on top of the set bonus, like chained X mult jokers
            }
            else
            {
                currentMult = hasSetBonus ? currentMult + 1.5f : 1.5f; // old additive behaviour
            }

            SetBalatroText(baseScore, currentMult);
            StartCoroutine(PulseBalatroText());
            PlaySFX(highRollBonusSound);
            yield return new WaitForSeconds(0.6f);
        }

        float finalMult = currentMult;

        // --- STEP 3: CALCULATE AND DISPLAY FINAL PRODUCT SCORE ---
        int rawFinalScore = Mathf.RoundToInt(baseScore * finalMult);
        int finalScore = rawFinalScore;

        if (PlayerRollUI.isLowRollActive)
        {
            finalScore = Mathf.RoundToInt(rawFinalScore * 0.9f);

            if (balatroScoreText != null)
            {
                balatroScoreText.text = rawFinalScore + " (-10% = " + finalScore + ")";
            }
            StartCoroutine(PulseBalatroText());
            PlaySFX(lowRollPenaltySound);
            yield return new WaitForSeconds(0.6f);
        }
        else
        {
            if (balatroScoreText != null)
            {
                balatroScoreText.text = finalScore.ToString();
            }
        }
        StartCoroutine(PulseBalatroText());
        PlaySFX(finalScoreSound);
        yield return new WaitForSeconds(0.9f);

        if (balatroScoreText != null)
        {
            SetBalatroText(1, 1f);
        }

        // --- STEP 4: SHOW FINAL SCORE POP-OUT & SHAKE ANIMATION IN CARD VIEW ---
        if (totalValueText != null)
        {
            totalValueText.text = finalScore.ToString();
            if (PlayerRollUI.isLowRollActive)
            {
                totalValueText.text = finalScore.ToString() + " (-10%)";
            }

            totalValueText.gameObject.SetActive(true);

            Transform textTransform = totalValueText.transform;
            Vector3 originalScale = textTransform.localScale;
            Vector3 originalPos = textTransform.localPosition;

            float popElapsed = 0f;
            float popDuration = 0.25f;

            while (popElapsed < popDuration)
            {
                popElapsed += Time.unscaledDeltaTime;
                float t = popElapsed / popDuration;
                textTransform.localScale = Vector3.Lerp(originalScale, originalScale * 1.4f, Mathf.Sin(t * Mathf.PI));
                yield return null;
            }
            textTransform.localScale = originalScale;

            float shakeElapsed = 0f;
            float shakeDuration = 0.2f;
            while (shakeElapsed < shakeDuration)
            {
                shakeElapsed += Time.unscaledDeltaTime;
                Vector3 randomOffset = Random.insideUnitSphere * 8f;
                textTransform.localPosition = originalPos + new Vector3(randomOffset.x, randomOffset.y, 0f);
                yield return null;
            }
            textTransform.localPosition = originalPos;
        }

        yield return new WaitForSeconds(cameraSwitchDelay);

        // --- STEP 5: SWITCH CAMERA & REPARENT TOTAL VALUE TEXT FOR COUNTDOWN ---
        PlaySFX(cameraSwitchSound);
        SetCameraState(false);

        if (cardGameNotificationText != null)
        {
            cardGameNotificationText.text = "";
            cardGameNotificationText.gameObject.SetActive(false);
        }

        if (totalValueText != null)
        {
            if (battleManager != null && battleManager.battleUIParent != null)
            {
                totalValueText.transform.SetParent(battleManager.battleUIParent.transform, false);
            }
            totalValueText.text = finalScore.ToString();
            if (PlayerRollUI.isLowRollActive)
            {
                totalValueText.text = finalScore.ToString() + " (-10%)";
            }
            totalValueText.gameObject.SetActive(true);
        }

        if (scoreShakeEffect != null)
        {
            scoreShakeEffect.TriggerScoreEffect(scoreCountdownDuration);
        }

        // --- STEP 6: COUNTDOWN ANIMATION ---
        int startRemainingScore = currentRemainingScore;
        int targetRemainingScore = currentRemainingScore - finalScore;
        float elapsed = 0f;
        float tickTimer = 0f;

        while (elapsed < scoreCountdownDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / scoreCountdownDuration);

            tickTimer += Time.unscaledDeltaTime;
            if (tickTimer >= countdownTickInterval)
            {
                tickTimer = 0f;
                PlaySFX(countdownTickSound, Mathf.Lerp(0.9f, 1.4f, t));
            }

            int currentDisplayScore = Mathf.RoundToInt(Mathf.Lerp(finalScore, 0, t));
            int currentDisplayRemaining = Mathf.RoundToInt(Mathf.Lerp(startRemainingScore, targetRemainingScore, t));

            if (totalValueText != null)
            {
                totalValueText.text = currentDisplayScore.ToString();
                if (PlayerRollUI.isLowRollActive)
                {
                    totalValueText.text = currentDisplayScore.ToString() + " (-10%)";
                }
            }

            currentRemainingScore = currentDisplayRemaining;
            UpdateTargetScoreDisplay();

            yield return null;
        }

        if (totalValueText != null)
        {
            totalValueText.text = "0";
            if (PlayerRollUI.isLowRollActive)
            {
                totalValueText.text = "0 (-10%)";
            }
        }

        currentRemainingScore = targetRemainingScore;
        UpdateTargetScoreDisplay();
        PlaySFX(countdownEndSound);

        yield return new WaitForSeconds(0.3f);

        if (totalValueText != null)
        {
            totalValueText.text = "";
            if (originalTotalValueParent != null)
            {
                totalValueText.transform.SetParent(originalTotalValueParent, false);
            }
        }

        // --- STEP 7: USUAL ROUND ENDING SEQUENCE ---
        PlaySFX(discardSound);
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

        List<CardUI> allCards = new List<CardUI>(spawnedCardUIList);
        foreach (CardUI card in allCards)
        {
            if (card != null)
            {
                if (hand.Contains(card.cardData)) hand.Remove(card.cardData);
                discardPile.Add(card.cardData);
                Destroy(card.gameObject);
            }
        }

        spawnedCardUIList.Clear();
        hand.Clear();

        if (layoutGroup != null) layoutGroup.enabled = true;

        ResetRoundState();

        // Re-enable battle camera button interactions now that full countdown and animation sequence ended
        if (battleManager != null)
        {
            battleManager.SetBattleUIInteractable(true);
        }

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

    // ------------------------------------------------------------------
    // Sound + Balatro-style score display helpers
    // ------------------------------------------------------------------
    private void PlaySFX(AudioClip clip, float pitch = 1f, float volumeScale = 1f)
    {
        GameSFX.Play(clip, sfxVolume * volumeScale, pitch);
    }

    private int CountCardsOfType<T>(List<CardUI> cards) where T : CardData
    {
        int count = 0;
        foreach (CardUI c in cards)
        {
            if (c != null && c.cardData is T) count++;
        }
        return count;
    }

    private string FormatBalatro(int chips, float mult)
    {
        string chipsHex = ColorUtility.ToHtmlStringRGB(chipsColor);
        string multHex = ColorUtility.ToHtmlStringRGB(multColor);
        return "<color=#" + chipsHex + ">" + chips + "</color> x <color=#" + multHex + ">" + mult.ToString("0.##") + "</color>";
    }

    private void SetBalatroText(int chips, float mult)
    {
        if (balatroScoreText != null)
        {
            balatroScoreText.text = FormatBalatro(chips, mult);
        }
    }

    private IEnumerator PulseBalatroText()
    {
        if (balatroScoreText == null) yield break;

        Transform target = balatroScoreText.transform;
        float elapsed = 0f;
        float duration = 0.18f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(elapsed / duration);
            target.localScale = balatroBaseScale * Mathf.Lerp(1f, scorePulseScale, Mathf.Sin(p * Mathf.PI));
            yield return null;
        }

        target.localScale = balatroBaseScale;
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