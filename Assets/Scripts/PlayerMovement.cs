using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    public float walkSpeed = 3f;
    public float runSpeed = 7f;
    public float runDragDistance = 150f;

    [Header("Jump (Space)")]
    public KeyCode jumpKey = KeyCode.Space;
    public float jumpVelocity = 7f;
    [Tooltip("Extra ray length below the collider used to detect the ground.")]
    public float groundCheckExtra = 0.15f;
    public LayerMask groundLayers = ~0;

    [Header("Dash (Left Ctrl)")]
    public KeyCode dashKey = KeyCode.LeftControl;
    public float dashSpeed = 14f;
    public float dashDuration = 0.2f;
    public float dashCooldown = 0.5f;

    [Header("Dash Jump (press Jump while dashing)")]
    [Tooltip("Horizontal speed kept in the air until landing.")]
    public float dashJumpSpeed = 12f;
    public float dashJumpVelocity = 8f;

    [Header("Footsteps (only while walking on the ground)")]
    public AudioClip[] footstepClips;
    [Tooltip("Optional. Leave empty and one is created automatically.")]
    public AudioSource footstepSource;
    [Range(0f, 1f)] public float footstepVolume = 0.6f;
    [Tooltip("Seconds between steps while walking.")]
    public float walkStepInterval = 0.45f;
    [Tooltip("Seconds between steps while running.")]
    public float runStepInterval = 0.28f;
    [Tooltip("Random pitch change per step so it doesn't sound repetitive.")]
    [Range(0f, 0.3f)] public float pitchVariation = 0.08f;

    [Header("Animations")]
    public string walkUp = "WalkUp";
    public string walkDown = "WalkDown";
    public string walkLeft = "WalkLeft";
    public string walkRight = "WalkRight";

    public string idleUp = "IdleUp";
    public string idleDown = "IdleDown";
    public string idleLeft = "IdleLeft";
    public string idleRight = "IdleRight";

    private Rigidbody rb;
    private Collider col;
    private Animator animator;
    private AudioSource stepSource;
    private float stepTimer;
    private int lastStepIndex = -1;

    private Vector3 mouseStart;
    private Vector3 moveDirection;
    private Vector3 lastFacing = Vector3.down;

    private bool isDragging = false;
    private float currentSpeed = 0f;
    private string currentAnimation = "";

    // Jump / dash state
    private bool isGrounded;
    private bool isDashJumping;
    private float dashTimer;
    private float dashCooldownTimer;
    private Vector3 dashDirection;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
        animator = GetComponent<Animator>();
        SetupFootsteps();

        if (rb != null && rb.isKinematic)
            Debug.LogWarning("PlayerMovement: Rigidbody is Kinematic. Untick 'Is Kinematic' and tick 'Use Gravity' so jumping works.");

        PlayFacingIdle();
    }

    void Update()
    {
        isGrounded = CheckGrounded();

        // Landed after a dash jump
        if (isDashJumping && isGrounded && rb != null && rb.linearVelocity.y <= 0.01f)
            isDashJumping = false;

        if (dashTimer > 0f) dashTimer -= Time.deltaTime;
        if (dashCooldownTimer > 0f) dashCooldownTimer -= Time.deltaTime;

        HandleMoveInput();
        HandleDash();
        HandleJump();

        HandleFootsteps();
        UpdateAnimation();
    }

    void HandleMoveInput()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        Vector3 keyboardInput = new Vector3(horizontal, 0f, vertical);

        if (keyboardInput.magnitude > 0f)
        {
            if (Mathf.Abs(horizontal) > Mathf.Abs(vertical))
            {
                lastFacing = horizontal < 0f ? Vector3.left : Vector3.right;
            }
            else
            {
                lastFacing = vertical > 0f ? Vector3.forward : Vector3.back;
            }

            moveDirection = keyboardInput.normalized;
            currentSpeed = IsRunHeld() ? runSpeed : walkSpeed; // Shift = run
            isDragging = false;
        }
        else
        {
            moveDirection = Vector3.zero;
            currentSpeed = 0f;

            if (Input.GetMouseButtonDown(0))
            {
                mouseStart = Input.mousePosition;
                isDragging = true;
            }

            if (Input.GetMouseButton(0) && isDragging)
            {
                Vector3 drag = Input.mousePosition - mouseStart;
                float dragDistance = drag.magnitude;

                if (dragDistance > 10f)
                {
                    moveDirection = new Vector3(drag.x, 0f, drag.y).normalized;

                    currentSpeed = dragDistance >= runDragDistance
                        ? runSpeed
                        : walkSpeed;

                    if (Mathf.Abs(moveDirection.x) > Mathf.Abs(moveDirection.z))
                    {
                        lastFacing = moveDirection.x < 0f
                            ? Vector3.left
                            : Vector3.right;
                    }
                    else
                    {
                        lastFacing = moveDirection.z > 0f
                            ? Vector3.forward
                            : Vector3.back;
                    }
                }
            }

            if (Input.GetMouseButtonUp(0))
            {
                isDragging = false;
                moveDirection = Vector3.zero;
                currentSpeed = 0f;
            }
        }
    }

    void HandleDash()
    {
        if (!Input.GetKeyDown(dashKey)) return;
        if (dashTimer > 0f || dashCooldownTimer > 0f || isDashJumping) return;

        // Dash the way you're moving, or the way you're facing if standing still
        dashDirection = moveDirection.sqrMagnitude > 0.01f ? moveDirection : lastFacing;
        dashTimer = dashDuration;
        dashCooldownTimer = dashDuration + dashCooldown;
    }

    void HandleJump()
    {
        if (rb == null) return;
        if (!Input.GetKeyDown(jumpKey) || !isGrounded) return;

        Vector3 velocity = rb.linearVelocity;

        if (dashTimer > 0f)
        {
            // Dash jump: bigger jump, keeps dash direction + speed until you land
            isDashJumping = true;
            dashTimer = 0f;
            velocity.y = dashJumpVelocity;
        }
        else
        {
            velocity.y = jumpVelocity;
        }

        rb.linearVelocity = velocity;
        isGrounded = false; // in the air now, so no footsteps this frame
    }

    void SetupFootsteps()
    {
        stepSource = footstepSource;

        if (stepSource == null)
        {
            stepSource = gameObject.AddComponent<AudioSource>();
            stepSource.playOnAwake = false;
            stepSource.loop = false;
            stepSource.spatialBlend = 0f; // 2D sound, same volume everywhere
        }
    }

    void HandleFootsteps()
    {
        if (stepSource == null || footstepClips == null || footstepClips.Length == 0) return;
        if (Time.timeScale <= 0f) return;

        // Steps only play while walking/running on the ground:
        // not in the air, not during a dash or dash jump, not while pushing against a wall.
        bool steppingOnGround = isGrounded
            && !isDashJumping
            && dashTimer <= 0f
            && currentSpeed > 0f
            && HorizontalSpeed() > 0.1f
            && (rb == null || Mathf.Abs(rb.linearVelocity.y) < 0.5f);

        if (!steppingOnGround)
        {
            stepTimer = 0f; // so the first step plays right when you start walking
            return;
        }

        stepTimer -= Time.deltaTime;
        if (stepTimer > 0f) return;

        PlayFootstep();
        stepTimer = currentSpeed >= runSpeed ? runStepInterval : walkStepInterval;
    }

    float HorizontalSpeed()
    {
        if (rb == null) return currentSpeed;
        return new Vector2(rb.linearVelocity.x, rb.linearVelocity.z).magnitude;
    }

    void PlayFootstep()
    {
        // Pick a random clip, but never the same one twice in a row
        int index = Random.Range(0, footstepClips.Length);
        if (footstepClips.Length > 1 && index == lastStepIndex)
            index = (index + 1 + Random.Range(0, footstepClips.Length - 1)) % footstepClips.Length;
        lastStepIndex = index;

        if (footstepClips[index] == null) return;

        stepSource.pitch = Random.Range(1f - pitchVariation, 1f + pitchVariation);
        stepSource.PlayOneShot(footstepClips[index], footstepVolume);
    }

    void FixedUpdate()
    {
        if (rb == null) return;

        Vector3 horizontalVelocity;

        if (isDashJumping)
            horizontalVelocity = dashDirection * dashJumpSpeed;
        else if (dashTimer > 0f)
            horizontalVelocity = dashDirection * dashSpeed;
        else
            horizontalVelocity = moveDirection * currentSpeed;

        // Keep the vertical velocity so gravity and jumps still work
        rb.linearVelocity = new Vector3(horizontalVelocity.x, rb.linearVelocity.y, horizontalVelocity.z);
    }

    bool IsRunHeld()
    {
        return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
    }

    bool CheckGrounded()
    {
        if (col == null)
            return Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, 0.3f, groundLayers, QueryTriggerInteraction.Ignore);

        Bounds b = col.bounds;
        float distance = b.extents.y + groundCheckExtra;
        return Physics.Raycast(b.center, Vector3.down, distance, groundLayers, QueryTriggerInteraction.Ignore);
    }

    void UpdateAnimation()
    {
        if (animator == null)
            return;

        bool isMoving = currentSpeed > 0f || dashTimer > 0f || isDashJumping;

        if (isMoving)
        {
            PlayFacingWalk();
        }
        else
        {
            PlayFacingIdle();
        }
    }

    void PlayFacingWalk()
    {
        if (Mathf.Abs(lastFacing.x) > Mathf.Abs(lastFacing.z))
        {
            PlayAnimation(lastFacing.x < 0f ? walkLeft : walkRight);
        }
        else
        {
            PlayAnimation(lastFacing.z > 0f ? walkUp : walkDown);
        }
    }

    void PlayFacingIdle()
    {
        if (animator == null)
            return;

        if (Mathf.Abs(lastFacing.x) > Mathf.Abs(lastFacing.z))
        {
            PlayAnimation(lastFacing.x < 0f ? idleLeft : idleRight);
        }
        else
        {
            PlayAnimation(lastFacing.z > 0f ? idleUp : idleDown);
        }
    }

    void PlayAnimation(string animationName)
    {
        if (string.IsNullOrEmpty(animationName))
            return;

        if (currentAnimation == animationName)
            return;

        if (animator.HasState(0, Animator.StringToHash(animationName)))
        {
            animator.Play(animationName, 0, 0f);
            currentAnimation = animationName;
        }
    }
}