using UnityEngine;

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

    public bool CanStart
    {
        get { return stamina != null && !stamina.IsExhausted && stamina.CardsLeft >= minCardsToStart; }
    }

    public void Init(CardStamina cardStamina, SpriteRenderer characterRenderer)
    {
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
    }

    private void OnDisable()
    {
        boosting = false;
        IsBoosting = false;
        SpeedMultiplier = 1f;
        if (aura != null) aura.SetActive(false);
    }
}