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

    [Header("Hand Layout (Balatro style)")]
    [Tooltip("Preferred distance between cards. It shrinks automatically so a big hand still fits.")]
    public float handCardSpacing = 200f;
    [Tooltip("Widest the hand may spread (outer edge of the first card to outer edge of the last). 0 = use 'Hand Area Width' below.")]
    public float handMaxWidth = 0f;
    [Tooltip("The area the hand lives in: between the back button and the deck pile. When you hold more cards (Hand Size boon) they squeeze together to stay inside it. Never wider than the hand container.")]
    public float handAreaWidth = 1370f;
    [Tooltip("Closest the cards may get to each other when the hand is squeezed.")]
    public float minCardSpacing = 30f;

    [Header("Drag & Drop (Balatro style)")]
    [Tooltip("Hold a card and drag it to another spot in the hand. The other cards slide aside. The order you set is the order cards are scored in.")]
    public bool allowCardDragging = true;
    public AudioClip cardPickupSound;
    [Tooltip("Played each time the dragged card passes another card.")]
    public AudioClip cardSwapSound;
    public AudioClip cardDropSound;
    [Tooltip("Tilt (degrees) of the outermost cards. The hand fans out like held cards.")]
    public float fanMaxAngle = 6f;
    [Tooltip("How much lower the outermost cards sit, creating the arc.")]
    public float fanArcHeight = 25f;

    [Header("Draw / Discard Motion")]
    [Tooltip("Where drawn cards fly in from and discarded cards fly to (relative to the hand container's centre).")]
    public Vector2 drawSpawnOffset = new Vector2(900f, -150f);
    [Tooltip("Seconds between each card being dealt.")]
    public float drawInterval = 0.08f;
    [Tooltip("Discarded cards fly back to the deck position instead of dropping off the bottom.")]
    public bool discardFlyToDeck = true;
    [Tooltip("Delay between each discarded card leaving.")]
    public float discardStagger = 0.04f;

    [Header("Deck Pile Visual")]
    [Tooltip("Optional: an object in your card canvas that IS the deck. Cards come out of it and discards fly back into it. Leave empty to build a pile from the Card Back Sprite instead.")]
    public RectTransform deckPile;
    [Tooltip("Card back image. If set (and no Deck Pile is assigned), a stack of cards is created automatically at the Draw Spawn Offset. The stack gets thinner as you draw.")]
    public Sprite cardBackSprite;
    [Tooltip("How many cards thick the full stack looks.")]
    public int deckVisualLayers = 8;
    [Tooltip("Shift between stacked cards, giving the pile its thickness.")]
    public Vector2 deckLayerOffset = new Vector2(1.5f, 2f);
    [Tooltip("Size of the auto-built pile. 0,0 = same size as the card prefab.")]
    public Vector2 deckSize = Vector2.zero;

    [Header("Card Destroy Effect (Balatro style)")]
    [Tooltip("After scoring, the played cards shake and break apart into pieces instead of flying away.")]
    public bool dissolveScoredCards = true;
    public int dissolveColumns = 7;
    public int dissolveRows = 10;
    [Tooltip("Shake before the card breaks up.")]
    public float dissolveShakeTime = 0.18f;
    [Tooltip("How long the break-up takes.")]
    public float dissolveDuration = 0.8f;
    [Tooltip("Delay between each played card starting to dissolve.")]
    public float dissolveStagger = 0.1f;
    [Tooltip("How far the pieces drift away.")]
    public float dissolveDriftDistance = 140f;

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

    [Header("Card Score Popups (Balatro style)")]
    [Tooltip("While scoring, each card shows its value above it (+10). If the card is boosted (Wind Power boon, mood), the extra shows right after as a second number.")]
    public bool showCardValuePopups = true;
    [Tooltip("Colour of the card's own value. Uses the Chips colour when left on the default.")]
    public Color cardValuePopupColor = new Color(0.30f, 0.65f, 1f);
    [Tooltip("Colour of the extra value from boosts (Wind Power boon, mood...).")]
    public Color modifiedValuePopupColor = new Color(1f, 0.85f, 0.25f);
    public float popupFontSize = 64f;
    [Tooltip("Gap between the top of the card and the first number.")]
    public float popupOffsetY = 45f;
    [Tooltip("Distance between stacked numbers (card value, then the boost above it).")]
    public float popupStackSpacing = 70f;
    [Tooltip("How far a number floats upward.")]
    public float popupRise = 30f;
    public float popupHoldTime = 0.55f;
    public float popupFadeTime = 0.25f;

    [Header("Sound Effects")]
    [Range(0f, 1f)] public float sfxVolume = 1f;
    public AudioClip openCardViewSound;
    public AudioClip backButtonSound;
    public AudioClip drawCardSound;
    [Tooltip("Played when the deck is shuffled and a new hand is dealt, and when discards are reshuffled in.")]
    public AudioClip shuffleSound;
    [Tooltip("Played as each scored card breaks apart.")]
    public AudioClip cardDestroySound;
    public AudioClip cardSelectSound;
    public AudioClip cardDeselectSound;
    public AudioClip discardSound;
    public AudioClip confirmSound;
    public AudioClip cardSlideSound;
    [Tooltip("Played for every scored card. The pitch rises with each card (Balatro style).")]
    public AudioClip cardScoreSound;
    [Tooltip("Fallback used when the Solar/Wind card asset has no Bonus Sound of its own.")]
    public AudioClip setBonusSound;
    [Tooltip("Played when a trinket adds to your mult (Candy, Wind Charm, Wind Fan, Sun Charm, Sun Plushie, Sugar Rush).")]
    public AudioClip trinketTriggerSound;

    [Header("Low Power Cards")]
    [Tooltip("Chance (0-1) that any card you draw is a Low Power copy: dimmed out and worth half. 0.06 = 6%.")]
    [Range(0f, 1f)] public float lowPowerChance = 0.06f;
    [Tooltip("Played when a Low Power card is dealt.")]
    public AudioClip lowPowerDrawSound;
    [Tooltip("Pause (seconds) after each trinket triggers while scoring, so you can watch them go from top to bottom.")]
    public float trinketTriggerDelay = 0.4f;
    [Tooltip("Played when the permanent Wind Power boon adds its flat bonus to your score.")]
    public AudioClip windPowerBoonSound;
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

    private CardUI draggingCard;
    private float currentSpacing = 200f;

    private int lastSelectedCount = 0;
    private bool suppressSelectSounds = false;
    private Vector3 balatroBaseScale = Vector3.one;

    void Awake()
    {
        maxHandSize += BoonProgress.ExtraHandSize;

        maxDiscardsAllowed = Mathf.Max(0, maxDiscardsAllowed + MoodManager.ReshufflesModifier);

        if (balatroScoreText != null)
        {
            balatroBaseScale = balatroScoreText.transform.localScale;
            MakeTextFit(balatroScoreText, 18f);
        }
        MakeTextFit(totalValueText, 18f);

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
            PlaySFX(shuffleSound);
            DrawCards(drawAmount);
        }
        else if (spawnedCardUIList.Count == 0 && hand.Count > 0)
        {
            foreach (CardData cardData in hand)
            {
                CreateCardUI(cardData);
            }
            RebuildLayout(true);
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

        UpdateDeckVisual();
    }

    public void DrawCards(int count)
    {
        StartCoroutine(DrawCardsRoutine(count));
    }

    private IEnumerator DrawCardsRoutine(int count)
    {
        DisableHandLayoutGroup();

        for (int i = 0; i < count; i++)
        {
            if (drawPile.Count == 0 || cardsRemainingInRound <= 0 || hand.Count >= maxHandSize)
                break;

            CardData drawnCard = drawPile[0];
            drawPile.RemoveAt(0);

            bool becameLowPower = false;
            if (!drawnCard.isLowPower && Random.value < lowPowerChance * StructureProgress.LowPowerChanceFactorFor(drawnCard))
            {
                drawnCard = MakeLowPowerCopy(drawnCard);
                becameLowPower = true;
            }

            hand.Add(drawnCard);
            cardsRemainingInRound--;

            CreateCardUI(drawnCard, true);
            RebuildLayout();

            PlaySFX(drawCardSound, Mathf.Min(1f + i * 0.05f, 1.5f) + Random.Range(-0.02f, 0.02f));
            if (becameLowPower) PlaySFX(lowPowerDrawSound);
            UpdateHandCountDisplay();
            UpdateDeckLeftDisplay();

            yield return new GameSpeedWait(drawInterval);
        }

        RebuildLayout();
        UpdateSelectedTotal();
    }

    private static CardData MakeLowPowerCopy(CardData original)
    {
        CardData copy = Instantiate(original);
        copy.name = original.name + " (Low Power)";
        copy.hideFlags = HideFlags.DontSave;
        copy.isLowPower = true;
        copy.lowPowerSource = original;
        return copy;
    }

    public int DebugMakeHandLowPower(bool onlySelected)
    {
        return DebugSwapHand(onlySelected, true);
    }

    public int DebugRestoreHand(bool onlySelected)
    {
        return DebugSwapHand(onlySelected, false);
    }

    private int DebugSwapHand(bool onlySelected, bool makeLow)
    {
        if (isScoring || isDiscarding) return 0;

        int changed = 0;
        foreach (CardUI ui in spawnedCardUIList)
        {
            if (ui == null || ui.cardData == null) continue;
            if (onlySelected && !ui.isSelected) continue;

            CardData old = ui.cardData;
            CardData replacement;
            if (makeLow)
            {
                if (old.isLowPower) continue;
                replacement = MakeLowPowerCopy(old);
            }
            else
            {
                if (!old.isLowPower || old.lowPowerSource == null) continue;
                replacement = old.lowPowerSource;
            }

            int index = hand.IndexOf(old);
            if (index >= 0) hand[index] = replacement;
            ui.LoadCardData(replacement);
            changed++;
        }

        if (changed > 0)
        {
            PlaySFX(makeLow ? lowPowerDrawSound : null);
            UpdateSelectedTotal();
        }
        return changed;
    }

    private CardUI CreateCardUI(CardData data, bool fromDeck = false)
    {
        if (cardPrefab == null || handParentContainer == null) return null;

        GameObject newCardObj = Instantiate(cardPrefab, handParentContainer);
        CardUI cardUI = newCardObj.GetComponent<CardUI>();
        if (cardUI == null) return null;

        cardUI.LoadCardData(data);
        spawnedCardUIList.Add(cardUI);

        LayoutElement le = newCardObj.GetComponent<LayoutElement>();
        if (le != null) le.ignoreLayout = true;

        RectTransform cardRect = newCardObj.GetComponent<RectTransform>();
        if (cardRect != null)
        {
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
        }

        CardDragHandler drag = newCardObj.GetComponent<CardDragHandler>();
        if (drag == null) drag = newCardObj.AddComponent<CardDragHandler>();
        drag.manager = this;
        drag.card = cardUI;

        cardUI.followSlot = true;
        if (fromDeck)
        {
            EnsureDeckVisual();
            cardUI.SnapTo(GetDeckPosition(), 1f, 0f);
        }

        return cardUI;
    }

    private void DisableHandLayoutGroup()
    {
        if (handParentContainer == null) return;
        HorizontalLayoutGroup layoutGroup = handParentContainer.GetComponent<HorizontalLayoutGroup>();
        if (layoutGroup != null) layoutGroup.enabled = false;
    }

    private void RebuildLayout(bool instant = false)
    {
        spawnedCardUIList.RemoveAll(item => item == null);
        if (handParentContainer == null) return;

        DisableHandLayoutGroup();

        int n = spawnedCardUIList.Count;
        if (n == 0) return;

        float cardWidth = 0f;
        RectTransform firstRect = spawnedCardUIList[0].GetComponent<RectTransform>();
        if (firstRect != null) cardWidth = firstRect.rect.width;

        float available = handMaxWidth > 0f ? handMaxWidth : handAreaWidth;
        if (handParentContainer is RectTransform handRect && handRect.rect.width >= 100f)
            available = available > 0f ? Mathf.Min(available, handRect.rect.width) : handRect.rect.width;
        if (available < 100f) available = float.MaxValue;

        float spacing = handCardSpacing;
        if (n > 1 && available < float.MaxValue)
            spacing = Mathf.Min(handCardSpacing, Mathf.Max(minCardSpacing, (available - cardWidth) / (n - 1)));
        currentSpacing = Mathf.Max(1f, spacing);

        float half = (n - 1) / 2f;

        for (int i = 0; i < n; i++)
        {
            CardUI card = spawnedCardUIList[i];
            if (card == null || card.isAnimatingToLayout) continue;

            float norm = n > 1 ? (i - half) / half : 0f;

            card.followSlot = card != draggingCard;
            card.slotPosition = new Vector2((i - half) * spacing, -(norm * norm) * fanArcHeight);
            card.slotRotation = -norm * fanMaxAngle;

            if (card.transform.parent != handParentContainer) card.transform.SetParent(handParentContainer, false);
            LayoutElement le = card.GetComponent<LayoutElement>();
            if (le != null) le.ignoreLayout = true;

            if (instant && card != draggingCard) card.SnapToSlot();
        }

        for (int i = 0; i < n; i++)
        {
            CardUI card = spawnedCardUIList[i];
            if (card != null && card != draggingCard) card.transform.SetAsLastSibling();
        }
        if (draggingCard != null) draggingCard.transform.SetAsLastSibling();
    }

    public bool CanDragCards
    {
        get
        {
            return allowCardDragging && !isScoring && !isDiscarding &&
                   (battleManager == null || !battleManager.isOpeningSequenceActive);
        }
    }

    public void BeginCardDrag(CardUI card)
    {
        if (card == null) return;
        draggingCard = card;
        card.followSlot = false;
        card.transform.SetAsLastSibling();
        PlaySFX(cardPickupSound);
    }

    private readonly Vector3[] clampCorners = new Vector3[4];

    public Vector2 ClampToHandArea(Vector2 pos, RectTransform cardRect)
    {
        RectTransform parent = handParentContainer as RectTransform;
        if (parent == null || cardRect == null) return pos;

        Canvas canvas = parent.GetComponentInParent<Canvas>();
        if (canvas == null) return pos;
        RectTransform canvasRect = canvas.rootCanvas.transform as RectTransform;
        if (canvasRect == null) return pos;

        canvasRect.GetWorldCorners(clampCorners);
        Vector2 center = parent.rect.center;
        Vector3 lo = parent.InverseTransformPoint(clampCorners[0]);
        Vector3 hi = parent.InverseTransformPoint(clampCorners[2]);

        float halfW = cardRect.rect.width * 0.5f * Mathf.Abs(cardRect.localScale.x);
        float halfH = cardRect.rect.height * 0.5f * Mathf.Abs(cardRect.localScale.y);

        float minX = lo.x - center.x + halfW;
        float maxX = hi.x - center.x - halfW;
        float minY = lo.y - center.y + halfH;
        float maxY = hi.y - center.y - halfH;

        if (minX < maxX) pos.x = Mathf.Clamp(pos.x, minX, maxX);
        if (minY < maxY) pos.y = Mathf.Clamp(pos.y, minY, maxY);
        return pos;
    }

    public void OnCardDragged(CardUI card)
    {
        if (card == null || card != draggingCard) return;

        int n = spawnedCardUIList.Count;
        int current = spawnedCardUIList.IndexOf(card);
        if (n < 2 || current < 0) return;

        RectTransform rect = card.GetComponent<RectTransform>();
        if (rect == null) return;

        float half = (n - 1) / 2f;
        int target = Mathf.Clamp(Mathf.RoundToInt(rect.anchoredPosition.x / currentSpacing + half), 0, n - 1);
        if (target == current) return;

        spawnedCardUIList.RemoveAt(current);
        spawnedCardUIList.Insert(target, card);

        PlaySFX(cardSwapSound, 1f + Random.Range(-0.04f, 0.04f));
        RebuildLayout();
    }

    public void EndCardDrag(CardUI card)
    {
        if (card == null || card != draggingCard) { if (card == null) draggingCard = null; return; }

        draggingCard = null;
        card.SyncSpringState();
        RebuildLayout();
        PlaySFX(cardDropSound);
    }

    private RectTransform autoDeckRoot;
    private readonly List<Image> deckLayers = new List<Image>();

    private bool HasDeckVisual()
    {
        return deckPile != null || autoDeckRoot != null;
    }

    private Vector2 GetDeckPosition()
    {
        if (deckPile != null && handParentContainer is RectTransform handRect)
        {
            Vector3 local = handRect.InverseTransformPoint(deckPile.position);
            return new Vector2(local.x, local.y) - handRect.rect.center;
        }

        if (autoDeckRoot != null) return autoDeckRoot.anchoredPosition;
        return drawSpawnOffset;
    }

    private void EnsureDeckVisual()
    {
        if (deckPile != null || autoDeckRoot != null) return;
        if (cardBackSprite == null || handParentContainer == null) return;

        Vector2 size = deckSize;
        if (size == Vector2.zero && cardPrefab != null)
        {
            RectTransform prefabRect = cardPrefab.GetComponent<RectTransform>();
            if (prefabRect != null) size = prefabRect.rect.size;
        }
        if (size == Vector2.zero) size = new Vector2(200f, 300f);

        GameObject root = new GameObject("DeckPile", typeof(RectTransform));
        root.transform.SetParent(handParentContainer, false);
        root.transform.SetAsFirstSibling();

        autoDeckRoot = root.GetComponent<RectTransform>();
        autoDeckRoot.anchorMin = new Vector2(0.5f, 0.5f);
        autoDeckRoot.anchorMax = new Vector2(0.5f, 0.5f);
        autoDeckRoot.pivot = new Vector2(0.5f, 0.5f);
        autoDeckRoot.sizeDelta = size;
        autoDeckRoot.anchoredPosition = drawSpawnOffset;

        int layers = Mathf.Max(1, deckVisualLayers);
        for (int i = 0; i < layers; i++)
        {
            GameObject layer = new GameObject("Layer" + i, typeof(RectTransform), typeof(Image));
            layer.transform.SetParent(root.transform, false);

            RectTransform lr = layer.GetComponent<RectTransform>();
            lr.anchorMin = new Vector2(0.5f, 0.5f);
            lr.anchorMax = new Vector2(0.5f, 0.5f);
            lr.pivot = new Vector2(0.5f, 0.5f);
            lr.sizeDelta = size;
            lr.anchoredPosition = deckLayerOffset * i;

            Image img = layer.GetComponent<Image>();
            img.sprite = cardBackSprite;
            img.raycastTarget = false;
            deckLayers.Add(img);
        }
    }

    private void UpdateDeckVisual()
    {
        EnsureDeckVisual();
        if (autoDeckRoot == null || deckLayers.Count == 0) return;

        int total = Mathf.Max(1, totalDeckCards);
        int visible = 0;
        if (cardsRemainingInRound > 0)
            visible = Mathf.Clamp(Mathf.CeilToInt((float)cardsRemainingInRound / total * deckLayers.Count), 1, deckLayers.Count);

        for (int i = 0; i < deckLayers.Count; i++)
            deckLayers[i].gameObject.SetActive(i < visible);
    }

    private float GetDissolveTotalTime(int cardCount)
    {
        return Mathf.Max(0, cardCount - 1) * dissolveStagger + dissolveShakeTime + dissolveDuration + 0.05f;
    }

    private IEnumerator DissolveCard(CardUI card, float startDelay, float pitch)
    {
        if (startDelay > 0f) yield return new GameSpeedWait(startDelay);
        if (card == null) yield break;

        RectTransform cardRect = card.GetComponent<RectTransform>();
        Image img = card.cardImage != null ? card.cardImage : card.GetComponent<Image>();
        if (cardRect == null) yield break;

        PlaySFX(cardDestroySound, pitch);

        Vector2 basePos = cardRect.anchoredPosition;
        Quaternion baseRot = cardRect.localRotation;
        float t = 0f;
        while (t < dissolveShakeTime)
        {
            if (card == null) yield break;
            t += GameSpeed.DeltaTime;
            float strength = Mathf.Lerp(2f, 9f, t / Mathf.Max(0.0001f, dissolveShakeTime));
            cardRect.anchoredPosition = basePos + Random.insideUnitCircle * strength;
            cardRect.localRotation = baseRot * Quaternion.Euler(0f, 0f, Random.Range(-2.5f, 2.5f));
            yield return null;
        }
        if (card == null) yield break;
        cardRect.anchoredPosition = basePos;
        cardRect.localRotation = baseRot;

        if (img == null || img.sprite == null || img.sprite.texture == null)
        {
            CanvasGroup fade = card.GetComponent<CanvasGroup>();
            if (fade == null) fade = card.gameObject.AddComponent<CanvasGroup>();
            float f = 0f;
            while (f < dissolveDuration && card != null)
            {
                f += GameSpeed.DeltaTime;
                fade.alpha = 1f - Mathf.Clamp01(f / dissolveDuration);
                yield return null;
            }
            yield break;
        }

        int cols = Mathf.Max(1, dissolveColumns);
        int rows = Mathf.Max(1, dissolveRows);

        Sprite sprite = img.sprite;
        Texture tex = sprite.texture;
        Rect tr = sprite.textureRect;
        Rect uv = new Rect(tr.x / tex.width, tr.y / tex.height, tr.width / tex.width, tr.height / tex.height);

        GameObject fxObj = new GameObject("CardDissolveFX", typeof(RectTransform));
        RectTransform fx = fxObj.GetComponent<RectTransform>();
        fx.SetParent(cardRect.parent, false);
        fx.anchorMin = cardRect.anchorMin;
        fx.anchorMax = cardRect.anchorMax;
        fx.pivot = cardRect.pivot;
        fx.sizeDelta = cardRect.sizeDelta;
        fx.anchoredPosition = cardRect.anchoredPosition;
        fx.localRotation = cardRect.localRotation;
        fx.localScale = cardRect.localScale;
        fx.SetSiblingIndex(Mathf.Min(card.transform.GetSiblingIndex() + 1, cardRect.parent.childCount - 1));

        Vector2 size = cardRect.rect.size;
        float cw = size.x / cols;
        float ch = size.y / rows;

        int count = cols * rows;
        RectTransform[] rts = new RectTransform[count];
        RawImage[] raws = new RawImage[count];
        Vector2[] startPos = new Vector2[count];
        Vector2[] drift = new Vector2[count];
        float[] delay = new float[count];
        float[] spin = new float[count];
        Color baseColor = img.color;

        int k = 0;
        for (int x = 0; x < cols; x++)
        {
            for (int y = 0; y < rows; y++)
            {
                GameObject piece = new GameObject("Piece", typeof(RectTransform), typeof(RawImage));
                RectTransform pr = piece.GetComponent<RectTransform>();
                pr.SetParent(fx, false);
                pr.anchorMin = Vector2.zero;
                pr.anchorMax = Vector2.zero;
                pr.pivot = new Vector2(0.5f, 0.5f);
                pr.sizeDelta = new Vector2(cw + 0.75f, ch + 0.75f);
                startPos[k] = new Vector2((x + 0.5f) * cw, (y + 0.5f) * ch);
                pr.anchoredPosition = startPos[k];

                RawImage raw = piece.GetComponent<RawImage>();
                raw.texture = tex;
                raw.uvRect = new Rect(uv.x + uv.width * x / cols, uv.y + uv.height * y / rows, uv.width / cols, uv.height / rows);
                raw.color = baseColor;
                raw.raycastTarget = false;

                delay[k] = Random.value * dissolveDuration * 0.6f;
                drift[k] = new Vector2(Random.Range(-1f, 1f), Random.Range(0.15f, 1f)).normalized * dissolveDriftDistance * Random.Range(0.35f, 1f);
                spin[k] = Random.Range(-140f, 140f);

                rts[k] = pr;
                raws[k] = raw;
                k++;
            }
        }

        CanvasGroup cg = card.GetComponent<CanvasGroup>();
        if (cg == null) cg = card.gameObject.AddComponent<CanvasGroup>();
        cg.alpha = 0f;
        cg.blocksRaycasts = false;

        float elapsed = 0f;
        while (elapsed < dissolveDuration)
        {
            elapsed += GameSpeed.DeltaTime;

            for (int i = 0; i < count; i++)
            {
                float life = Mathf.Max(0.05f, dissolveDuration - delay[i]);
                float p = Mathf.Clamp01((elapsed - delay[i]) / life);
                if (p <= 0f) continue;

                float ease = 1f - (1f - p) * (1f - p);
                rts[i].anchoredPosition = startPos[i] + drift[i] * ease;
                rts[i].localRotation = Quaternion.Euler(0f, 0f, spin[i] * ease);
                rts[i].localScale = Vector3.one * Mathf.Lerp(1f, 0.15f, p);

                Color c = baseColor;
                c.a = baseColor.a * (1f - p * p);
                raws[i].color = c;
            }

            yield return null;
        }

        Destroy(fxObj);
    }

    private IEnumerator FlyCardAway(CardUI card, float delay = 0f)
    {
        if (delay > 0f) yield return new GameSpeedWait(delay);
        if (card == null) yield break;

        if (discardFlyToDeck)
        {
            if (HasDeckVisual())
                yield return StartCoroutine(card.MoveToAnchoredPosition(GetDeckPosition(), discardFlyDuration, 1f, 0f));
            else
                yield return StartCoroutine(card.MoveToAnchoredPosition(GetDeckPosition(), discardFlyDuration, 0.55f, 12f));
        }
        else
        {
            RectTransform cardRect = card.GetComponent<RectTransform>();
            if (cardRect == null) yield break;
            Vector2 offscreenPos = new Vector2(cardRect.anchoredPosition.x, -(Screen.height * 0.75f));
            yield return StartCoroutine(card.MoveToAnchoredPosition(offscreenPos, discardFlyDuration));
        }
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
        for (int fi = 0; fi < selectedCards.Count; fi++)
            StartCoroutine(FlyCardAway(selectedCards[fi], fi * discardStagger));

        yield return new GameSpeedWait(discardFlyDuration + 0.05f + selectedCards.Count * discardStagger);

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
        PlaySFX(shuffleSound);
        UpdateDeckLeftDisplay();

        spawnedCardUIList.RemoveAll(item => item == null);
        foreach (CardUI card in remainingCards)
        {
            if (card != null) card.DeselectCard();
        }
        lastSelectedCount = 0;

        RebuildLayout();
        UpdateHandCountDisplay();

        yield return new GameSpeedWait(0.25f);
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
        TrinketSidebar.BeginScoring();

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
        yield return new GameSpeedWait(0.4f);

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
                    setMult = solar.EffectiveMultiplier;
                    hasSetBonus = true;
                    setBonusClip = solar.bonusSound;
                    setBonusVolume = solar.bonusVolume;
                }
            }
            else if (firstData is WindCard wind)
            {
                if (CountCardsOfType<WindCard>(selectedCards) >= wind.requiredCards)
                {
                    setMult = wind.EffectiveMultiplier;
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

        List<TrinketData> trinketOrder = new List<TrinketData>(TrinketInventory.Owned);

        int scoredIndex = 0;
        bool windBoonSoundPlayed = false;
        foreach (CardUI card in selectedCards)
        {
            if (card.cardData != null)
            {
                Vector3 origScale = card.transform.localScale;
                card.transform.localScale = origScale * 1.25f;

                int cardValue = card.cardData.GetEffectiveValue();

                int ownValue = Mathf.Min(cardValue, GetBaseCardValue(card.cardData, cardValue));
                int boost = cardValue - ownValue;

                baseScore += ownValue;
                SetBalatroText(baseScore, currentMult);
                StartCoroutine(PulseBalatroText());
                ShowCardPopup(card, "+" + ownValue, cardValuePopupColor, 0);

                PlaySFX(cardScoreSound, Mathf.Min(1f + scoredIndex * 0.08f, 2f));
                scoredIndex++;

                if (boost > 0)
                {
                    yield return new GameSpeedWait(0.25f);

                    if (card.cardData is WindCard && BoonProgress.BonusWindPower > 0 && !windBoonSoundPlayed)
                    {
                        PlaySFX(windPowerBoonSound);
                        windBoonSoundPlayed = true;
                    }

                    baseScore += boost;
                    SetBalatroText(baseScore, currentMult);
                    StartCoroutine(PulseBalatroText());
                    ShowCardPopup(card, "+" + boost, modifiedValuePopupColor, 1);
                    PlaySFX(cardScoreSound, Mathf.Min(1f + scoredIndex * 0.08f + 0.2f, 2f));
                }

                yield return new GameSpeedWait(0.15f);
                card.transform.localScale = origScale;
                yield return new GameSpeedWait(0.2f);

                for (int ti = 0; ti < trinketOrder.Count; ti++)
                {
                    string trinketLabel;
                    bool trinketTriggered = TrinketEffects.ApplyOnCardScored(trinketOrder[ti], card.cardData, ref currentMult, out trinketLabel);
                    if (trinketLabel == null) continue;

                    TrinketSidebar.Pulse(ti, trinketLabel);
                    if (trinketTriggered)
                    {
                        SetBalatroText(baseScore, currentMult);
                        StartCoroutine(PulseBalatroText());
                        PlaySFX(trinketTriggerSound, Mathf.Min(1f + ti * 0.08f, 1.5f));
                    }
                    yield return new GameSpeedWait(trinketTriggerDelay);
                }
            }
        }

        yield return new GameSpeedWait(0.3f);

        int windCardsInHand = CountCardsOfType<WindCard>(selectedCards);
        for (int ti = 0; ti < trinketOrder.Count; ti++)
        {
            string handLabel;
            if (TrinketEffects.ApplyOnHandScored(trinketOrder[ti], windCardsInHand, ref currentMult, out handLabel))
            {
                TrinketSidebar.Pulse(ti, handLabel);
                SetBalatroText(baseScore, currentMult);
                StartCoroutine(PulseBalatroText());
                PlaySFX(trinketTriggerSound, Mathf.Min(1f + ti * 0.08f, 1.5f));
                yield return new GameSpeedWait(trinketTriggerDelay + 0.2f);
            }
        }

        if (hasSetBonus)
        {
            currentMult *= setMult;
            SetBalatroText(baseScore, currentMult);
            StartCoroutine(PulseBalatroText());
            PlaySFX(setBonusClip != null ? setBonusClip : setBonusSound, 1f, setBonusVolume);
            yield return new GameSpeedWait(0.6f);
        }

        float moodMult = MoodManager.ScoreMultiplier;
        if (moodMult > 1.001f)
        {
            currentMult *= moodMult;
            MoodHUD.Pulse("X" + moodMult.ToString("0.##") + " Mult");
            SetBalatroText(baseScore, currentMult);
            StartCoroutine(PulseBalatroText());
            PlaySFX(trinketTriggerSound, 1.25f);
            yield return new GameSpeedWait(0.6f);
        }

        if (PlayerRollUI.isHighRollActive)
        {
            if (rollBonusIsMultiplicative)
            {
                currentMult *= 1.5f;
            }
            else
            {
                currentMult = hasSetBonus ? currentMult + 1.5f : 1.5f;
            }

            SetBalatroText(baseScore, currentMult);
            StartCoroutine(PulseBalatroText());
            PlaySFX(highRollBonusSound);
            yield return new GameSpeedWait(0.6f);
        }

        TrinketSidebar.EndScoring();

        float finalMult = currentMult;

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
            yield return new GameSpeedWait(0.6f);
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
        yield return new GameSpeedWait(0.9f);

        if (balatroScoreText != null)
        {
            SetBalatroText(1, 1f);
        }

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
                popElapsed += GameSpeed.DeltaTime;
                float t = popElapsed / popDuration;
                textTransform.localScale = Vector3.Lerp(originalScale, originalScale * 1.4f, Mathf.Sin(t * Mathf.PI));
                yield return null;
            }
            textTransform.localScale = originalScale;

            float shakeElapsed = 0f;
            float shakeDuration = 0.2f;
            while (shakeElapsed < shakeDuration)
            {
                shakeElapsed += GameSpeed.DeltaTime;
                Vector3 randomOffset = Random.insideUnitSphere * 8f;
                textTransform.localPosition = originalPos + new Vector3(randomOffset.x, randomOffset.y, 0f);
                yield return null;
            }
            textTransform.localPosition = originalPos;
        }

        yield return new GameSpeedWait(cameraSwitchDelay);

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

        int startRemainingScore = currentRemainingScore;
        int targetRemainingScore = currentRemainingScore - finalScore;
        float elapsed = 0f;
        float tickTimer = 0f;

        while (elapsed < scoreCountdownDuration)
        {
            elapsed += GameSpeed.DeltaTime;
            float t = Mathf.Clamp01(elapsed / scoreCountdownDuration);

            tickTimer += GameSpeed.DeltaTime;
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

        yield return new GameSpeedWait(0.3f);

        if (totalValueText != null)
        {
            totalValueText.text = "";
            if (originalTotalValueParent != null)
            {
                totalValueText.transform.SetParent(originalTotalValueParent, false);
            }
        }

        float endWait = discardFlyDuration + 0.05f;

        if (dissolveScoredCards)
        {
            for (int di = 0; di < selectedCards.Count; di++)
                StartCoroutine(DissolveCard(selectedCards[di], di * dissolveStagger, Mathf.Min(1f + di * 0.06f, 1.4f)));

            endWait = Mathf.Max(endWait, GetDissolveTotalTime(selectedCards.Count) - delayBetweenDiscards);
        }
        else
        {
            PlaySFX(discardSound);
            for (int fi = 0; fi < selectedCards.Count; fi++)
                StartCoroutine(FlyCardAway(selectedCards[fi], fi * discardStagger));
        }

        yield return new GameSpeedWait(delayBetweenDiscards);

        if (dissolveScoredCards && remainingCards.Count > 0) PlaySFX(discardSound);
        foreach (CardUI card in remainingCards)
            StartCoroutine(FlyCardAway(card));

        yield return new GameSpeedWait(endWait);

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

        ResetRoundState();

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

    private void PlaySFX(AudioClip clip, float pitch = 1f, float volumeScale = 1f)
    {
        GameSFX.Play(clip, sfxVolume * volumeScale, pitch);
    }

    private static int GetBaseCardValue(CardData data, int fallback)
    {
        if (data == null) return fallback;

        System.Type type = data.GetType();
        System.Reflection.FieldInfo field = type.GetField("primaryValue", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        if (field != null && field.FieldType == typeof(int)) return (int)field.GetValue(data);

        System.Reflection.PropertyInfo prop = type.GetProperty("primaryValue", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        if (prop != null && prop.PropertyType == typeof(int) && prop.CanRead) return (int)prop.GetValue(data, null);

        return fallback;
    }

    private void ShowCardPopup(CardUI card, string text, Color color, int stackIndex)
    {
        if (!showCardValuePopups || card == null || handParentContainer == null) return;
        StartCoroutine(CardPopupRoutine(card, text, color, stackIndex));
    }

    private IEnumerator CardPopupRoutine(CardUI card, string text, Color color, int stackIndex)
    {
        RectTransform cardRect = card.GetComponent<RectTransform>();
        if (cardRect == null) yield break;

        GameObject go = new GameObject("CardScorePopup", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        go.GetComponent<LayoutElement>().ignoreLayout = true;

        RectTransform r = go.GetComponent<RectTransform>();
        r.SetParent(handParentContainer, false);
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
        r.sizeDelta = new Vector2(400f, popupFontSize * 1.4f);
        r.SetAsLastSibling();

        TextMeshProUGUI t = go.GetComponent<TextMeshProUGUI>();
        if (balatroScoreText != null) t.font = balatroScoreText.font;
        t.text = text;
        t.color = color;
        t.fontSize = popupFontSize;
        t.fontStyle = FontStyles.Bold;
        t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false;
        t.outlineWidth = 0.25f;
        t.outlineColor = new Color32(0, 0, 0, 255);

        float cardHalfHeight = cardRect.rect.height * 0.5f * Mathf.Abs(cardRect.localScale.y);
        Vector2 start = cardRect.anchoredPosition + new Vector2(0f, cardHalfHeight + popupOffsetY + stackIndex * popupStackSpacing);
        r.anchoredPosition = start;
        r.localScale = Vector3.zero;

        float popTime = 0.2f;
        float e = 0f;
        while (e < popTime)
        {
            e += GameSpeed.DeltaTime;
            float p = Mathf.Clamp01(e / popTime);
            float x = p - 1f;
            const float c1 = 1.70158f;
            float back = 1f + (c1 + 1f) * x * x * x + c1 * x * x;
            r.localScale = Vector3.one * back;
            r.anchoredPosition = start + new Vector2(0f, popupRise * 0.5f * p);
            yield return null;
        }
        r.localScale = Vector3.one;

        float hold = 0f;
        while (hold < popupHoldTime)
        {
            hold += GameSpeed.DeltaTime;
            float p = Mathf.Clamp01(hold / Mathf.Max(0.01f, popupHoldTime));
            r.anchoredPosition = start + new Vector2(0f, popupRise * (0.5f + 0.3f * p));
            yield return null;
        }

        float f = 0f;
        Vector2 fadeStart = r.anchoredPosition;
        while (f < popupFadeTime)
        {
            f += GameSpeed.DeltaTime;
            float p = Mathf.Clamp01(f / Mathf.Max(0.01f, popupFadeTime));
            Color c = color;
            c.a = 1f - p;
            t.color = c;
            r.anchoredPosition = fadeStart + new Vector2(0f, popupRise * 0.2f * p);
            yield return null;
        }

        Destroy(go);
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
        return "<color=#" + chipsHex + ">" + Compact(chips, "0") + "</color> x <color=#" + multHex + ">" + Compact(mult, "0.##") + "</color>";
    }

    private static string Compact(float value, string format)
    {
        float a = Mathf.Abs(value);
        if (a >= 1e12f) return (value / 1e12f).ToString("0.##") + "T";
        if (a >= 1e9f) return (value / 1e9f).ToString("0.##") + "B";
        if (a >= 1e6f) return (value / 1e6f).ToString("0.##") + "M";
        return value.ToString(format);
    }

    private static void MakeTextFit(TMP_Text text, float minSize)
    {
        if (text == null) return;
        text.enableAutoSizing = true;
        text.fontSizeMax = text.fontSize;
        text.fontSizeMin = Mathf.Min(minSize, text.fontSize);
#pragma warning disable 618
        text.enableWordWrapping = false;
#pragma warning restore 618
        text.overflowMode = TextOverflowModes.Overflow;
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
            elapsed += GameSpeed.DeltaTime;
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