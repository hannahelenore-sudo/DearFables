using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// A row of cards showing the player's health. Losing the card game flips one card over and dims it.
/// If the HUD isn't in the scene when you lose (for example in the card game), the animation plays
/// the next time a scene with this HUD loads.
/// Build it with  Tools > HUD > Create Player HUD  and then just swap the card sprites.
/// </summary>
public class HealthCardsHUD : MonoBehaviour
{
    [Tooltip("The card images, left to right. Extra cards beyond the player's max health are hidden.")]
    public Image[] cards;

    [Header("Look")]
    [Tooltip("Optional: sprite for a card you still have. Empty = keep each card's own sprite.")]
    public Sprite aliveSprite;
    [Tooltip("Optional: sprite for a lost card (for example a card back). Empty = the same sprite, dimmed.")]
    public Sprite lostSprite;
    public Color aliveColor = Color.white;
    public Color lostColor = new Color(0.25f, 0.25f, 0.28f, 0.55f);

    [Header("Game over (optional)")]
    [Tooltip("Scene to load when the last card is lost. Empty = nothing happens yet (health just stays at 0).")]
    public string gameOverSceneName = "";
    public float gameOverDelay = 1.2f;
    [Tooltip("Gives the player full health again before loading the game over scene.")]
    public bool resetHealthOnGameOver = true;

    private Sprite[] originalSprites;
    private bool[] showingLost;
    private Vector2[] basePositions;
    private Coroutine loseRoutine;

    private void Awake()
    {
        if (cards == null) cards = new Image[0];
        originalSprites = new Sprite[cards.Length];
        basePositions = new Vector2[cards.Length];
        showingLost = new bool[cards.Length];

        for (int i = 0; i < cards.Length; i++)
        {
            if (cards[i] == null) continue;
            originalSprites[i] = cards[i].sprite;
            basePositions[i] = cards[i].rectTransform.anchoredPosition;
            if (aliveSprite != null) originalSprites[i] = aliveSprite;
        }
    }

    private void OnEnable()
    {
        PlayerHealth.Changed += OnHealthChanged;

        // Start from what we last showed, then animate to the real value
        ShowInstant(PlayerHealth.DisplayedHealth);
        OnHealthChanged();
    }

    private void OnDisable()
    {
        PlayerHealth.Changed -= OnHealthChanged;
        loseRoutine = null;
    }

    private void OnHealthChanged()
    {
        if (!isActiveAndEnabled) return;
        if (loseRoutine == null) loseRoutine = StartCoroutine(AnimateToCurrent());
    }

    private void ShowInstant(int health)
    {
        int max = PlayerHealth.Max;
        for (int i = 0; i < cards.Length; i++)
        {
            if (cards[i] == null) continue;
            cards[i].gameObject.SetActive(i < max);
            SetCardLook(i, i < health);
            cards[i].rectTransform.anchoredPosition = basePositions[i];
            cards[i].rectTransform.localScale = Vector3.one;
            cards[i].rectTransform.localRotation = Quaternion.identity;
        }
    }

    private void SetCardLook(int i, bool alive)
    {
        if (cards[i] == null) return;
        showingLost[i] = !alive;

        if (alive)
        {
            if (originalSprites[i] != null) cards[i].sprite = originalSprites[i];
            cards[i].color = aliveColor;
        }
        else
        {
            if (lostSprite != null) cards[i].sprite = lostSprite;
            cards[i].color = lostColor;
        }
    }

    private IEnumerator AnimateToCurrent()
    {
        yield return null; // let everything else in the scene start first

        // Cards for a bigger / smaller max health
        for (int i = 0; i < cards.Length; i++)
            if (cards[i] != null) cards[i].gameObject.SetActive(i < PlayerHealth.Max);

        int shown = PlayerHealth.DisplayedHealth;
        int target = PlayerHealth.Current;

        // Lost cards: the right-most remaining card goes first
        while (shown > target)
        {
            shown--;
            yield return StartCoroutine(LoseCard(shown));
            yield return new WaitForSecondsRealtime(0.15f);
        }

        // Gained cards
        while (shown < target)
        {
            if (shown < cards.Length) yield return StartCoroutine(GainCard(shown));
            shown++;
        }

        PlayerHealth.DisplayedHealth = target;
        loseRoutine = null;

        if (target <= 0 && !string.IsNullOrEmpty(gameOverSceneName))
        {
            yield return new WaitForSecondsRealtime(gameOverDelay);
            if (resetHealthOnGameOver) PlayerHealth.ResetAll();
            SceneManager.LoadScene(gameOverSceneName);
        }
    }

    // Shake, flip over, dim
    private IEnumerator LoseCard(int index)
    {
        if (index < 0 || index >= cards.Length || cards[index] == null) yield break;

        RectTransform r = cards[index].rectTransform;
        Vector2 start = basePositions[index];

        // 1. shake + flash red
        const float shakeTime = 0.35f;
        float t = 0f;
        while (t < shakeTime)
        {
            t += Time.unscaledDeltaTime;
            float x = Mathf.Clamp01(t / shakeTime);
            r.anchoredPosition = start + new Vector2(Mathf.Sin(x * Mathf.PI * 10f) * 7f * (1f - x * 0.5f), 0f);
            cards[index].color = Color.Lerp(aliveColor, new Color(1f, 0.3f, 0.3f, 1f), Mathf.Sin(x * Mathf.PI));
            yield return null;
        }
        r.anchoredPosition = start;

        // 2. flip (squash to nothing, swap, open again)
        const float flipTime = 0.18f;
        t = 0f;
        while (t < flipTime)
        {
            t += Time.unscaledDeltaTime;
            r.localScale = new Vector3(1f - Mathf.Clamp01(t / flipTime), 1f, 1f);
            yield return null;
        }

        SetCardLook(index, false);

        t = 0f;
        while (t < flipTime)
        {
            t += Time.unscaledDeltaTime;
            r.localScale = new Vector3(Mathf.Clamp01(t / flipTime), 1f, 1f);
            yield return null;
        }
        r.localScale = Vector3.one;
    }

    private IEnumerator GainCard(int index)
    {
        if (cards[index] == null) yield break;

        SetCardLook(index, true);
        RectTransform r = cards[index].rectTransform;

        const float duration = 0.3f;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float x = Mathf.Clamp01(t / duration);
            r.localScale = Vector3.one * (1f + 0.3f * Mathf.Sin(x * Mathf.PI));
            yield return null;
        }
        r.localScale = Vector3.one;
    }
}
