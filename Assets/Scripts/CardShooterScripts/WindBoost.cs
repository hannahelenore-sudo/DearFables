using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The Wind Card: click once while it is selected and the boost stays on until the bar runs dry
/// (or you switch to another item).
/// Drains the same stamina bar as the thrown items, and shows a wind aura around the player.
/// Added automatically by CardShooter (you can also add it yourself to tweak the values).
///
/// SPEED: by default this adds the extra speed itself (it watches how far the player moved
/// each frame and moves them a bit further). If your movement script has a speed variable you'd
/// rather change, turn off 'Auto Apply To Movement' and multiply your speed by WindBoost.SpeedMultiplier.
/// </summary>
public class WindBoost : MonoBehaviour
{
    [Tooltip("1.8 = 80% faster while boosting.")]
    public float speedMultiplier = 1.8f;
    [Tooltip("Cards drained per second while running. With 4 cards, 1.5 = about 2.7 seconds of boost.")]
    public float drainPerSecond = 1.5f;
    [Tooltip("You need at least this many cards to start boosting.")]
    public float minCardsToStart = 0.5f;
    [Tooltip("ON: this script moves the player the extra distance itself. OFF: your movement script reads WindBoost.SpeedMultiplier.")]
    public bool autoApplyToMovement = true;
    [Tooltip("Movement bigger than this in one frame is treated as a teleport and ignored.")]
    public float teleportLimit = 2f;

    [Header("Speed Up Popup")]
    [Tooltip("Shows a text above the player's head when the Wind Card is switched on.")]
    public bool showPopup = true;
    public string popupText = "Speed Up!";
    [Tooltip("Optional. Leave empty to use TextMeshPro's default font.")]
    public TMP_FontAsset popupFont;
    public float popupFontSize = 56f;
    public Color popupColor = new Color(0.75f, 0.93f, 1f, 1f);
    public Color popupOutlineColor = new Color(0.05f, 0.15f, 0.3f, 1f);
    [Range(0f, 1f)] public float popupOutlineWidth = 0.25f;
    [Tooltip("Seconds the text is on screen.")]
    public float popupDuration = 1.1f;
    [Tooltip("How far the text floats upward while it fades (pixels at 1920x1080).")]
    public float popupRisePixels = 70f;
    [Tooltip("How far above the character's head the text starts, as a fraction of the character's height. The held item icon sits just above the head, so keep this higher than the icon.")]
    public float popupHeightAbove = 0.6f;

    /// <summary>1 normally, speedMultiplier while boosting. Read this in your movement script.</summary>
    public static float SpeedMultiplier { get; private set; } = 1f;
    public static bool IsBoosting { get; private set; }

    private CardStamina stamina;
    private WindAura aura;
    private CharacterController cc;
    private bool wantBoost;
    private bool boosting;
    private bool hasLast;
    private Vector3 lastPos;
    private float movingTimer;

    // Popup
    private SpriteRenderer characterRenderer;
    private GameObject popupCanvasGO;
    private Canvas popupCanvas;
    private CanvasGroup popupGroup;
    private RectTransform popupRect;
    private TextMeshProUGUI popupLabel;
    private float popupTimer;

    public bool CanStart
    {
        get { return stamina != null && !stamina.IsExhausted && stamina.CardsLeft >= minCardsToStart; }
    }

    public void Init(CardStamina cardStamina, SpriteRenderer characterRenderer)
    {
        this.characterRenderer = characterRenderer;
        stamina = cardStamina;
        cc = GetComponent<CharacterController>();

        aura = GetComponent<WindAura>();
        if (aura == null) aura = gameObject.AddComponent<WindAura>();
        aura.Init(characterRenderer);
    }

    /// <summary>True while the boost is switched on (it stays on until the stamina is drained or you switch items).</summary>
    public bool IsActive { get { return wantBoost; } }

    /// <summary>CardShooter calls this when the player clicks with the Wind Card selected.</summary>
    public void Activate()
    {
        if (!wantBoost) ShowPopup(); // only when it actually switches on, not on every click
        wantBoost = true;
    }

    /// <summary>Switching to another item (or anything that should cancel the boost). Stops the drain and the effect.</summary>
    public void Deactivate()
    {
        wantBoost = false;
        boosting = false;
        IsBoosting = false;
        SpeedMultiplier = 1f;
        if (aura != null) aura.SetActive(false);
    }

