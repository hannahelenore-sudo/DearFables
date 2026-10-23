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

    [Tooltip("Panel or Image used to dim the screen for the round intro.")]
    public Image roundDimOverlay;

    [Tooltip("TextMeshProUGUI element that displays text like 'ROUND 1/5'.")]
    public TextMeshProUGUI roundPopupText;

    [Header("Round Settings")]
    [Tooltip("Total number of rounds in this battle.")]
    public int maxRounds = 5;
    [Range(0f, 1f)]
    [Tooltip("Transparency of the dim overlay.")]
    public float dimOverlayAlpha = 0.75f;
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

    private void Awake()
    {
        isOpeningSequenceActive = true;
        isMatchOver = false;
        hasPlayerWon = false;
        hasPlayerLost = false;

        if (scoreToBeatUI != null) SetCanvasGroupState(scoreToBeatUI.gameObject, false, 0f);
        if (numberSpinnerUI != null) SetCanvasGroupState(numberSpinnerUI.gameObject, false, 0f);

        if (roundDimOverlay != null) roundDimOverlay.gameObject.SetActive(false);
        if (roundPopupText != null) roundPopupText.gameObject.SetActive(false);

        
        SetBattleUIInteractable(false);
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
        StartCoroutine(ExitGameRoutine("Victory! Target score reached. Exiting..."));
    }

    public void TriggerLossCondition()
    {
        if (isMatchOver) return;
        isMatchOver = true;
        hasPlayerLost = true;
        SetBattleUIInteractable(false);
        StartCoroutine(ExitGameRoutine("Failed! Out of rounds. Exiting..."));
    }

    private IEnumerator ExitGameRoutine(string logMessage)
    {
        if (roundDimOverlay != null) roundDimOverlay.gameObject.SetActive(false);
        if (roundPopupText != null) roundPopupText.gameObject.SetActive(false);

        Debug.Log(logMessage);
        yield return new WaitForSecondsRealtime(1.5f);

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
            scoreToBeatUI.scoreText.text = "Score To Beat:\n" + Mathf.Max(0, remainingScore).ToString();
        }
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

        if (isActive)
        {
            deckManager.UpdateTargetScoreDisplay();
        }
    }
}