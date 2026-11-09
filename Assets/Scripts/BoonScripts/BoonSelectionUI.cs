using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// The "Choose Your Boon" screen: a big container with a title and (up to) 3 clickable boon cards.
/// Call Open(callback) and it pops in, lets the player click a card, then closes and returns the pick.
/// Needs an EventSystem in the scene (a normal UI Canvas setup already has one).
/// </summary>
public class BoonSelectionUI : MonoBehaviour
{
    [Header("UI")]
    [Tooltip("The big container (your grey box). Holds the title and the cards. It is shown/hidden by this script.")]
    public GameObject panelRoot;
    public TMP_Text titleText;
    public string title = "Choose Your Boon";
    [Tooltip("Optional full-screen dark Image placed BEHIND the panel (as a sibling, not a child). Also blocks clicks on the game.")]
    public Image dimOverlay;
    [Range(0f, 1f)] public float dimAlpha = 0.6f;
    [Tooltip("The boon cards, left to right.")]
    public BoonCardUI[] cards;

    [Header("Boon Pool")]
    [Tooltip("Boons that can be offered. If there are more than cards, a random selection is shown.")]
    public List<BoonData> boonPool = new List<BoonData>();
    [Tooltip("If the pool is empty, offer the 3 default boons (+1 hand per round, +1 hand size, +50 Wind Power).")]
    public bool useDefaultBoonsIfEmpty = true;

    [Header("Animation")]
    public float panelPopDuration = 0.3f;
    public float cardStagger = 0.12f;
    public float cardAppearDuration = 0.3f;
    public float chosenHoldTime = 0.6f;
    public float closeDuration = 0.25f;

    [Header("Sounds")]
    [Range(0f, 1f)] public float sfxVolume = 1f;
    public AudioClip openSound;
    [Tooltip("Played for every card as it pops in. Pitch rises with each card.")]
    public AudioClip cardAppearSound;
    public AudioClip hoverSound;
    [Tooltip("Played when a card is clicked (unless that boon has its own Pick Sound).")]
    public AudioClip selectSound;
    public AudioClip closeSound;

    public bool IsOpen { get; private set; }

    private CanvasGroup panelGroup;
    private Vector3 panelBaseScale = Vector3.one;
    private Action<BoonData> onChosen;
    private bool hasChosen;
    private List<BoonData> activeChoices = new List<BoonData>();

    private void Awake()
    {
        if (panelRoot != null)
        {
            panelBaseScale = panelRoot.transform.localScale;

            panelGroup = panelRoot.GetComponent<CanvasGroup>();
            if (panelGroup == null) panelGroup = panelRoot.AddComponent<CanvasGroup>();

            panelRoot.SetActive(false);
        }

        if (dimOverlay != null) dimOverlay.gameObject.SetActive(false);

        foreach (BoonCardUI card in cards)
        {
            if (card == null) continue;
            card.onClicked = OnCardClicked;
            card.onHovered = OnCardHovered;
        }
    }

    public void Open(Action<BoonData> callback)
    {
        if (IsOpen) return;

        if (panelRoot == null || cards == null || cards.Length == 0)
        {
            Debug.LogError("BoonSelectionUI: assign the Panel Root and at least one card.", this);
            return;
        }

        onChosen = callback;
        StartCoroutine(OpenRoutine());
    }

    // -------------------------------------------------------------------
    // Open / choose / close
    // -------------------------------------------------------------------

    private IEnumerator OpenRoutine()
    {
        IsOpen = true;
        hasChosen = false;

        BuildChoices();

        if (titleText != null) titleText.text = title;

        // Fill the cards, hide the unused ones
        for (int i = 0; i < cards.Length; i++)
        {
            BoonCardUI card = cards[i];
            if (card == null) continue;

            if (i < activeChoices.Count)
            {
                card.gameObject.SetActive(true);
                card.Show(activeChoices[i]);
                card.SetInteractable(false);
                card.PrepareHidden();
            }
            else
            {
                card.gameObject.SetActive(false);
            }
        }

        PlaySFX(openSound);

        if (dimOverlay != null) dimOverlay.gameObject.SetActive(true);
        panelRoot.SetActive(true);

        // Panel pops in
        float elapsed = 0f;
        while (elapsed < panelPopDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / panelPopDuration);
            float eased = 1f - (1f - t) * (1f - t);

            panelGroup.alpha = eased;
            panelRoot.transform.localScale = panelBaseScale * Mathf.Lerp(0.85f, 1f, eased);

            if (dimOverlay != null) SetDimAlpha(dimAlpha * eased);
            yield return null;
        }
        panelGroup.alpha = 1f;
        panelRoot.transform.localScale = panelBaseScale;
        if (dimOverlay != null) SetDimAlpha(dimAlpha);