    private void LateUpdate()
    {
        Vector3 pos = transform.position;
        Vector3 delta = pos - lastPos;
        delta.y = 0f;

        bool moved = hasLast && Time.deltaTime > 0f
                     && delta.sqrMagnitude > 0.000001f
                     && delta.magnitude < teleportLimit;
        hasLast = true;

        if (moved) movingTimer = 0.12f;
        else movingTimer -= Time.deltaTime;

        bool boost = false;
        if (wantBoost && stamina != null && !stamina.IsExhausted && (boosting || CanStart))
        {
            boost = true;
            // Only costs stamina while you actually run
            if (moved) boost = stamina.Drain(drainPerSecond * Time.deltaTime);
        }

        if (boost && moved && autoApplyToMovement)
        {
            Vector3 extra = delta * (speedMultiplier - 1f);
            if (cc != null && cc.enabled) cc.Move(extra);
            else transform.position += extra;
        }

        // Ran dry -> the boost switches itself off (click again once the bar is full)
        if (wantBoost && !boost && stamina != null && stamina.IsExhausted) wantBoost = false;

        boosting = boost;
        IsBoosting = boost;
        SpeedMultiplier = boost ? speedMultiplier : 1f;

        if (aura != null) aura.SetActive(boost && movingTimer > 0f);

        lastPos = transform.position;

        UpdatePopup();
    }

    private void OnDisable()
    {
        boosting = false;
        IsBoosting = false;
        SpeedMultiplier = 1f;
        if (aura != null) aura.SetActive(false);

        popupTimer = 0f;
        if (popupGroup != null) popupGroup.alpha = 0f;
    }

    private void OnDestroy()
    {
        if (popupCanvasGO != null) Destroy(popupCanvasGO);
    }

    // ---------------- "Speed Up!" popup ----------------

    private void ShowPopup()
    {
        if (!showPopup) return;
        if (popupCanvasGO == null) BuildPopup();

        // Applied on every show, so you can tweak these while playing
        popupLabel.text = popupText;
        popupLabel.fontSize = popupFontSize;
        popupLabel.color = popupColor;
        if (popupFont != null) popupLabel.font = popupFont;
        popupLabel.outlineWidth = popupOutlineWidth;
        popupLabel.outlineColor = popupOutlineColor;

        popupTimer = Mathf.Max(0.1f, popupDuration);
    }

    private void BuildPopup()
    {
        popupCanvasGO = new GameObject("SpeedUpPopupUI");
        popupCanvas = popupCanvasGO.AddComponent<Canvas>();
        popupCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        popupCanvas.sortingOrder = 60;

        CanvasScaler scaler = popupCanvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        popupGroup = popupCanvasGO.AddComponent<CanvasGroup>();
        popupGroup.interactable = false;
        popupGroup.blocksRaycasts = false;
        popupGroup.alpha = 0f;

        GameObject textGO = new GameObject("Text", typeof(RectTransform));
        textGO.transform.SetParent(popupCanvasGO.transform, false);

        popupLabel = textGO.AddComponent<TextMeshProUGUI>();
        popupLabel.alignment = TextAlignmentOptions.Center;
        popupLabel.fontStyle = FontStyles.Bold;
        popupLabel.raycastTarget = false;

        popupRect = popupLabel.rectTransform;
        popupRect.sizeDelta = new Vector2(800f, 120f);
        popupRect.pivot = new Vector2(0.5f, 0.5f);
    }

    // Pops in (small overshoot), floats up, then fades out. Follows the player's head on screen.
    private void UpdatePopup()
    {
        if (popupGroup == null) return;

        if (popupTimer <= 0f)
        {
            if (popupGroup.alpha > 0f) popupGroup.alpha = 0f;
            return;
        }

        Camera cam = Camera.main;
        if (cam == null) return;

        popupTimer -= Time.unscaledDeltaTime;
        float t = 1f - Mathf.Clamp01(popupTimer / Mathf.Max(0.1f, popupDuration)); // 0 -> 1 over the popup

        float charH = 2f;
        Vector3 head = transform.position + Vector3.up * 2f;
        if (characterRenderer != null)
        {
            Bounds b = characterRenderer.bounds;
            charH = Mathf.Max(0.5f, b.size.y);
            head = new Vector3(transform.position.x, b.max.y, transform.position.z);
        }
        head += Vector3.up * (charH * popupHeightAbove);

        Vector3 sp = cam.WorldToScreenPoint(head);
        if (sp.z <= 0f) { popupGroup.alpha = 0f; return; }

        float scale = popupCanvas.scaleFactor;
        float rise = popupRisePixels * (1f - (1f - t) * (1f - t)) * scale;
        popupRect.position = new Vector2(sp.x, sp.y + rise);

        // Ease-out-back pop during the first 20%
        float pt = Mathf.Clamp01(t / 0.2f);
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float eased = 1f + c3 * Mathf.Pow(pt - 1f, 3f) + c1 * Mathf.Pow(pt - 1f, 2f);
        popupRect.localScale = Vector3.one * Mathf.Max(0.01f, eased);

        // Fully visible, then fades over the last 40%
        popupGroup.alpha = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
    }
}