using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// The shop screen (the back of the car): shows 3 trinkets and a Reshuffle button.
/// Everything is free for now: clicking a trinket takes it. Open it with Open() (ShopInteractable does this).
/// Needs an EventSystem in the scene. Tip: build the whole UI with  Tools > Shop > Create Shop UI.
/// </summary>
public class TrinketShopUI : MonoBehaviour
{
    [Serializable]
    public class Slot
    {
        public Button button;
        public Image icon;
        public TMP_Text nameText;
        public TMP_Text descriptionText;
        [Tooltip("Shown on top of the slot once the trinket was taken.")]
        public GameObject takenOverlay;

        [NonSerialized] public TrinketData trinket;
        [NonSerialized] public bool taken;
    }

    [Header("UI")]
    [Tooltip("The container that is shown/hidden (full-screen dim + the shop panel).")]
    public GameObject panelRoot;
    [Tooltip("The trinket slots, left to right (3).")]
    public Slot[] slots;
    public Button reshuffleButton;
    public Button closeButton;

    [Header("Shopkeeper")]
    [Tooltip("Optional. The shopkeeper's dialogue box: greets you, reacts when you take something or reshuffle.")]
    public ShopkeeperDialogue shopkeeper;

    [Header("Trinkets")]
    [Tooltip("Trinkets that can appear. Leave empty to use the 8 default trinkets.")]
    public List<TrinketData> trinketPool = new List<TrinketData>();
    public bool useDefaultTrinketsIfEmpty = true;

    [Header("Animation")]
    public float popDuration = 0.2f;
    public float slotPopDuration = 0.25f;
    public float slotStagger = 0.08f;

    [Header("Sounds")]
    [Range(0f, 1f)] public float sfxVolume = 1f;
    public AudioClip openSound;
    public AudioClip buySound;
    public AudioClip reshuffleSound;
    public AudioClip closeSound;
    [Tooltip("Played when you try to take a trinket but already have 5.")]
    public AudioClip fullSound;

    /// <summary>True while any shop is open. Handy to stop player movement: if (TrinketShopUI.IsAnyShopOpen) return;</summary>
    public static bool IsAnyShopOpen { get; private set; }
    public bool IsOpen { get; private set; }

    /// <summary>The shop that is open right now (null if none). The sidebar uses it to react when you sell.</summary>
    public static TrinketShopUI OpenShop { get; private set; }

    private CanvasGroup panelGroup;
    private bool hasStock;
    private List<TrinketData> defaultPool;

