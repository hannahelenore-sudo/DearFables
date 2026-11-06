using UnityEngine;

/// <summary>
/// Adds "juice" to the player sprite:
///  - Idle breathing (subtle squish)
///  - Little bounce while running
///  - Stretch in the air, squash on landing, stretch on takeoff
///  - Leans into the direction of travel (more when running / dashing)
///
/// Put this on the PLAYER object (the one with the Rigidbody).
/// Best results: the SpriteRenderer lives on a CHILD object, assigned to "Visual".
/// </summary>
public class PlayerSquishAnimator : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The object with the SpriteRenderer. Best as a CHILD of the player. Leave empty to auto-find.")]
    public Transform visual;

    [Header("Idle Breathing")]
    public float breathSpeed = 2f;
    [Tooltip("How much the sprite swells and shrinks. Keep it small for a subtle effect.")]
    public float breathAmount = 0.03f;

    [Header("Run Bounce")]
    public float runBobSpeed = 14f;
    public float runBobAmount = 0.05f;

    [Header("Jump / Land Squash & Stretch")]
    [Tooltip("Stretch per unit of vertical speed while in the air.")]
    public float airStretchAmount = 0.02f;
    public float maxAirStretch = 0.25f;
    [Tooltip("How hard the landing squash hits.")]
    public float landImpulse = 4f;
    [Tooltip("How hard the stretch is when jumping off.")]
    public float takeoffImpulse = 2.5f;
    [Tooltip("Higher = snappier / wobblier spring.")]
    public float springStiffness = 180f;
    [Tooltip("Lower = more wobble. Higher = settles quickly.")]
    public float springDamping = 12f;
    [Tooltip("How much the sprite gets wider when squashed (and thinner when stretched).")]
    public float widthFactor = 0.6f;
    [Tooltip("Keeps the bottom of the sprite in place while squashing, whatever the pivot is.")]
    public bool keepFeetOnGround = true;

    [Header("Lean")]
    [Tooltip("Side lean in degrees at full speed. If it leans the wrong way, make this negative.")]
    public float maxLeanRoll = 12f;
    [Tooltip("Forward/back lean in degrees. Set to 0 to disable. Negative flips direction.")]
    public float maxLeanPitch = 6f;
    [Tooltip("Speed that counts as full lean (use your run speed).")]
    public float leanSpeedReference = 7f;
    public float leanSharpness = 12f;

    [Header("Ground Check")]
    public LayerMask groundLayers = ~0;
    public float groundCheckExtra = 0.15f;

    private Rigidbody rb;
    private Collider col;
    private SpriteRenderer sr;

    private Vector3 baseScale;
    private Vector3 basePos;
    private Quaternion baseRot;
    private float spriteBottom;

    // Squash spring (positive = squashed, negative = stretched)
    private float squash;
    private float squashVel;
    private float airStretch;

    private float breathPhase;
    private float runPhase;
    private float runWeight;
    private float idleWeight = 1f;

    private bool wasGrounded = true;
    private float airTime;
    private float lowestVy;

    private float roll;
    private float pitch;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();

        if (visual == null)
        {
            sr = GetComponentInChildren<SpriteRenderer>();
            if (sr != null) visual = sr.transform;
        }
        else
        {
            sr = visual.GetComponent<SpriteRenderer>();
        }

        if (visual == null)
        {
            Debug.LogError("PlayerSquishAnimator: no SpriteRenderer found. Assign the 'Visual' object.", this);
            enabled = false;
            return;
        }

        baseScale = visual.localScale;
        basePos = visual.localPosition;
        baseRot = visual.localRotation;

        if (sr != null && sr.sprite != null)
            spriteBottom = sr.sprite.bounds.min.y;

        if (visual == transform)
        {
            Debug.LogWarning("PlayerSquishAnimator: the sprite is on the same object as the Rigidbody/Collider, so the collider will squash and tilt too. " +
                             "Moving the SpriteRenderer + Animator to a child object gives much better results.", this);
        }
    }

    // LateUpdate so we apply after the Animator has run
    void LateUpdate()
    {
        float dt = Mathf.Min(Time.deltaTime, 0.033f);
        if (dt <= 0f) return;

        Vector3 vel = rb != null ? rb.linearVelocity : Vector3.zero;
        Vector3 flat = new Vector3(vel.x, 0f, vel.z);
        float speed = flat.magnitude;
        bool grounded = CheckGrounded();

        // ---- Takeoff / landing detection ----
        if (grounded)
        {
            if (!wasGrounded)
            {
                if (airTime > 0.08f)
                {
                    float fallSpeed = Mathf.Max(0f, -lowestVy);
                    if (fallSpeed > 1.5f)
                        squashVel += landImpulse * Mathf.Clamp(fallSpeed / 8f, 0.3f, 1.5f);
                }
                airTime = 0f;
                lowestVy = 0f;
            }
        }
        else
        {
            if (wasGrounded)
            {
                if (vel.y > 1f) squashVel -= takeoffImpulse;
                airTime = 0f;
                lowestVy = 0f;
            }
            airTime += dt;
            lowestVy = Mathf.Min(lowestVy, vel.y);
        }
        wasGrounded = grounded;

        // ---- Spring (landing / takeoff wobble) ----
        float acc = -springStiffness * squash - springDamping * squashVel;
        squashVel += acc * dt;
        squash += squashVel * dt;

        // ---- Stretch while in the air ----
        float targetAir = grounded ? 0f : -Mathf.Clamp(Mathf.Abs(vel.y) * airStretchAmount, 0f, maxAirStretch);
        airStretch = Mathf.Lerp(airStretch, targetAir, 1f - Mathf.Exp(-15f * dt));

        // ---- Idle vs run weights ----
        float runTarget = (grounded && speed > 0.1f) ? Mathf.Clamp01(speed / leanSpeedReference) : 0f;
        float idleTarget = (grounded && speed <= 0.1f) ? 1f : 0f;
        runWeight = Mathf.Lerp(runWeight, runTarget, 1f - Mathf.Exp(-10f * dt));
        idleWeight = Mathf.Lerp(idleWeight, idleTarget, 1f - Mathf.Exp(-6f * dt));

        breathPhase += dt * breathSpeed;
        runPhase += dt * runBobSpeed * (0.5f + runWeight);

        float breath = -Mathf.Sin(breathPhase) * breathAmount * idleWeight;
        float runBob = -Mathf.Sin(runPhase) * runBobAmount * runWeight;

        // ---- Combine into one squash value and apply ----
        float s = Mathf.Clamp(squash + airStretch + breath + runBob, -0.35f, 0.45f);

        float scaleY = 1f - s;
        float scaleX = 1f + s * widthFactor;
        visual.localScale = new Vector3(baseScale.x * scaleX, baseScale.y * scaleY, baseScale.z);

        if (keepFeetOnGround && visual != transform)
        {
            float offset = spriteBottom * baseScale.y * s;
            visual.localPosition = basePos + baseRot * (Vector3.up * offset);
        }

        // ---- Lean into movement direction ----
        Vector3 right = Vector3.right;
        Vector3 fwd = Vector3.forward;
        Camera cam = Camera.main;
        if (cam != null)
        {
            right = cam.transform.right;
            right.y = 0f;
            right = right.sqrMagnitude > 0.001f ? right.normalized : Vector3.right;

            fwd = cam.transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.001f)
            {
                fwd = cam.transform.up; // looking straight down
                fwd.y = 0f;
            }
            fwd = fwd.sqrMagnitude > 0.001f ? fwd.normalized : Vector3.forward;
        }

        float sideAmount = Mathf.Clamp(Vector3.Dot(flat, right) / leanSpeedReference, -1.2f, 1.2f);
        float fwdAmount = Mathf.Clamp(Vector3.Dot(flat, fwd) / leanSpeedReference, -1.2f, 1.2f);

        float targetRoll = -sideAmount * maxLeanRoll;
        float targetPitch = fwdAmount * maxLeanPitch;

        float t = 1f - Mathf.Exp(-leanSharpness * dt);
        roll = Mathf.Lerp(roll, targetRoll, t);
        pitch = Mathf.Lerp(pitch, targetPitch, t);

        visual.localRotation = baseRot * Quaternion.Euler(pitch, 0f, roll);
    }

    bool CheckGrounded()
    {
        if (col == null)
            return Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, 0.3f, groundLayers, QueryTriggerInteraction.Ignore);

        Bounds b = col.bounds;
        float distance = b.extents.y + groundCheckExtra;
        return Physics.Raycast(b.center, Vector3.down, distance, groundLayers, QueryTriggerInteraction.Ignore);
    }
}
