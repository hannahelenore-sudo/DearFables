using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// A container image with a liquid that fills up each time the player fights an enemy.
/// When it is full, the "Choose Your Boon" screen opens and the pick becomes a permanent upgrade.
///
/// Put this in the scene the player returns to after a battle (the overworld).
/// The fill animates from where it was last time to where it is now.
/// </summary>
public class BoonMeterUI : MonoBehaviour
{
    [Header("Meter Visuals")]
    [Tooltip("The container / frame image. Assign any sprite you like.")]
    public Image containerImage;
    [Tooltip("RECOMMENDED: a Liquid Graphic (waves, slosh, foam, bubbles). If assigned, it is used instead of the Liquid Image below.")]
    public LiquidGraphic liquidGraphic;
    [Tooltip("Simple alternative: a flat Image that fills from the bottom. Leave empty when using a Liquid Graphic.")]
    public Image liquidImage;
    [Tooltip("Optional text, shows the fill as a percentage.")]
    public TMP_Text percentText;
    [Tooltip("Liquid colour pulses towards this while a boon is ready.")]
    public Color readyGlowColor = new Color(1f, 0.9f, 0.4f, 1f);
    public float readyPulseSpeed = 4f;

    [Header("Boon Selection")]
    public BoonSelectionUI selectionUI;
    [Tooltip("ON: the boon screen opens by itself when the meter is full. OFF: the player must click the Meter Button.")]
    public bool autoOpenWhenFull = true;
    [Tooltip("Optional. Only used when Auto Open is OFF. Make the container a Button and drag it here.")]
    public Button meterButton;

    [Header("Timing")]
    [Tooltip("Wait after the scene loads before the meter starts filling.")]
    public float startDelay = 0.8f;
    public float fillDuration = 1.2f;
    public float drainDuration = 0.8f;
    public float readyHoldTime = 0.6f;
    public float gainPulseScale = 1.1f;

    [Header("Player")]
    [Tooltip("Frozen while choosing a boon. Leave empty to auto-find the object tagged 'Player' and its PlayerMovement script.")]
    public Behaviour playerMovement;
    [Tooltip("Make sure the mouse is usable while the boon screen is open.")]
    public bool unlockCursorDuringSelection = true;

    [Header("Sounds")]
    [Range(0f, 1f)] public float sfxVolume = 1f;
    [Tooltip("Ticks while the liquid rises. Pitch climbs as it fills.")]
    public AudioClip fillTickSound;
    public float fillTickInterval = 0.06f;
    [Tooltip("Played when the liquid stops rising.")]
    public AudioClip fillDoneSound;
    [Tooltip("Played when the meter is full and a boon is ready.")]
    public AudioClip readySound;
    [Tooltip("Played when the liquid drains after a boon was picked.")]
    public AudioClip drainSound;

    public bool IsReady { get; private set; }

    private static Sprite whiteSprite;

    private float shownFill;
    private bool running;
    private bool clickRequested;
    private bool originalMovementEnabled;
    private CursorLockMode originalLock;
    private bool originalCursorVisible;
    private Color liquidBaseColor = Color.white;
    private Vector3 containerBaseScale = Vector3.one;
    private RectTransform pulseTarget;

    // -------------------------------------------------------------------
    // Setup
    // -------------------------------------------------------------------

    private void Awake()
    {
        pulseTarget = containerImage != null ? containerImage.rectTransform : transform as RectTransform;
        if (pulseTarget != null) containerBaseScale = pulseTarget.localScale;

        if (liquidImage != null)
        {
            // A filled Image needs a sprite, so give it a plain white one if none is assigned
            if (liquidImage.sprite == null) liquidImage.sprite = GetWhiteSprite();

            liquidImage.type = Image.Type.Filled;
            liquidImage.fillMethod = Image.FillMethod.Vertical;
            liquidImage.fillOrigin = (int)Image.OriginVertical.Bottom;
            liquidBaseColor = liquidImage.color;
        }

        if (liquidGraphic != null)
            liquidBaseColor = liquidGraphic.color;

        if (meterButton != null)
        {
            meterButton.onClick.RemoveListener(OnMeterClicked);
            meterButton.onClick.AddListener(OnMeterClicked);
            meterButton.interactable = false;
        }

        FindPlayerMovement();
    }

    private void OnEnable()
    {
        shownFill = Mathf.Clamp01(BoonProgress.DisplayedMeter);
        SetFill(shownFill);
        Refresh();
    }

    private void OnDisable()
    {
        running = false;
    }

    private void OnDestroy()
    {
        if (meterButton != null) meterButton.onClick.RemoveListener(OnMeterClicked);
    }

    private static Sprite GetWhiteSprite()
    {
        if (whiteSprite == null)
        {
            Texture2D tex = Texture2D.whiteTexture;
            whiteSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        }
        return whiteSprite;
    }

    private void FindPlayerMovement()
    {
        if (playerMovement != null) return;

        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
            playerMovement = player.GetComponent("PlayerMovement") as Behaviour;
    }

    // -------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------

    /// <summary>Animates the meter to the current value. Called automatically when the scene loads.</summary>
    public void Refresh()
    {
        if (!isActiveAndEnabled || running) return;
        StartCoroutine(RunRoutine());
    }

    /// <summary>Hook this to a Button, or use the Meter Button field. Opens the boon screen when ready.</summary>
    public void OnMeterClicked()
    {
        if (IsReady) clickRequested = true;
    }

    // -------------------------------------------------------------------
    // Main loop
    // -------------------------------------------------------------------

