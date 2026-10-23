using System.Collections;
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

    [Header("Shield State Images")]
    [Tooltip("Difference within this range counts as a Tie/Close roll during normal spins (e.g., 1 means diff between -1 and +1 is a Tie).")]
    public int tieThreshold = 1;
    [Tooltip("Sprite used when player skips, or rolls a tie/close number.")]
    public Sprite tieShieldSprite;
    [Tooltip("Sprite used when player rolls much lower than enemy defense.")]
    public Sprite lowRollShieldSprite;
    [Tooltip("Sprite used when player rolls much higher than enemy defense.")]
    public Sprite highRollShieldSprite;

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
    public float lingerDuration = 0.8f;
    public int minRoll = 1;
    public int maxRoll = 10;

    [Header("Pop & Wiggle Settings")]
    [Tooltip("Target RectTransform for pop/wiggle animations. Uses Player Shield/Number container if assigned.")]
    public RectTransform playerRollContainer;
    public float popScaleMultiplier = 1.35f;
    public float wiggleAngle = 10f;
    public float wiggleDuration = 0.35f;

    [Header("Fade Settings")]
    public float fadeInDuration = 0.3f;
    public float fadeOutDuration = 0.3f;

    [HideInInspector] public bool hasRolledThisRound = false;

    private bool isRolling = false;
    private int currentEnemyDefense = 1;
    private Vector3 initialContainerScale = Vector3.one;
    private Sprite defaultShieldSprite;

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
    }

    public void OpenPlayerRollView(int enemyRolledDefense)
    {
        if (hasRolledThisRound) return;

        currentEnemyDefense = enemyRolledDefense;

        // Reset player shield sprite back to default when starting a view
        if (playerShieldImage != null && defaultShieldSprite != null)
        {
            playerShieldImage.sprite = defaultShieldSprite;
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
        if (playerNumberText != null) playerNumberText.text = "?";

        if (rollTriggerButton != null) rollTriggerButton.interactable = true;
        if (backButton != null) backButton.interactable = true;
        if (skipButton != null) skipButton.interactable = true;

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
        isRolling = false;

        StartCoroutine(CloseAndReturnToBattleView());
    }

    /// <summary>
    /// Forces the outcome to a Tie, updates player UI with enemy's value, replaces shield with tieShieldSprite, and proceeds.
    /// </summary>
    public void SkipRoll()
    {
        if (hasRolledThisRound) return;

        StopAllCoroutines();

        hasRolledThisRound = true;
        isRolling = false;

        SetButtonsInteractable(false);

        if (playerRollContainer != null)
        {
            playerRollContainer.localRotation = Quaternion.identity;
            playerRollContainer.localScale = initialContainerScale;
        }

        // Force rolled value to match enemy's defense for an exact tie
        int finalRolledValue = currentEnemyDefense;

        if (playerNumberText != null)
        {
            playerNumberText.text = finalRolledValue.ToString();
        }

        // Force-apply the Tie shield sprite
        if (playerShieldImage != null && tieShieldSprite != null)
        {
            playerShieldImage.sprite = tieShieldSprite;
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

        while (elapsedTime < spinDuration)
        {
            int randomDisplay = Random.Range(minRoll, maxRoll + 1);

            if (playerNumberText != null)
            {
                playerNumberText.text = randomDisplay.ToString();
            }

            yield return new WaitForSecondsRealtime(spinSpeed);
            elapsedTime += spinSpeed;
        }

        int finalRolledValue = Random.Range(minRoll, maxRoll + 1);

        if (playerNumberText != null)
        {
            playerNumberText.text = finalRolledValue.ToString();
        }

        // Evaluate roll result and replace shield image accordingly
        EvaluateShieldState(finalRolledValue);

        yield return StartCoroutine(PopAndWiggleRoutine());

        yield return new WaitForSecondsRealtime(lingerDuration);

        hasRolledThisRound = true;

        yield return StartCoroutine(FadeCanvasGroupRoutine(1f, 0f, fadeOutDuration));

        CompleteRollSequence(finalRolledValue);
    }

    /// <summary>
    /// Swaps the player's shield image depending on roll outcome compared to enemy defense.
    /// </summary>
    private void EvaluateShieldState(int playerRoll)
    {
        if (playerShieldImage == null) return;

        int difference = playerRoll - currentEnemyDefense;

        // State 1: Tie or close roll
        if (Mathf.Abs(difference) <= tieThreshold)
        {
            if (tieShieldSprite != null) playerShieldImage.sprite = tieShieldSprite;
        }
        // State 2: Lower roll than enemy
        else if (difference < -tieThreshold)
        {
            if (lowRollShieldSprite != null) playerShieldImage.sprite = lowRollShieldSprite;
        }
        // State 3: Higher roll than enemy
        else
        {
            if (highRollShieldSprite != null) playerShieldImage.sprite = highRollShieldSprite;
        }
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

        if (dimOverlay != null) dimOverlay.gameObject.SetActive(false);
        if (playerRollCanvas != null) playerRollCanvas.gameObject.SetActive(false);

        if (rollCanvasGroup != null) rollCanvasGroup.alpha = 1f;
    }
}