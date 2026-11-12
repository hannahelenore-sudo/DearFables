using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class BattleManager : MonoBehaviour
{
    [Header("Cameras")]
    public Camera battleCamera;
    public Camera cardFieldCamera;

    [Header("Player Roll Link")]
    public PlayerRollUI playerRollUI;
    public Canvas cardSystemCanvas;

    [Header("UI References")]
    public GameObject battleUIParent;
    public ScoreToBeatUI scoreToBeatUI;
    public NumberSpinnerUI numberSpinnerUI;
    public ScoreShakeEffect scoreShakeEffect; // Reference to the score shake/fall script

    [Header("Current Round Text References")]
    public TextMeshProUGUI currentRoundText1;
    public TextMeshProUGUI currentRoundText2;

    [Tooltip("Panel or Image used to dim the screen for the round intro.")]
    public Image roundDimOverlay;

    [Tooltip("TextMeshProUGUI element that displays text like 'ROUND 1/5'.")]
    public TextMeshProUGUI roundPopupText;

    [Header("Ending Sequence UI References")]
    public GameObject endingRootObject;         // The ENDING parent GameObject
    public RectTransform paperImgTransform;     // PAPERIMG RectTransform
    public TextMeshProUGUI targetScoreTxt;      // TARGETSCORETXT
    public TextMeshProUGUI yourScoreTxt;        // YOURSCORETXT
    public TextMeshProUGUI starsGainedTxt;      // STARSGAINEDTEXT
    public GameObject winGameObject;            // WIN parent object
    public GameObject lostGameObject;           // LOST parent object

    [Header("Ending Settings")]
    public int starsGainedOnWin = 1;            // Configurable stars in inspector
    public float paperFlyDuration = 0.5f;
    public float countAnimationDuration = 0.8f; // Duration per individual stat count
    public float stampAnimationDuration = 0.35f;
    public float delayBeforeStamp = 1.0f;      // Wait time after stars gained before stamp plays
    public float lossStampDelay = 0.5f;        // On a loss there is no paper: wait this long, then the LOST stamp drops
    public string returnSceneName = "YourOtherSceneName";

    [Header("Boon Meter")]
    [Range(0f, 1f)]
    [Tooltip("How much of the boon meter a WIN fills. The meter starts half full, so 0.5 means the first battle completes it.")]
    public float boonMeterFillOnWin = 0.5f;
    [Range(0f, 1f)]
    [Tooltip("How much of the boon meter a LOSS fills.")]
    public float boonMeterFillOnLoss = 0.5f;

    [Header("Round Settings")]
    [Tooltip("Total number of rounds (hands) in this battle. Permanent 'Extra Hand' boons are added on top of this at startup.")]
    public int maxRounds = 5;
    [Range(0f, 1f)]
    [Tooltip("Transparency of the dim overlay.")]
    public float dimOverlayAlpha = 0.95f;
    public float roundFadeInDuration = 0.3f;
    public float roundDisplayDuration = 1.0f;
    public float roundFadeOutDuration = 0.3f;

    [Header("Battle Target Settings")]
    [Tooltip("Target score at the EASIEST difficulty. Raised automatically as the difficulty meter fills.")]
    public int battleTargetScore = 400;

    [Header("Difficulty")]
    [Tooltip("Target score at max difficulty = Battle Target Score x this. At 0 difficulty it stays at Battle Target Score.")]
    public float maxScoreMultiplier = 3f;

    [Header("Sequence Settings")]
    public float delayBetweenSequences = 0.2f;
    public float fadeDuration = 0.25f;
    [Tooltip("Score To Beat fades out while the number spinner fades in over this time (they overlap).")]
    public float crossFadeDuration = 0.5f;

    [Header("Deck Manager Link")]
    public DeckManager deckManager;

    [Header("Sound Effects")]
    [Range(0f, 1f)] public float sfxVolume = 1f;
    public AudioClip buttonClickSound;
    public AudioClip scoreToBeatSound;
    public AudioClip spinnerResultSound;
    public AudioClip roundIntroSound;
    [Tooltip("Optional. Played instead of the round intro sound on the final round.")]
    public AudioClip finalRoundSound;
    public AudioClip paperSlideSound;
    public AudioClip countTickSound;
    public float countTickInterval = 0.05f;
    public AudioClip statDoneSound;
    public AudioClip stampSound;
    public AudioClip victorySound;
    public AudioClip lossSound;
    public AudioClip exitClickSound;

    [HideInInspector] public bool isOpeningSequenceActive = true;
    [HideInInspector] public bool hasPlayerWon = false;
    [HideInInspector] public bool hasPlayerLost = false;

    private int rolledNumber = 1;
    private int currentRoundNumber = 1;
    private bool hasMatchStartedOnce = false;
    private bool isMatchOver = false;
    private bool isWaitingForClickToExit = false;
    private int scoreBeforeAdvantage;
    private bool advantageApplied;

    private void Awake()
    {
        // Permanent boons: +hands per round (extra scoring attempts)
        maxRounds += BoonProgress.ExtraHands;

        // Mood (Angry): -1 hand
        maxRounds = Mathf.Max(1, maxRounds + MoodManager.HandsModifier);

        // Difficulty meter: the more stars / fights, the higher the score to beat
        battleTargetScore = DifficultyProgress.ScaleScore(battleTargetScore, maxScoreMultiplier);
        Debug.Log("BattleManager: difficulty " + Mathf.RoundToInt(DifficultyProgress.Meter * 100f) + "% -> target score " + battleTargetScore);

        // Tsinelas trinket: rerolls the enemy's defence (the score to beat)
        int beforeReroll = battleTargetScore;
        battleTargetScore = TrinketEffects.RerollEnemyDefence(battleTargetScore);
        if (battleTargetScore != beforeReroll)
            Debug.Log("BattleManager: Tsinelas rerolled the target score " + beforeReroll + " -> " + battleTargetScore);

        // Advantage Hit: you hit the enemy with a card before the fight -> smaller score to beat
        scoreBeforeAdvantage = battleTargetScore;
        advantageApplied = AdvantageHit.BeginBattle();
        if (advantageApplied)
        {
            battleTargetScore = AdvantageHit.Apply(battleTargetScore);
            Debug.Log("BattleManager: Advantage Hit! target score " + scoreBeforeAdvantage + " -> " + battleTargetScore);
        }

        isOpeningSequenceActive = true;
        isMatchOver = false;
        hasPlayerWon = false;
        hasPlayerLost = false;
        isWaitingForClickToExit = false;

        if (scoreToBeatUI != null) SetCanvasGroupState(scoreToBeatUI.gameObject, false, 0f);
        if (numberSpinnerUI != null) SetCanvasGroupState(numberSpinnerUI.gameObject, false, 0f);

        if (roundDimOverlay != null) roundDimOverlay.gameObject.SetActive(false);
        if (roundPopupText != null) roundPopupText.gameObject.SetActive(false);

        if (endingRootObject != null) endingRootObject.SetActive(false);
        if (winGameObject != null) winGameObject.SetActive(false);
        if (lostGameObject != null) lostGameObject.SetActive(false);

        if (currentRoundText1 != null) currentRoundText1.gameObject.SetActive(false);
        if (currentRoundText2 != null) currentRoundText2.gameObject.SetActive(false);

        SetBattleUIInteractable(false);
    }

    private void Update()
    {
        if (isWaitingForClickToExit && Input.GetMouseButtonDown(0))
        {
            isWaitingForClickToExit = false;
            PlaySFX(exitClickSound);
            StartCoroutine(FinalizeAndExitGame());
        }
    }

    private void Start()
    {
        if (deckManager != null)
        {
            deckManager.battleManager = this;
            deckManager.targetScore = battleTargetScore;
            deckManager.currentRemainingScore = battleTargetScore;

            if (deckManager.battleTargetScoreText != null)
                deckManager.battleTargetScoreText.gameObject.SetActive(false);
            if (deckManager.targetScoreText != null)
                deckManager.targetScoreText.gameObject.SetActive(false);
        }

        StartCoroutine(RunBattleOpeningSequence());
    }

    private void OnDestroy()
    {
        AdvantageHit.EndBattle(); // the advantage only lasts for this fight
    }

    public void OnCardSystemButtonClicked()
    {
        if (isOpeningSequenceActive || isMatchOver) return;

        PlaySFX(buttonClickSound);

        if (playerRollUI != null && playerRollUI.hasRolledThisRound)
        {
            SetCameraState(battleViewActive: false);

            if (cardSystemCanvas != null)
            {
                cardSystemCanvas.gameObject.SetActive(true);
            }

            if (deckManager != null)
            {
                deckManager.OpenCardGameView();
            }
        }
        else if (playerRollUI != null)
        {
            SetCameraState(battleViewActive: false);
            playerRollUI.OpenPlayerRollView(rolledNumber);
        }
    }

    public void SetBattleUIInteractable(bool isInteractable)
    {
        GameObject targetObj = battleUIParent;

        if (targetObj == null)
        {
            Canvas battleCanvas = GetComponentInChildren<Canvas>();
            if (battleCanvas != null) targetObj = battleCanvas.gameObject;
        }

        if (targetObj == null) return;

        CanvasGroup canvasGroup = targetObj.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = targetObj.AddComponent<CanvasGroup>();
        }

        canvasGroup.interactable = isInteractable;
        canvasGroup.blocksRaycasts = isInteractable;
    }

    public void SetCameraState(bool battleViewActive)
    {
        if (isMatchOver) return;

        if (battleCamera != null) battleCamera.gameObject.SetActive(battleViewActive);
        if (cardFieldCamera != null) cardFieldCamera.gameObject.SetActive(!battleViewActive);

        if (battleUIParent != null) battleUIParent.SetActive(true);
    }

    public void OnHandCompletedAndReturned()
    {
        if (!hasMatchStartedOnce || isMatchOver) return;

        currentRoundNumber++;

        if (currentRoundNumber <= maxRounds)
        {
            if (playerRollUI != null)
            {
                playerRollUI.OnNewRoundStarted();
            }

            StartCoroutine(PlayRoundIntroSequence(currentRoundNumber));
        }
        else
        {
            Debug.Log("Max rounds reached without hitting target score!");
            TriggerLossCondition();
        }
    }

    public void TriggerVictoryCondition()
    {
        if (isMatchOver) return;
        isMatchOver = true;
        hasPlayerWon = true;
        DifficultyProgress.AddBattle(); // every fight nudges the difficulty meter up
        PlayerProgress.AddStars(starsGainedOnWin);

        // Batteries: Balatro style - a base reward plus a bonus for every hand you had left.
        // They drop in the world (BatteryDropper) when you are back in the game scene.
        int handsLeft = Mathf.Max(0, maxRounds - currentRoundNumber);
        int batteryReward = BatteryManager.CalculateFightReward(handsLeft);
        BatteryManager.QueueDrop(batteryReward);
        Debug.Log("BattleManager: won with " + handsLeft + " hand(s) left -> " + batteryReward + " batteries");

        MoodManager.OnFightEnded(); // timed moods count down
        BoonProgress.AddMeter(boonMeterFillOnWin); // fills the boon meter in the overworld
        SetBattleUIInteractable(false);

        // Trigger the bread-like falling effect when score hits zero
        if (scoreShakeEffect != null)
        {
            scoreShakeEffect.TriggerFallDown();
        }

        int finalYourScore = battleTargetScore - (deckManager != null ? deckManager.currentRemainingScore : 0);
        StartCoroutine(RunEndingSequence(true, battleTargetScore, finalYourScore, starsGainedOnWin));
    }

    public void TriggerLossCondition()
    {
        if (isMatchOver) return;
        isMatchOver = true;
        hasPlayerLost = true;

        PlayerHealth.Lose(1); // losing the card game costs a health card
        MoodManager.OnFightEnded(); // timed moods count down
        DifficultyProgress.AddBattle(); // every fight nudges the difficulty meter up
        BoonProgress.AddMeter(boonMeterFillOnLoss); // fills the boon meter in the overworld
        SetBattleUIInteractable(false);

        int finalYourScore = battleTargetScore - (deckManager != null ? deckManager.currentRemainingScore : 0);
        StartCoroutine(RunEndingSequence(false, battleTargetScore, finalYourScore, 0));
    }

    private IEnumerator RunEndingSequence(bool isWin, int targetScore, int yourScore, int starsGained)
    {
        if (cardSystemCanvas != null) cardSystemCanvas.gameObject.SetActive(false);

        // 1. DIM OVERLAY (Sorting Order 500 - Dims background only)
        if (roundDimOverlay != null)
        {
            roundDimOverlay.gameObject.SetActive(true);
            roundDimOverlay.color = new Color(0f, 0f, 0f, 0.9f);

            Canvas dimCanvas = roundDimOverlay.GetComponent<Canvas>();
            if (dimCanvas == null) dimCanvas = roundDimOverlay.gameObject.AddComponent<Canvas>();
            dimCanvas.overrideSorting = true;
            dimCanvas.sortingOrder = 500;

            if (roundDimOverlay.GetComponent<GraphicRaycaster>() == null)
                roundDimOverlay.gameObject.AddComponent<GraphicRaycaster>();
        }

        // 2. ENDING ROOT & PAPER (Sorting Order 501 - Sits brightly above dim)
        if (endingRootObject != null)
        {
            endingRootObject.SetActive(true);
            ConfigureEndingSortingOrders();
        }

        // Handle Paper Sliding Animation using Anchored Position
        Vector2 finalAnchoredPos = Vector2.zero;
        if (paperImgTransform != null)
        {
            finalAnchoredPos = paperImgTransform.anchoredPosition;
            paperImgTransform.anchoredPosition = new Vector2(finalAnchoredPos.x, finalAnchoredPos.y - 600f);
        }

        if (targetScoreTxt != null) targetScoreTxt.text = "Target Score: 0";
        if (yourScoreTxt != null) yourScoreTxt.text = "Your Score: 0";
        if (starsGainedTxt != null) starsGainedTxt.text = "Stars Gained: 0";

        if (winGameObject != null) winGameObject.SetActive(false);
        if (lostGameObject != null) lostGameObject.SetActive(false);

        // Losing: no paper and no scores, only the LOST stamp drops onto the dimmed screen
        SetPaperVisible(isWin);

        if (isWin)
        {
            PlaySFX(paperSlideSound);

            // Fly PAPERIMG smoothly from below using local anchoredPosition
            float elapsed = 0f;
            Vector2 startAnchoredPos = paperImgTransform != null ? paperImgTransform.anchoredPosition : Vector2.zero;

            while (elapsed < paperFlyDuration && paperImgTransform != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / paperFlyDuration);
                float smoothT = 1f - (1f - t) * (1f - t);
                paperImgTransform.anchoredPosition = Vector2.Lerp(startAnchoredPos, finalAnchoredPos, smoothT);
                yield return null;
            }
            if (paperImgTransform != null) paperImgTransform.anchoredPosition = finalAnchoredPos;

            // 1. COUNT TARGET SCORE
            yield return StartCoroutine(CountStatSequence(targetScoreTxt, "Target Score: ", targetScore));

            // 2. COUNT YOUR SCORE
            yield return StartCoroutine(CountStatSequence(yourScoreTxt, "Your Score: ", yourScore));

            // 3. COUNT STARS GAINED
            yield return StartCoroutine(CountStatSequence(starsGainedTxt, "Stars Gained: ", starsGained));

            // Delay after counting is fully done before the stamp animation plays
            yield return new WaitForSecondsRealtime(delayBeforeStamp);
        }
        else
        {
            yield return new WaitForSecondsRealtime(lossStampDelay);
        }

        // 4. Enable Win or Lost Stamping Object with Stamping Animation (Absolute Top - Sorting Order 503)
        GameObject targetStampObj = isWin ? winGameObject : lostGameObject;
        if (targetStampObj != null)
        {
            targetStampObj.SetActive(true);

            Canvas stampCanvas = targetStampObj.GetComponent<Canvas>();
            if (stampCanvas == null) stampCanvas = targetStampObj.AddComponent<Canvas>();
            stampCanvas.overrideSorting = true;
            stampCanvas.sortingOrder = 503;

            SpriteRenderer[] stampRenders = targetStampObj.GetComponentsInChildren<SpriteRenderer>(true);
            foreach (SpriteRenderer sr in stampRenders)
            {
                sr.sortingOrder = Mathf.Max(sr.sortingOrder, 504);
            }

            yield return StartCoroutine(StampDropAnimation(targetStampObj.transform));
            PlaySFX(stampSound);
            PlaySFX(isWin ? victorySound : lossSound);
        }

        isWaitingForClickToExit = true;
    }

    /// <summary>Shows or hides the paper and its three score lines (only the paper's own picture is hidden, so a stamp placed inside it still shows).</summary>
    private void SetPaperVisible(bool visible)
    {
        if (paperImgTransform != null)
        {
            Graphic g = paperImgTransform.GetComponent<Graphic>();
            if (g != null) g.enabled = visible;
            SpriteRenderer sr = paperImgTransform.GetComponent<SpriteRenderer>();
            if (sr != null) sr.enabled = visible;
        }

        if (targetScoreTxt != null) targetScoreTxt.gameObject.SetActive(visible);
        if (yourScoreTxt != null) yourScoreTxt.gameObject.SetActive(visible);
        if (starsGainedTxt != null) starsGainedTxt.gameObject.SetActive(visible);
    }

    private void ConfigureEndingSortingOrders()
    {
        // 1. Paper / Root Canvas (Sorting Order 501 - Above dim overlay 500)
        if (endingRootObject != null)
        {
            Canvas rootCanvas = endingRootObject.GetComponent<Canvas>();
            if (rootCanvas == null) rootCanvas = endingRootObject.AddComponent<Canvas>();
            rootCanvas.overrideSorting = true;
            rootCanvas.sortingOrder = 501;

            if (endingRootObject.GetComponent<CanvasScaler>() == null)
                endingRootObject.AddComponent<CanvasScaler>();
            if (endingRootObject.GetComponent<GraphicRaycaster>() == null)
                endingRootObject.AddComponent<GraphicRaycaster>();

            SpriteRenderer[] paperRenders = endingRootObject.GetComponentsInChildren<SpriteRenderer>(true);
            foreach (SpriteRenderer sr in paperRenders)
            {
                sr.sortingOrder = Mathf.Max(sr.sortingOrder, 501);
            }
        }

        // 2. Text Canvas/Layer (Sorting Order 502 - Directly above paper)
        SetCanvasOrder(targetScoreTxt, 502);
        SetCanvasOrder(yourScoreTxt, 502);
        SetCanvasOrder(starsGainedTxt, 502);

        // 3. Win / Lose GameObjects (Sorting Order 503 - Absolute front of everything)
        if (winGameObject != null)
        {
            Canvas winCanvas = winGameObject.GetComponent<Canvas>();
            if (winCanvas == null) winCanvas = winGameObject.AddComponent<Canvas>();
            winCanvas.overrideSorting = true;
            winCanvas.sortingOrder = 503;

            SpriteRenderer[] winRenders = winGameObject.GetComponentsInChildren<SpriteRenderer>(true);
            foreach (SpriteRenderer sr in winRenders)
            {
                sr.sortingOrder = Mathf.Max(sr.sortingOrder, 504);
            }
        }

        if (lostGameObject != null)
        {
            Canvas loseCanvas = lostGameObject.GetComponent<Canvas>();
            if (loseCanvas == null) loseCanvas = lostGameObject.AddComponent<Canvas>();
            loseCanvas.overrideSorting = true;
            loseCanvas.sortingOrder = 503;

            SpriteRenderer[] loseRenders = lostGameObject.GetComponentsInChildren<SpriteRenderer>(true);
            foreach (SpriteRenderer sr in loseRenders)
            {
                sr.sortingOrder = Mathf.Max(sr.sortingOrder, 504);
            }
        }
    }

    private void SetCanvasOrder(Component comp, int order)
    {
        if (comp == null) return;
        Canvas canvas = comp.GetComponent<Canvas>();
        if (canvas == null) canvas = comp.gameObject.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = order;

        SpriteRenderer[] renderers = comp.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (SpriteRenderer sr in renderers)
        {
            sr.sortingOrder = Mathf.Max(sr.sortingOrder, order + 1);
        }
    }

    private IEnumerator CountStatSequence(TextMeshProUGUI textComponent, string labelPrefix, int targetValue)
    {
        float elapsed = 0f;
        float tickTimer = 0f;
        while (elapsed < countAnimationDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / countAnimationDuration);
            int currentDisplay = Mathf.RoundToInt(Mathf.Lerp(0, targetValue, t));

            tickTimer += Time.unscaledDeltaTime;
            if (targetValue > 0 && tickTimer >= countTickInterval)
            {
                tickTimer = 0f;
                PlaySFX(countTickSound, Mathf.Lerp(0.9f, 1.3f, t));
            }

            if (textComponent != null)
            {
                textComponent.text = labelPrefix + currentDisplay;
            }
            yield return null;
        }

        if (textComponent != null)
        {
            textComponent.text = labelPrefix + targetValue;
            if (targetValue > 0) PlaySFX(statDoneSound);
            yield return StartCoroutine(WiggleTransform(textComponent.transform));
        }
    }

    private IEnumerator WiggleTransform(Transform targetTransform)
    {
        if (targetTransform == null) yield break;
        Vector3 originalScale = targetTransform.localScale;
        float elapsed = 0f;
        float duration = 0.25f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;
            targetTransform.localScale = originalScale * Mathf.Lerp(1f, 1.25f, Mathf.Sin(t * Mathf.PI));
            yield return null;
        }
        targetTransform.localScale = originalScale;
    }

    private IEnumerator StampDropAnimation(Transform stampTransform)
    {
        if (stampTransform == null) yield break;

        Vector3 originalScale = stampTransform.localScale;
        stampTransform.localScale = originalScale * 2.5f;

        float elapsed = 0f;
        while (elapsed < stampAnimationDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / stampAnimationDuration);
            stampTransform.localScale = Vector3.Lerp(originalScale * 2.5f, originalScale, t * t);
            yield return null;
        }
        stampTransform.localScale = originalScale;
    }

    private IEnumerator FinalizeAndExitGame()
    {
        yield return new WaitForSecondsRealtime(0.1f);
        Time.timeScale = 1f;

        if (BattleTransition.IsPlaying)
        {
            // Safety: never get stuck in the card game
            SceneManager.LoadScene(returnSceneName);
        }
        else
        {
            // Fade to black, load the game scene under the black, then fade back in
            BattleTransition.PlayFade(() => SceneManager.LoadScene(returnSceneName), 0.6f);
        }
    }

    private void PlaySFX(AudioClip clip, float pitch = 1f)
    {
        GameSFX.Play(clip, sfxVolume, pitch);
    }

    public void UpdateBattleTargetScoreDisplay(int remainingScore)
    {
        if (scoreToBeatUI != null && scoreToBeatUI.scoreText != null)
        {
            scoreToBeatUI.scoreText.text = Mathf.Max(0, remainingScore).ToString();
        }
    }

    public void UpdateCurrentRoundDisplays()
    {
        string roundString = (currentRoundNumber >= maxRounds) ? "Final Round" : "Round " + currentRoundNumber;

        if (currentRoundText1 != null)
            currentRoundText1.text = roundString;

        if (currentRoundText2 != null)
            currentRoundText2.text = roundString;
    }

    private IEnumerator RunBattleOpeningSequence()
    {
        isOpeningSequenceActive = true;
        SetBattleUIInteractable(false);

        SetCameraState(battleViewActive: true);
        SetPersistentUITextsActive(false);

        if (scoreToBeatUI != null)
        {
            yield return StartCoroutine(FadeCanvasGroup(scoreToBeatUI.gameObject, 0f, 1f, fadeDuration));

            PlaySFX(scoreToBeatSound);
            bool isScoreComplete = false;
            scoreToBeatUI.StartSequence(battleTargetScore, () => isScoreComplete = true, advantageApplied ? scoreBeforeAdvantage : 0);
            while (!isScoreComplete) yield return null;
        }

        yield return new WaitForSecondsRealtime(delayBetweenSequences);

        // Cross-fade: Score To Beat fades out while the number spinner fades in (and starts spinning) at the same time
        Coroutine scoreFadeOut = null;
        if (scoreToBeatUI != null)
        {
            scoreFadeOut = StartCoroutine(FadeCanvasGroup(scoreToBeatUI.gameObject, 1f, 0f, crossFadeDuration));
        }

        if (numberSpinnerUI != null)
        {
            numberSpinnerUI.SetNumberLimits(1, 10);
            Coroutine spinnerFadeIn = StartCoroutine(FadeCanvasGroup(numberSpinnerUI.gameObject, 0f, 1f, crossFadeDuration));

            bool isSpinnerComplete = false;
            numberSpinnerUI.StartSequence((resultNumber) =>
            {
                rolledNumber = resultNumber;
                PlaySFX(spinnerResultSound);
                isSpinnerComplete = true;
            }, true); // the fade-in above controls the alpha
            while (!isSpinnerComplete) yield return null;
            yield return spinnerFadeIn;
        }

        if (scoreFadeOut != null) yield return scoreFadeOut;

        if (deckManager != null)
        {
            deckManager.targetScore = battleTargetScore;
            deckManager.currentRemainingScore = battleTargetScore;
            UpdateBattleTargetScoreDisplay(battleTargetScore);
        }

        yield return new WaitForSecondsRealtime(0.4f);

        currentRoundNumber = 1;
        UpdateCurrentRoundDisplays();

        if (playerRollUI != null)
        {
            playerRollUI.OnNewRoundStarted();
        }

        yield return StartCoroutine(PlayRoundIntroSequence(currentRoundNumber));

        hasMatchStartedOnce = true;
        isOpeningSequenceActive = false;

        SetCameraState(battleViewActive: true);
        SetPersistentUITextsActive(true);
        SetBattleUIInteractable(true);
    }

    public IEnumerator PlayRoundIntroSequence(int roundNum)
    {
        if (roundDimOverlay == null || roundPopupText == null || isMatchOver) yield break;

        currentRoundNumber = roundNum;
        UpdateCurrentRoundDisplays();

        SetBattleUIInteractable(false);

        RectTransform textRect = roundPopupText.GetComponent<RectTransform>();
        if (textRect != null)
        {
            textRect.anchorMin = new Vector2(0.5f, 0.5f);
            textRect.anchorMax = new Vector2(0.5f, 0.5f);
            textRect.anchoredPosition = Vector2.zero;
        }

        roundPopupText.text = "ROUND " + roundNum + "/" + maxRounds;
        roundPopupText.alignment = TextAlignmentOptions.Center;

        PlaySFX((roundNum >= maxRounds && finalRoundSound != null) ? finalRoundSound : roundIntroSound);

        roundDimOverlay.gameObject.SetActive(true);
        roundPopupText.gameObject.SetActive(true);

        Color dimColor = roundDimOverlay.color;
        Color textColor = roundPopupText.color;

        textColor.r = 1f; textColor.g = 1f; textColor.b = 1f;
        dimColor.a = 0f; textColor.a = 0f;
        roundDimOverlay.color = dimColor;
        roundPopupText.color = textColor;

        float elapsed = 0f;
        while (elapsed < roundFadeInDuration)
        {
            if (isMatchOver) yield break;
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / roundFadeInDuration;

            dimColor.a = Mathf.Lerp(0f, dimOverlayAlpha, t);
            textColor.a = Mathf.Lerp(0f, 1f, t);

            roundDimOverlay.color = dimColor;
            roundPopupText.color = textColor;
            yield return null;
        }

        yield return new WaitForSecondsRealtime(roundDisplayDuration);

        elapsed = 0f;
        while (elapsed < roundFadeOutDuration)
        {
            if (isMatchOver) yield break;
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / roundFadeOutDuration;

            dimColor.a = Mathf.Lerp(dimOverlayAlpha, 0f, t);
            textColor.a = Mathf.Lerp(1f, 0f, t);

            roundDimOverlay.color = dimColor;
            roundPopupText.color = textColor;
            yield return null;
        }

        roundDimOverlay.gameObject.SetActive(false);
        roundPopupText.gameObject.SetActive(false);

        if (!isOpeningSequenceActive)
        {
            SetBattleUIInteractable(true);
        }
    }

    private IEnumerator FadeCanvasGroup(GameObject targetObj, float startAlpha, float endAlpha, float duration)
    {
        if (targetObj == null) yield break;

        CanvasGroup canvasGroup = targetObj.GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = targetObj.AddComponent<CanvasGroup>();

        targetObj.SetActive(true);
        canvasGroup.alpha = startAlpha;

        float elapsedTime = 0f;
        while (elapsedTime < duration)
        {
            elapsedTime += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.SmoothStep(startAlpha, endAlpha, Mathf.Clamp01(elapsedTime / duration)); // eased, so it starts and ends softly
            yield return null;
        }

        canvasGroup.alpha = endAlpha;
        if (endAlpha <= 0f) targetObj.SetActive(false);
    }

    private void SetCanvasGroupState(GameObject targetObj, bool isActive, float alpha)
    {
        if (targetObj == null) return;
        targetObj.SetActive(isActive);
        CanvasGroup cg = targetObj.GetComponent<CanvasGroup>();
        if (cg == null) cg = targetObj.AddComponent<CanvasGroup>();
        cg.alpha = alpha;
    }

    public void SetPersistentUITextsActive(bool isActive)
    {
        if (isOpeningSequenceActive && isActive) return;
        if (deckManager == null) return;

        if (deckManager.battleTargetScoreText != null)
            deckManager.battleTargetScoreText.gameObject.SetActive(isActive);

        if (deckManager.targetScoreText != null)
            deckManager.targetScoreText.gameObject.SetActive(isActive);

        if (currentRoundText1 != null)
            currentRoundText1.gameObject.SetActive(isActive);

        if (currentRoundText2 != null)
            currentRoundText2.gameObject.SetActive(isActive);

        if (isActive)
        {
            deckManager.UpdateTargetScoreDisplay();
            UpdateCurrentRoundDisplays();
        }
    }
}