    private IEnumerator RunRoutine()
    {
        running = true;

        yield return new WaitForSecondsRealtime(startDelay);

        while (running)
        {
            // 1. Animate to the real meter value
            float target = Mathf.Min(BoonProgress.Meter, 1f);
            if (Mathf.Abs(target - shownFill) > 0.001f)
            {
                bool gaining = target > shownFill;
                yield return StartCoroutine(AnimateFill(shownFill, target, gaining ? fillDuration : drainDuration, gaining));
            }

            shownFill = target;
            BoonProgress.DisplayedMeter = target;

            // 2. Not full yet -> done for now
            if (!BoonProgress.IsMeterFull) break;

            if (selectionUI == null)
            {
                Debug.LogError("BoonMeterUI: the meter is full but no Selection UI is assigned.", this);
                break;
            }

            // 3. Full -> a boon is ready
            SetReady(true);
            PlaySFX(readySound);
            yield return new WaitForSecondsRealtime(readyHoldTime);

            if (!autoOpenWhenFull)
            {
                clickRequested = false;
                if (meterButton != null) meterButton.interactable = true;

                while (!clickRequested) yield return null;

                if (meterButton != null) meterButton.interactable = false;
            }

            // 4. Let the player pick
            FreezePlayer(true);

            BoonData picked = null;
            bool done = false;
            selectionUI.Open(boon => { picked = boon; done = true; });
            while (!done) yield return null;

            FreezePlayer(false);

            // 5. Permanent upgrade, empty the meter (leftover carries over), loop again
            BoonProgress.ApplyBoon(picked);
            BoonProgress.ConsumeFullMeter();
            SetReady(false);
        }

        running = false;
    }

    private IEnumerator AnimateFill(float from, float to, float duration, bool gaining)
    {
        float elapsed = 0f;
        float tickTimer = fillTickInterval;

        if (!gaining)
        {
            PlaySFX(drainSound);
            if (liquidGraphic != null) liquidGraphic.Slosh(0.7f);
        }

        while (elapsed < duration)
        {
            float dt = Time.unscaledDeltaTime;
            elapsed += dt;

            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - (1f - t) * (1f - t);
            SetFill(Mathf.Lerp(from, to, eased));

            if (gaining)
            {
                tickTimer += dt;
                if (tickTimer >= fillTickInterval)
                {
                    tickTimer = 0f;
                    PlaySFX(fillTickSound, Mathf.Lerp(0.9f, 1.5f, t));
                }
            }

            yield return null;
        }

        SetFill(to);

        if (gaining)
        {
            PlaySFX(fillDoneSound);
            if (liquidGraphic != null) liquidGraphic.Slosh(1f); // liquid rocks when the level stops
            yield return StartCoroutine(PulseContainer());
        }
    }

    private IEnumerator PulseContainer()
    {
        if (pulseTarget == null) yield break;

        float elapsed = 0f;
        float duration = 0.25f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(elapsed / duration);
            pulseTarget.localScale = containerBaseScale * Mathf.Lerp(1f, gainPulseScale, Mathf.Sin(p * Mathf.PI));
            yield return null;
        }

        pulseTarget.localScale = containerBaseScale;
    }

    // -------------------------------------------------------------------
    // Visuals
    // -------------------------------------------------------------------

    private void SetFill(float value)
    {
        shownFill = Mathf.Clamp01(value);

        if (liquidGraphic != null) liquidGraphic.SetFill(shownFill);
        if (liquidImage != null) liquidImage.fillAmount = shownFill;
        if (percentText != null) percentText.text = Mathf.RoundToInt(shownFill * 100f) + "%";
    }

    private void SetReady(bool ready)
    {
        IsReady = ready;

        if (!ready)
        {
            if (liquidImage != null) liquidImage.color = liquidBaseColor;
            if (liquidGraphic != null) liquidGraphic.color = liquidBaseColor;
            if (pulseTarget != null) pulseTarget.localScale = containerBaseScale;
        }
    }

    private void Update()
    {
        if (!IsReady) return;

        // Glow + gentle breathing while a boon is waiting
        float wave = (Mathf.Sin(Time.unscaledTime * readyPulseSpeed) + 1f) * 0.5f;

        if (liquidImage != null)
            liquidImage.color = Color.Lerp(liquidBaseColor, readyGlowColor, wave);

        if (liquidGraphic != null)
            liquidGraphic.color = Color.Lerp(liquidBaseColor, readyGlowColor, wave);

        if (pulseTarget != null)
            pulseTarget.localScale = containerBaseScale * (1f + wave * 0.04f);
    }

    // -------------------------------------------------------------------
    // Player
    // -------------------------------------------------------------------

    private void FreezePlayer(bool freeze)
    {
        if (freeze)
        {
            FindPlayerMovement();

            if (playerMovement != null)
            {
                originalMovementEnabled = playerMovement.enabled;
                playerMovement.enabled = false;

                Rigidbody rb = playerMovement.GetComponent<Rigidbody>();
                if (rb != null) rb.linearVelocity = Vector3.zero;

                Rigidbody2D rb2d = playerMovement.GetComponent<Rigidbody2D>();
                if (rb2d != null) rb2d.linearVelocity = Vector2.zero;
            }

            if (unlockCursorDuringSelection)
            {
                originalLock = Cursor.lockState;
                originalCursorVisible = Cursor.visible;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }
        else
        {
            if (playerMovement != null)
                playerMovement.enabled = originalMovementEnabled;

            if (unlockCursorDuringSelection)
            {
                Cursor.lockState = originalLock;
                Cursor.visible = originalCursorVisible;
            }
        }
    }

    private void PlaySFX(AudioClip clip, float pitch = 1f)
    {
        if (clip == null) return;
        GameSFX.Play(clip, sfxVolume, pitch);
    }
}