        // Cards pop in one after another
        for (int i = 0; i < activeChoices.Count; i++)
        {
            PlaySFX(cardAppearSound, 1f + i * 0.12f);
            StartCoroutine(cards[i].PlayAppear(cardAppearDuration));
            yield return new WaitForSecondsRealtime(cardStagger);
        }

        yield return new WaitForSecondsRealtime(cardAppearDuration);

        // Now the player may click
        for (int i = 0; i < activeChoices.Count; i++)
            cards[i].SetInteractable(true);
    }

    private void OnCardHovered(BoonCardUI card)
    {
        if (!IsOpen || hasChosen) return;
        PlaySFX(hoverSound, UnityEngine.Random.Range(0.95f, 1.08f));
    }

    private void OnCardClicked(BoonCardUI card)
    {
        if (!IsOpen || hasChosen) return;
        hasChosen = true;

        for (int i = 0; i < activeChoices.Count; i++)
            cards[i].SetInteractable(false);

        StartCoroutine(ChooseRoutine(card));
    }

    private IEnumerator ChooseRoutine(BoonCardUI chosenCard)
    {
        BoonData boon = chosenCard.Boon;

        if (boon != null && boon.pickSound != null)
            PlaySFX(boon.pickSound, 1f, boon.pickVolume);
        else
            PlaySFX(selectSound);

        // Shrink the other cards away, pulse the chosen one
        for (int i = 0; i < activeChoices.Count; i++)
        {
            if (cards[i] != chosenCard)
                StartCoroutine(cards[i].PlayDismissed(0.3f));
        }

        yield return StartCoroutine(chosenCard.PlayChosen());
        yield return new WaitForSecondsRealtime(chosenHoldTime);

        // Close
        PlaySFX(closeSound);

        float elapsed = 0f;
        while (elapsed < closeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / closeDuration);

            panelGroup.alpha = 1f - t;
            panelRoot.transform.localScale = panelBaseScale * Mathf.Lerp(1f, 0.9f, t);

            if (dimOverlay != null) SetDimAlpha(dimAlpha * (1f - t));
            yield return null;
        }

        panelRoot.SetActive(false);
        panelRoot.transform.localScale = panelBaseScale;
        if (dimOverlay != null) dimOverlay.gameObject.SetActive(false);

        IsOpen = false;

        Action<BoonData> callback = onChosen;
        onChosen = null;
        callback?.Invoke(boon);
    }

    // -------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------

    private void BuildChoices()
    {
        List<BoonData> source = new List<BoonData>();
        foreach (BoonData b in boonPool)
        {
            if (b != null) source.Add(b);
        }

        if (source.Count == 0 && useDefaultBoonsIfEmpty)
            source = BuildDefaultBoons();

        // Only shuffle when there are more boons than cards
        if (source.Count > cards.Length)
        {
            for (int i = source.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                BoonData temp = source[i];
                source[i] = source[j];
                source[j] = temp;
            }
        }

        activeChoices.Clear();
        int count = Mathf.Min(cards.Length, source.Count);
        for (int i = 0; i < count; i++)
            activeChoices.Add(source[i]);
    }

    private List<BoonData> BuildDefaultBoons()
    {
        return new List<BoonData>
        {
            BoonData.CreateRuntime("Extra Hand", "Permanently gain +1 hand per round", BoonType.ExtraHandPerRound, 1),
            BoonData.CreateRuntime("Bigger Hand", "+1 Hand Size", BoonType.HandSize, 1),
            BoonData.CreateRuntime("Wind Power", "+50 Wind Power", BoonType.WindPower, 50)
        };
    }

    private void SetDimAlpha(float alpha)
    {
        Color c = dimOverlay.color;
        c.a = alpha;
        dimOverlay.color = c;
    }

    private void PlaySFX(AudioClip clip, float pitch = 1f, float volumeScale = 1f)
    {
        if (clip == null) return;
        GameSFX.Play(clip, sfxVolume * volumeScale, pitch);
    }
}
