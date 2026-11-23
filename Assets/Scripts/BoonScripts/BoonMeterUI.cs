using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

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

    [Header("Cutscenes")]
    [Tooltip("ON: if a cutscene is playing when the boon is about to appear, the boon waits and only starts after the cutscene has finished.")]
    public bool waitForCutscenes = true;
    [Tooltip("After a cutscene ends, wait this long (and check again) before the boon starts, in case another cutscene follows right away.")]
    public float cutsceneSettleTime = 0.35f;

    [Header("Player")]
    [Tooltip("Frozen while choosing a boon. Leave empty to auto-find the object tagged 'Player' and its PlayerMovement script.")]
    public Behaviour playerMovement;
    [Tooltip("Make sure the mouse is usable while the boon screen is open.")]
    public bool unlockCursorDuringSelection = true;
    [Tooltip("ON: the whole game is paused while the boon screen is open (player, enemies, thrown cards, animations). The boon screen itself keeps working.")]
    public bool pauseGameDuringSelection = true;

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
    private bool frozen;
    private float originalTimeScale = 1f;
    private bool originalInputLocked;
    private CardShooter cardShooter;
    private bool originalShooterEnabled;
    private Color liquidBaseColor = Color.white;
    private Vector3 containerBaseScale = Vector3.one;
    private RectTransform pulseTarget;
    private IntroCutscene[] cutscenes;

    private void Awake()
    {
        pulseTarget = containerImage != null ? containerImage.rectTransform : transform as RectTransform;
        if (pulseTarget != null) containerBaseScale = pulseTarget.localScale;

        if (liquidImage != null)
        {
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

        // Never leave the game paused if this object is switched off or the scene closes mid-selection
        if (frozen) FreezePlayer(false);
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

    public void Refresh()
    {
        if (!isActiveAndEnabled || running) return;
        StartCoroutine(RunRoutine());
    }

    public void OnMeterClicked()
    {
        if (IsReady) clickRequested = true;
    }

    private IEnumerator RunRoutine()
    {
        running = true;

        yield return new WaitForSecondsRealtime(startDelay);

        while (running)
        {
            float target = Mathf.Min(BoonProgress.Meter, 1f);
            if (Mathf.Abs(target - shownFill) > 0.001f)
            {
                bool gaining = target > shownFill;
                yield return StartCoroutine(AnimateFill(shownFill, target, gaining ? fillDuration : drainDuration, gaining));
            }

            shownFill = target;
            BoonProgress.DisplayedMeter = target;

            if (!BoonProgress.IsMeterFull) break;

            if (selectionUI == null)
            {
                Debug.LogError("BoonMeterUI: the meter is full but no Selection UI is assigned.", this);
                break;
            }

            yield return StartCoroutine(WaitForCutscenes());

            SetReady(true);
            PlaySFX(readySound);
            yield return new WaitForSecondsRealtime(readyHoldTime);

            yield return StartCoroutine(WaitForCutscenes());

            if (!autoOpenWhenFull)
            {
                clickRequested = false;
                if (meterButton != null) meterButton.interactable = true;

                while (!clickRequested || CutscenePlaying())
                {
                    if (CutscenePlaying()) clickRequested = false;
                    yield return null;
                }

                if (meterButton != null) meterButton.interactable = false;
            }

            yield return StartCoroutine(WaitForCutscenes());

            FreezePlayer(true);

            BoonData picked = null;
            bool done = false;
            selectionUI.Open(boon => { picked = boon; done = true; });
            while (!done) yield return null;

            FreezePlayer(false);

            BoonProgress.ApplyBoon(picked);
            BoonProgress.ConsumeFullMeter();
            SetReady(false);
        }

        running = false;
    }

    private bool CutscenePlaying()
    {
        if (!waitForCutscenes) return false;

        if (cutscenes == null || cutscenes.Length == 0)
            cutscenes = FindObjectsByType<IntroCutscene>(FindObjectsSortMode.None);

        foreach (IntroCutscene c in cutscenes)
            if (c != null && c.IsPlaying) return true;

        return false;
    }

    private IEnumerator WaitForCutscenes()
    {
        if (!CutscenePlaying()) yield break;

        float clearFor = 0f;
        while (clearFor < cutsceneSettleTime)
        {
            if (CutscenePlaying()) clearFor = 0f;
            else clearFor += Time.unscaledDeltaTime;
            yield return null;
        }
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
            if (liquidGraphic != null) liquidGraphic.Slosh(1f);
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

        float wave = (Mathf.Sin(Time.unscaledTime * readyPulseSpeed) + 1f) * 0.5f;

        if (liquidImage != null)
            liquidImage.color = Color.Lerp(liquidBaseColor, readyGlowColor, wave);

        if (liquidGraphic != null)
            liquidGraphic.color = Color.Lerp(liquidBaseColor, readyGlowColor, wave);

        if (pulseTarget != null)
            pulseTarget.localScale = containerBaseScale * (1f + wave * 0.04f);
    }

    private void FreezePlayer(bool freeze)
    {
        if (freeze)
        {
            if (frozen) return;
            frozen = true;

            FindPlayerMovement();

            // Locks walking, running, dragging, jumping and dashing. Enemies check this too and stand still while it is on.
            PlayerMovement pm = playerMovement as PlayerMovement;
            if (pm != null)
            {
                originalInputLocked = pm.InputLocked;
                pm.InputLocked = true;
            }

            if (playerMovement != null)
            {
                Rigidbody rb = playerMovement.GetComponent<Rigidbody>();
                if (rb != null) rb.linearVelocity = Vector3.zero;

                Rigidbody2D rb2d = playerMovement.GetComponent<Rigidbody2D>();
                if (rb2d != null) rb2d.linearVelocity = Vector2.zero;
            }

            // Clicking a boon card must not also throw a card into the world
            if (cardShooter == null) cardShooter = FindAnyObjectByType<CardShooter>();
            if (cardShooter != null)
            {
                originalShooterEnabled = cardShooter.enabled;
                cardShooter.enabled = false;
            }

            // Freezes everything else: enemies, thrown cards, animations, particles
            if (pauseGameDuringSelection)
            {
                originalTimeScale = Time.timeScale;
                Time.timeScale = 0f;
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
            if (!frozen) return;
            frozen = false;

            if (pauseGameDuringSelection)
                Time.timeScale = originalTimeScale > 0f ? originalTimeScale : 1f;

            PlayerMovement pm = playerMovement as PlayerMovement;
            if (pm != null) pm.InputLocked = originalInputLocked;

            if (cardShooter != null) cardShooter.enabled = originalShooterEnabled;

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