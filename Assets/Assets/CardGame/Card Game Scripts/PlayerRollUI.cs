using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerRollUI : MonoBehaviour
{
    [Header("UI Canvas & Visuals")]
    public Canvas playerRollCanvas;
    public CanvasGroup rollCanvasGroup;
    public Image dimOverlay;
    public TextMeshProUGUI playerNumberText;
    public Image playerShieldImage;
    public TextMeshProUGUI enemyNumberText;
    public Image enemyShieldImage;
    [Tooltip("Text object shown on the roll screen on high/low rolls.")]
    public TextMeshProUGUI notificationText;

    [Header("Shield State Images")]
    [Tooltip("Difference within this range counts as a Tie/Close roll during normal spins.")]
    public int tieThreshold = 1;
    public Sprite tieShieldSprite;
    public Sprite lowRollShieldSprite;
    public Sprite highRollShieldSprite;

    [Header("High Roll Confetti Settings")]
    [Tooltip("Sprite of the star used for confetti falling on high rolls.")]
    public Sprite starConfettiSprite;
    public float confettiSpawnInterval = 0.15f;
    public float confettiFallDuration = 2.0f;

    [Header("Manager References")]
    public BattleManager battleManager;
    public DeckManager deckManager;

    [Header("Interactive Buttons")]
    public Button rollTriggerButton;
    public Button backButton;
    public Button skipButton;

    [Header("Spin & Roll Settings")]
    public float spinDuration = 1.2f;
    public float spinSpeed = 0.05f;
    public float lingerDuration = 1.5f;
    public int minRoll = 1;
    public int maxRoll = 10;

    [Header("Pop & Wiggle Settings")]
    public RectTransform playerRollContainer;
    public float popScaleMultiplier = 1.35f;
    public float wiggleAngle = 10f;
    public float wiggleDuration = 0.35f;

    [Header("Fade Settings")]
    public float fadeInDuration = 0.3f;
    public float fadeOutDuration = 0.3f;

    [HideInInspector] public bool hasRolledThisRound = false;
    [HideInInspector] public static bool isHighRollActive = false;
    [HideInInspector] public static bool isLowRollActive = false;

    private bool isRolling = false;
    private int currentEnemyDefense = 1;
    private Vector3 initialContainerScale = Vector3.one;
    private Sprite defaultShieldSprite;
    private Coroutine confettiRoutine;
    private Coroutine shieldSpinRoutine;
    private List<GameObject> activeConfettiList = new List<GameObject>();
    private float currentShieldZRotation = 0f;

    private void Awake()
    {
        if (rollCanvasGroup == null && playerRollCanvas != null)
        {
            rollCanvasGroup = playerRollCanvas.GetComponent<CanvasGroup>();
            if (rollCanvasGroup == null) rollCanvasGroup = playerRollCanvas.gameObject.AddComponent<CanvasGroup>();
        }

        if (playerShieldImage != null)
        {
            defaultShieldSprite = playerShieldImage.sprite;
        }

        if (playerRollContainer != null)
        {
            initialContainerScale = playerRollContainer.localScale;
        }

        if (rollTriggerButton != null)
        {
            rollTriggerButton.onClick.RemoveAllListeners();
            rollTriggerButton.onClick.AddListener(OnRollButtonClicked);
        }

        if (backButton != null)
        {
            backButton.onClick.RemoveAllListeners();
            backButton.onClick.AddListener(OnBackButtonClicked);
        }

        if (skipButton != null)
        {
            skipButton.onClick.RemoveAllListeners();
            skipButton.onClick.AddListener(SkipRoll);
        }

        if (notificationText != null)
        {
            notificationText.gameObject.SetActive(false);
        }
    }

    public void OpenPlayerRollView(int enemyRolledDefense)
    {
        if (hasRolledThisRound) return;

        currentEnemyDefense = enemyRolledDefense;
        isHighRollActive = false;
        isLowRollActive = false;
        CleanupConfetti();

        currentShieldZRotation = 0f;
        if (playerShieldImage != null)
        {
            if (defaultShieldSprite != null) playerShieldImage.sprite = defaultShieldSprite;
            playerShieldImage.color = new Color(1f, 1f, 1f, 1f);
            playerShieldImage.rectTransform.localRotation = Quaternion.Euler(0, 0, 0);
        }

        if (playerNumberText != null)
        {
            playerNumberText.color = new Color(playerNumberText.color.r, playerNumberText.color.g, playerNumberText.color.b, 1f);
        }

        if (notificationText != null)
        {
            notificationText.text = "";
            notificationText.gameObject.SetActive(false);
        }

        if (playerRollCanvas != null)
        {
            playerRollCanvas.gameObject.SetActive(true);
        }

        if (dimOverlay != null)
        {
            dimOverlay.gameObject.SetActive(true);
            Color dimColor = dimOverlay.color;
            dimColor.a = 0.75f;
            dimOverlay.color = dimColor;
        }

        if (enemyNumberText != null) enemyNumberText.text = currentEnemyDefense.ToString();
        if (playerNumberText != null)
        {
            playerNumberText.text = "?";
            playerNumberText.gameObject.SetActive(true);
        }

        SetButtonsInteractable(true);
        isRolling = false;

        StartCoroutine(FadeCanvasGroupRoutine(0f, 1f, fadeInDuration));
    }

    public void OnRollButtonClicked()
    {
        if (isRolling || hasRolledThisRound) return;

        SetButtonsInteractable(false);
        StartCoroutine(AnimatePlayerRollRoutine());
    }

    public void OnBackButtonClicked()
    {
        if (isRolling) return;

        StopAllCoroutines();
        CleanupConfetti();
        isRolling = false;

        StartCoroutine(CloseAndReturnToBattleView());
    }

    public void SkipRoll()
    {
        if (hasRolledThisRound) return;

        StopAllCoroutines();
        CleanupConfetti();

        hasRolledThisRound = true;
        isRolling = false;
        isHighRollActive = false;
        isLowRollActive = false;

        SetButtonsInteractable(false);

        if (playerRollContainer != null)
        {
            playerRollContainer.localRotation = Quaternion.identity;
            playerRollContainer.localScale = initialContainerScale;
        }

        currentShieldZRotation = 0f;
        if (playerShieldImage != null)
        {
            playerShieldImage.rectTransform.localRotation = Quaternion.Euler(0, 0, 0);
        }

        int finalRolledValue = currentEnemyDefense;

        if (playerNumberText != null)
        {
            playerNumberText.text = finalRolledValue.ToString();
            playerNumberText.color = new Color(playerNumberText.color.r, playerNumberText.color.g, playerNumberText.color.b, 1f);
            playerNumberText.gameObject.SetActive(true);
        }

        if (playerShieldImage != null)
        {
            playerShieldImage.sprite = tieShieldSprite != null ? tieShieldSprite : defaultShieldSprite;
            playerShieldImage.color = new Color(1f, 1f, 1f, 1f);
        }

        StartCoroutine(SkipRollRoutine(finalRolledValue));
    }

    private IEnumerator SkipRollRoutine(int finalRolledValue)
    {
        float currentAlpha = rollCanvasGroup != null ? rollCanvasGroup.alpha : 1f;
        yield return StartCoroutine(FadeCanvasGroupRoutine(currentAlpha, 0f, fadeOutDuration));

        CompleteRollSequence(finalRolledValue);
    }

    private IEnumerator AnimatePlayerRollRoutine()
    {
        isRolling = true;
        float elapsedTime = 0f;

        if (playerShieldImage != null)
        {
            playerShieldImage.color = new Color(1f, 1f, 1f, 1f);
        }

        if (playerNumberText != null)
        {
            playerNumberText.color = new Color(playerNumberText.color.r, playerNumberText.color.g, playerNumberText.color.b, 1f);
        }

        while (elapsedTime < spinDuration)
        {
            int randomDisplay = Random.Range(minRoll, maxRoll + 1);

            if (playerNumberText != null)
            {
                playerNumberText.text = randomDisplay.ToString();
                playerNumberText.gameObject.SetActive(true);
            }

            yield return new WaitForSecondsRealtime(spinSpeed);
            elapsedTime += spinSpeed;
        }

        int finalRolledValue = Random.Range(minRoll, maxRoll + 1);

        if (playerNumberText != null)
        {
            playerNumberText.text = finalRolledValue.ToString();
            playerNumberText.color = new Color(playerNumberText.color.r, playerNumberText.color.g, playerNumberText.color.b, 1f);
            playerNumberText.gameObject.SetActive(true);
        }

        EvaluateShieldState(finalRolledValue);

        yield return StartCoroutine(PopAndWiggleRoutine());

        yield return new WaitForSecondsRealtime(lingerDuration);

        hasRolledThisRound = true;

        CleanupConfetti();
        if (shieldSpinRoutine != null)
        {
            StopCoroutine(shieldSpinRoutine);
            shieldSpinRoutine = null;
            currentShieldZRotation = 0f;
            if (playerShieldImage != null) playerShieldImage.rectTransform.localRotation = Quaternion.Euler(0, 0, 0);
        }

        yield return StartCoroutine(FadeCanvasGroupRoutine(1f, 0f, fadeOutDuration));

        CompleteRollSequence(finalRolledValue);
    }

    private void EvaluateShieldState(int playerRoll)
    {
        if (playerShieldImage == null) return;

        int difference = playerRoll - currentEnemyDefense;

        playerShieldImage.color = new Color(1f, 1f, 1f, 1f);
        if (playerNumberText != null)
        {
            playerNumberText.color = new Color(playerNumberText.color.r, playerNumberText.color.g, playerNumberText.color.b, 1f);
        }

        if (Mathf.Abs(difference) <= tieThreshold)
        {
            if (tieShieldSprite != null) playerShieldImage.sprite = tieShieldSprite;
            isHighRollActive = false;
            isLowRollActive = false;
        }
        else if (difference < -tieThreshold)
        {
            if (lowRollShieldSprite != null) playerShieldImage.sprite = lowRollShieldSprite;
            isHighRollActive = false;
            isLowRollActive = true;

            TriggerLowRollEffects();
        }
        else
        {
            if (highRollShieldSprite != null) playerShieldImage.sprite = highRollShieldSprite;
            isHighRollActive = true;
            isLowRollActive = false;

            TriggerHighRollEffects();
        }
    }

    private void TriggerLowRollEffects()
    {
        if (notificationText != null)
        {
            notificationText.text = "-10%";
            notificationText.gameObject.SetActive(true);
        }
    }

    private void TriggerHighRollEffects()
    {
        if (notificationText != null)
        {
            notificationText.text = "X1.5 on this round";
            notificationText.gameObject.SetActive(true);
        }

        if (playerRollCanvas != null && starConfettiSprite != null)
        {
            if (confettiRoutine != null) StopCoroutine(confettiRoutine);
            confettiRoutine = StartCoroutine(InfiniteConfettiLoop());
        }

        if (playerShieldImage != null)
        {
            if (shieldSpinRoutine != null) StopCoroutine(shieldSpinRoutine);
            shieldSpinRoutine = StartCoroutine(SpinShieldRoutine());
        }
    }

    private IEnumerator InfiniteConfettiLoop()
    {
        while (true)
        {
            SpawnSingleConfetti();
            yield return new WaitForSecondsRealtime(confettiSpawnInterval);
        }
    }

    private void SpawnSingleConfetti()
    {
        if (playerRollCanvas == null || starConfettiSprite == null) return;

        RectTransform canvasRect = playerRollCanvas.GetComponent<RectTransform>();

        GameObject confettiObj = new GameObject("StarConfetti", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        confettiObj.transform.SetParent(playerRollCanvas.transform, false);

        Image img = confettiObj.GetComponent<Image>();
        img.sprite = starConfettiSprite;
        img.raycastTarget = false;

        RectTransform rect = confettiObj.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(Random.Range(30f, 65f), Random.Range(30f, 65f));

        float randomX = Random.Range(-canvasRect.rect.width / 2f, canvasRect.rect.width / 2f);
        float startY = canvasRect.rect.height / 2f + 50f;
        rect.anchoredPosition = new Vector2(randomX, startY);

        activeConfettiList.Add(confettiObj);
        StartCoroutine(FallConfettiPiece(rect, confettiFallDuration));
    }

    private IEnumerator FallConfettiPiece(RectTransform rect, float duration)
    {
        float elapsed = 0f;
        float startY = rect != null ? rect.anchoredPosition.y : 0f;
        float endY = -700f;
        float rotationSpeed = Random.Range(-180f, 180f);
        float swaySpeed = Random.Range(2f, 5f);
        float swayAmount = Random.Range(20f, 60f);
        float startX = rect != null ? rect.anchoredPosition.x : 0f;

        while (elapsed < duration && rect != null)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;

            float currentY = Mathf.Lerp(startY, endY, t);
            float currentX = startX + Mathf.Sin(t * Mathf.PI * swaySpeed) * swayAmount;

            rect.anchoredPosition = new Vector2(currentX, currentY);
            rect.Rotate(0, 0, rotationSpeed * Time.unscaledDeltaTime);

            yield return null;
        }

        if (rect != null)
        {
            activeConfettiList.Remove(rect.gameObject);
            Destroy(rect.gameObject);
        }
    }

    private IEnumerator SpinShieldRoutine()
    {
        while (playerShieldImage != null)
        {
            currentShieldZRotation -= 120f * Time.unscaledDeltaTime;
            playerShieldImage.rectTransform.localRotation = Quaternion.Euler(0, 0, currentShieldZRotation);

            if (playerNumberText != null)
            {
                playerNumberText.color = new Color(playerNumberText.color.r, playerNumberText.color.g, playerNumberText.color.b, 1f);
                playerNumberText.gameObject.SetActive(true);
            }

            yield return null;
        }
    }

    private void CleanupConfetti()
    {
        if (confettiRoutine != null)
        {
            StopCoroutine(confettiRoutine);
            confettiRoutine = null;
        }

        foreach (GameObject confetti in activeConfettiList)
        {
            if (confetti != null) Destroy(confetti);
        }
        activeConfettiList.Clear();
    }

    private IEnumerator PopAndWiggleRoutine()
    {
        if (playerRollContainer == null) yield break;

        Vector3 baseScale = initialContainerScale;
        Vector3 maxPop = baseScale * popScaleMultiplier;

        float elapsed = 0f;
        while (elapsed < 0.1f)
        {
            elapsed += Time.unscaledDeltaTime;
            playerRollContainer.localScale = Vector3.Lerp(baseScale, maxPop, elapsed / 0.1f);
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < wiggleDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / wiggleDuration);
            float currentWiggle = Mathf.Sin(progress * Mathf.PI * 8f) * wiggleAngle * (1f - progress);

            playerRollContainer.localRotation = Quaternion.Euler(0, 0, currentWiggle);
            playerRollContainer.localScale = Vector3.Lerp(maxPop, baseScale, progress);
            yield return null;
        }

        playerRollContainer.localRotation = Quaternion.identity;
        playerRollContainer.localScale = baseScale;
    }

    private IEnumerator FadeCanvasGroupRoutine(float startAlpha, float endAlpha, float duration)
    {
        if (rollCanvasGroup == null) yield break;

        rollCanvasGroup.alpha = startAlpha;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            rollCanvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, elapsed / duration);
            yield return null;
        }

        rollCanvasGroup.alpha = endAlpha;
    }

    private IEnumerator CloseAndReturnToBattleView()
    {
        yield return StartCoroutine(FadeCanvasGroupRoutine(1f, 0f, fadeOutDuration));

        if (dimOverlay != null) dimOverlay.gameObject.SetActive(false);
        if (playerRollCanvas != null) playerRollCanvas.gameObject.SetActive(false);

        if (battleManager != null)
        {
            battleManager.SetCameraState(battleViewActive: true);
            battleManager.SetBattleUIInteractable(true);
        }
    }

    private void CompleteRollSequence(int rolledValue)
    {
        isRolling = false;
        CleanupConfetti();

        if (dimOverlay != null) dimOverlay.gameObject.SetActive(false);
        if (playerRollCanvas != null) playerRollCanvas.gameObject.SetActive(false);

        DirectToCardSystem();
    }

    private void DirectToCardSystem()
    {
        if (battleManager != null)
        {
            battleManager.SetCameraState(battleViewActive: false);

            if (battleManager.cardSystemCanvas != null)
            {
                battleManager.cardSystemCanvas.gameObject.SetActive(true);
            }
        }

        DeckManager targetDeckManager = deckManager;

        if (targetDeckManager == null)
        {
            targetDeckManager = Object.FindAnyObjectByType<DeckManager>();
        }

        if (targetDeckManager != null)
        {
            targetDeckManager.OpenCardGameView();
        }
        else
        {
            Debug.LogError("[PlayerRollUI] DeckManager reference could not be found in the scene!", this);
        }
    }

    private void SetButtonsInteractable(bool state)
    {
        if (rollTriggerButton != null) rollTriggerButton.interactable = state;
        if (backButton != null) backButton.interactable = state;
        if (skipButton != null) skipButton.interactable = state;
    }

    public void OnNewRoundStarted()
    {
        isRolling = false;
        hasRolledThisRound = false;
        isHighRollActive = false;
        isLowRollActive = false;
        CleanupConfetti();

        currentShieldZRotation = 0f;
        if (playerShieldImage != null)
        {
            if (defaultShieldSprite != null) playerShieldImage.sprite = defaultShieldSprite;
            playerShieldImage.color = new Color(1f, 1f, 1f, 1f);
            playerShieldImage.rectTransform.localRotation = Quaternion.Euler(0, 0, 0);
        }

        if (dimOverlay != null) dimOverlay.gameObject.SetActive(false);
        if (playerRollCanvas != null) playerRollCanvas.gameObject.SetActive(false);

        if (rollCanvasGroup != null) rollCanvasGroup.alpha = 1f;
        if (notificationText != null)
        {
            notificationText.text = "";
            notificationText.gameObject.SetActive(false);
        }

        if (playerNumberText != null)
        {
            playerNumberText.color = new Color(playerNumberText.color.r, playerNumberText.color.g, playerNumberText.color.b, 1f);
            playerNumberText.gameObject.SetActive(true);
        }
    }
}