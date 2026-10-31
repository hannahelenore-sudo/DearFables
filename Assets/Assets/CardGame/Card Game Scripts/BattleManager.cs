using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

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

    [Header("Current Round Text References")]
    public TextMeshProUGUI currentRoundText1;
    public TextMeshProUGUI currentRoundText2;

    [Tooltip("Panel or Image used to dim the screen for the round intro.")]
    public Image roundDimOverlay;

    [Tooltip("TextMeshProUGUI element that displays text like 'ROUND 1/5'.")]
    public TextMeshProUGUI roundPopupText;

    [Header("Ending Sequence UI References")]
    public GameObject endingRootObject;         // The ENDING parent GameObject
    public Transform paperImgTransform;         // PAPERIMG Transform (SpriteRenderer object)
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

    [Header("Round Settings")]
    [Tooltip("Total number of rounds in this battle.")]
    public int maxRounds = 5;
    [Range(0f, 1f)]
    [Tooltip("Transparency of the dim overlay.")]
    public float dimOverlayAlpha = 0.95f;
    public float roundFadeInDuration = 0.3f;
    public float roundDisplayDuration = 1.0f;
    public float roundFadeOutDuration = 0.3f;

    [Header("Battle Target Settings")]
    public int battleTargetScore = 400;

    [Header("Sequence Settings")]
    public float delayBetweenSequences = 0.2f;
    public float fadeDuration = 0.25f;

    [Header("Deck Manager Link")]
    public DeckManager deckManager;

    [HideInInspector] public bool isOpeningSequenceActive = true;
    [HideInInspector] public bool hasPlayerWon = false;
    [HideInInspector] public bool hasPlayerLost = false;

    private int rolledNumber = 1;
    private int currentRoundNumber = 1;
    private bool hasMatchStartedOnce = false;
    private bool isMatchOver = false;
    private bool isWaitingForClickToExit = false;

    private void Awake()
    {
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

    public void OnCardSystemButtonClicked()
    {
        if (isOpeningSequenceActive || isMatchOver) return;

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
        SetBattleUIInteractable(false);

        int finalYourScore = battleTargetScore - (deckManager != null ? deckManager.currentRemainingScore : 0);
        StartCoroutine(RunEndingSequence(true, battleTargetScore, finalYourScore, starsGainedOnWin));
    }

    public void TriggerLossCondition()
    {
        if (isMatchOver) return;
        isMatchOver = true;
        hasPlayerLost = true;
        SetBattleUIInteractable(false);

        int finalYourScore = battleTargetScore - (deckManager != null ? deckManager.currentRemainingScore : 0);
        StartCoroutine(RunEndingSequence(false, battleTargetScore, finalYourScore, 0));
    }

    private IEnumerator RunEndingSequence(bool isWin, int targetScore, int yourScore, int starsGained)
    {
        if (cardSystemCanvas != null) cardSystemCanvas.gameObject.SetActive(false);
        if (roundDimOverlay != null)
        {
            roundDimOverlay.gameObject.SetActive(true);
            roundDimOverlay.color = new Color(0f, 0f, 0f, 0.9f);

            // Dim overlay sits at sorting order 10 to dim the background canvas battle
            Canvas dimCanvas = roundDimOverlay.GetComponent<Canvas>();
            if (dimCanvas == null) dimCanvas = roundDimOverlay.gameObject.AddComponent<Canvas>();
            dimCanvas.overrideSorting = true;
            dimCanvas.sortingOrder = 10;

            if (roundDimOverlay.GetComponent<GraphicRaycaster>() == null)
                roundDimOverlay.gameObject.AddComponent<GraphicRaycaster>();
        }

        if (endingRootObject != null)
        {
            endingRootObject.SetActive(true);
            ConfigureEndingSortingOrders();
        }

        Vector3 finalPaperPos = Vector3.zero;
        if (paperImgTransform != null)
        {
            finalPaperPos = paperImgTransform.position;
            paperImgTransform.position = new Vector3(finalPaperPos.x, finalPaperPos.y - 15f, finalPaperPos.z);
        }

        if (targetScoreTxt != null) targetScoreTxt.text = "Target Score: 0";
        if (yourScoreTxt != null) yourScoreTxt.text = "Your Score: 0";
        if (starsGainedTxt != null) starsGainedTxt.text = "Stars Gained: 0";

        if (winGameObject != null) winGameObject.SetActive(false);
        if (lostGameObject != null) lostGameObject.SetActive(false);

        // Fly PAPERIMG from below
        float elapsed = 0f;
        Vector3 startPaperPos = paperImgTransform != null ? paperImgTransform.position : Vector3.zero;

        while (elapsed < paperFlyDuration && paperImgTransform != null)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / paperFlyDuration);
            float smoothT = 1f - (1f - t) * (1f - t);
            paperImgTransform.position = Vector3.Lerp(startPaperPos, finalPaperPos, smoothT);
            yield return null;
        }
        if (paperImgTransform != null) paperImgTransform.position = finalPaperPos;

        // 1. COUNT TARGET SCORE
        yield return StartCoroutine(CountStatSequence(targetScoreTxt, "Target Score: ", targetScore));

        // 2. COUNT YOUR SCORE
        yield return StartCoroutine(CountStatSequence(yourScoreTxt, "Your Score: ", yourScore));

        // 3. COUNT STARS GAINED
        yield return StartCoroutine(CountStatSequence(starsGainedTxt, "Stars Gained: ", starsGained));

        // Delay after counting is fully done before the stamp animation plays
        yield return new WaitForSecondsRealtime(delayBeforeStamp);

        // 4. Enable Win or Lost Stamping Object with Stamping Animation (Absolute Top)
        GameObject targetStampObj = isWin ? winGameObject : lostGameObject;
        if (targetStampObj != null)
        {
            targetStampObj.SetActive(true);

            // Stamp canvas at sorting order 30 (front of ending sequence)
            Canvas stampCanvas = targetStampObj.GetComponent<Canvas>();
            if (stampCanvas == null) stampCanvas = targetStampObj.AddComponent<Canvas>();
            stampCanvas.overrideSorting = true;
            stampCanvas.sortingOrder = 30;

            yield return StartCoroutine(StampDropAnimation(targetStampObj.transform));
        }

        isWaitingForClickToExit = true;
    }

    private void ConfigureEndingSortingOrders()
    {
        // 1. Paper / Root Canvas (Sorting Order 20 - Back of ending sequence)
        if (endingRootObject != null)
        {
            Canvas rootCanvas = endingRootObject.GetComponent<Canvas>();
            if (rootCanvas == null) rootCanvas = endingRootObject.AddComponent<Canvas>();
            rootCanvas.overrideSorting = true;
            rootCanvas.sortingOrder = 20;

            if (endingRootObject.GetComponent<CanvasScaler>() == null)
                endingRootObject.AddComponent<CanvasScaler>();
            if (endingRootObject.GetComponent<GraphicRaycaster>() == null)
                endingRootObject.AddComponent<GraphicRaycaster>();
        }

        // 2. Text Canvas/Layer (Sorting Order 25 - Directly above the paper)
        SetCanvasOrder(targetScoreTxt, 25);
        SetCanvasOrder(yourScoreTxt, 25);
        SetCanvasOrder(starsGainedTxt, 25);

        // 3. Win / Lose GameObjects (Sorting Order 30 - Absolute front of ending sequence)
        if (winGameObject != null)
        {
            Canvas winCanvas = winGameObject.GetComponent<Canvas>();
            if (winCanvas == null) winCanvas = winGameObject.AddComponent<Canvas>();
            winCanvas.overrideSorting = true;
            winCanvas.sortingOrder = 30;
        }

        if (lostGameObject != null)
        {
            Canvas loseCanvas = lostGameObject.GetComponent<Canvas>();
            if (loseCanvas == null) loseCanvas = lostGameObject.AddComponent<Canvas>();
            loseCanvas.overrideSorting = true;
            loseCanvas.sortingOrder = 30;
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
        while (elapsed < countAnimationDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / countAnimationDuration);
            int currentDisplay = Mathf.RoundToInt(Mathf.Lerp(0, targetValue, t));

            if (textComponent != null)
            {
                textComponent.text = labelPrefix + currentDisplay;
            }
            yield return null;
        }

        if (textComponent != null)
        {
            textComponent.text = labelPrefix + targetValue;
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
        Debug.Log("Ending sequence completed. Exiting game...");
        yield return new WaitForSecondsRealtime(0.1f);

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
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

            bool isScoreComplete = false;
            scoreToBeatUI.StartSequence(battleTargetScore, () => isScoreComplete = true);
            while (!isScoreComplete) yield return null;
        }

        yield return new WaitForSecondsRealtime(delayBetweenSequences);

        if (scoreToBeatUI != null)
        {
            yield return StartCoroutine(FadeCanvasGroup(scoreToBeatUI.gameObject, 1f, 0f, fadeDuration));
        }

        if (numberSpinnerUI != null)
        {
            numberSpinnerUI.SetNumberLimits(1, 10);
            yield return StartCoroutine(FadeCanvasGroup(numberSpinnerUI.gameObject, 0f, 1f, fadeDuration));

            bool isSpinnerComplete = false;
            numberSpinnerUI.StartSequence((resultNumber) =>
            {
                rolledNumber = resultNumber;
                isSpinnerComplete = true;
            });
            while (!isSpinnerComplete) yield return null;
        }

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
            canvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, elapsedTime / duration);
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