    private void Awake()
    {
        IsAnyShopOpen = false;

        if (panelRoot != null)
        {
            panelGroup = panelRoot.GetComponent<CanvasGroup>();
            if (panelGroup == null) panelGroup = panelRoot.AddComponent<CanvasGroup>();
            panelRoot.SetActive(false);
        }

        if (slots != null)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                int index = i;
                if (slots[i] != null && slots[i].button != null)
                    slots[i].button.onClick.AddListener(() => Buy(index));
            }
        }

        if (reshuffleButton != null) reshuffleButton.onClick.AddListener(Reshuffle);
        if (closeButton != null) closeButton.onClick.AddListener(Close);
    }

    private void OnDisable()
    {
        if (IsOpen) IsAnyShopOpen = false;
        if (OpenShop == this) OpenShop = null;
    }

    private void Update()
    {
        if (IsOpen && Input.GetKeyDown(KeyCode.Escape)) Close();
    }

    // -------------------------------------------------------------------
    // Open / close
    // -------------------------------------------------------------------

    public void Open()
    {
        if (IsOpen) return;

        if (panelRoot == null || slots == null || slots.Length == 0)
        {
            Debug.LogError("TrinketShopUI: assign the Panel Root and the slots (or use Tools > Shop > Create Shop UI).", this);
            return;
        }

        IsOpen = true;
        IsAnyShopOpen = true;
        OpenShop = this;

        PlaySFX(openSound);
        panelRoot.SetActive(true);
        StartCoroutine(PopIn());

        // The stock stays the same between visits until you press Reshuffle
        if (!hasStock) RollStock();

        if (shopkeeper != null) shopkeeper.SayGreeting();
    }

    public void Close()
    {
        if (!IsOpen) return;

        IsOpen = false;
        IsAnyShopOpen = false;
        if (OpenShop == this) OpenShop = null;

        PlaySFX(closeSound);
        if (shopkeeper != null) shopkeeper.Clear();
        StopAllCoroutines();
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    private IEnumerator PopIn()
    {
        Vector3 baseScale = panelRoot.transform.localScale;
        float elapsed = 0f;

        while (elapsed < popDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / popDuration);
            float eased = 1f - (1f - t) * (1f - t);

            panelGroup.alpha = eased;
            panelRoot.transform.localScale = baseScale * Mathf.Lerp(0.9f, 1f, eased);
            yield return null;
        }

        panelGroup.alpha = 1f;
        panelRoot.transform.localScale = baseScale;
    }

    // -------------------------------------------------------------------
    // Stock
    // -------------------------------------------------------------------

    public void Reshuffle()
    {
        if (!IsOpen) return;
        PlaySFX(reshuffleSound);
        RollStock();

        if (shopkeeper != null) shopkeeper.SayReshuffle();
    }

    private void RollStock()
    {
        hasStock = true;

        List<TrinketData> source = new List<TrinketData>();
        foreach (TrinketData t in trinketPool)
        {
            if (t != null) source.Add(t);
        }

        if (source.Count == 0 && useDefaultTrinketsIfEmpty)
        {
            if (defaultPool == null) defaultPool = TrinketData.BuildDefaults();
            source.AddRange(defaultPool);
        }

        // Shuffle, then deal one distinct trinket to each slot
        for (int i = source.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            TrinketData temp = source[i];
            source[i] = source[j];
            source[j] = temp;
        }

        for (int i = 0; i < slots.Length; i++)
        {
            Slot slot = slots[i];
            if (slot == null) continue;

            slot.trinket = i < source.Count ? source[i] : null;
            slot.taken = false;
            RefreshSlot(slot);

            if (slot.trinket != null && slot.button != null)
                StartCoroutine(PopSlot(slot.button.transform, i * slotStagger));
        }
    }

    private void RefreshSlot(Slot slot)
    {
        if (slot.button != null) slot.button.gameObject.SetActive(slot.trinket != null);
        if (slot.trinket == null) return;

        if (slot.nameText != null) slot.nameText.text = slot.trinket.trinketName;
        if (slot.descriptionText != null) slot.descriptionText.text = slot.trinket.description;

        if (slot.icon != null)
        {
            slot.icon.sprite = slot.trinket.icon;
            slot.icon.enabled = slot.trinket.icon != null;
        }

        if (slot.takenOverlay != null) slot.takenOverlay.SetActive(slot.taken);
        if (slot.button != null) slot.button.interactable = !slot.taken;
    }

    private void Buy(int index)
    {
        if (!IsOpen || index < 0 || index >= slots.Length) return;

        Slot slot = slots[index];
        if (slot == null || slot.trinket == null || slot.taken) return;

        // Max 5 trinkets: sell one in the sidebar to make room
        if (TrinketInventory.IsFull)
        {
            PlaySFX(fullSound);
            TrinketSidebar.FlashFull();
            if (shopkeeper != null) shopkeeper.SayFull();
            return;
        }

        // Free for now. Costs / ownership checks come later.
        TrinketInventory.Add(slot.trinket);
        slot.taken = true;
        RefreshSlot(slot);

        PlaySFX(buySound);
        if (shopkeeper != null) shopkeeper.SayBought(slot.trinket.trinketName);
        Debug.Log("Took trinket: " + slot.trinket.trinketName);
    }

    /// <summary>Called by the sidebar after the player sells a trinket.</summary>
    public void NotifySold(TrinketData sold)
    {
        if (shopkeeper != null) shopkeeper.SaySold(sold != null ? sold.trinketName : "");
    }

    // Little "pop" when a slot is dealt
    private IEnumerator PopSlot(Transform target, float delay)
    {
        target.localScale = Vector3.zero;
        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);

        float elapsed = 0f;
        while (elapsed < slotPopDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / slotPopDuration);

            // ease out back (slight overshoot)
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float eased = 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);

            target.localScale = Vector3.one * eased;
            yield return null;
        }

        target.localScale = Vector3.one;
    }

    private void PlaySFX(AudioClip clip, float pitch = 1f)
    {
        if (clip == null) return;
        GameSFX.Play(clip, sfxVolume, pitch);
    }
}