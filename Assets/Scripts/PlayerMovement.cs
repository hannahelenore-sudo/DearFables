using System.Collections.Generic;
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

    [Header("Sprint Dust")]
    public bool sprintDust = true;
    [Tooltip("TEST: dust always on, even when standing still. Untick when you're done testing.")]
    public bool dustDebugAlwaysOn = false;
    [Tooltip("Colour and strength (alpha) of the dust. Change it live while playing.")]
    public Color dustColor = new Color(0.85f, 0.8f, 0.7f, 0.7f);
    [Tooltip("Optional. Leave empty for a soft round puff that is generated automatically.")]
    public Texture2D dustTexture;
    [Tooltip("Puffs per second while sprinting.")]
    public float dustRate = 18f;
    [Tooltip("Random puff size between these two values (meters).")]
    public Vector2 dustSize = new Vector2(0.3f, 0.6f);
    [Tooltip("Random puff lifetime between these two values (seconds).")]
    public Vector2 dustLifetime = new Vector2(0.35f, 0.6f);
    [Tooltip("How fast puffs drift away from the player.")]
    public float dustSpeed = 0.6f;
    [Tooltip("How wide the puffs spread out (degrees).")]
    public float dustSpread = 25f;
    [Tooltip("Height above the feet where the dust appears.")]
    public float dustHeight = 0.05f;
    [Tooltip("How far behind the player the dust appears.")]
    public float dustBackOffset = 0.1f;
    [Tooltip("Sorting order relative to the player's sprite. 1 = in front of the player, -1 = just behind the player.")]
    public int dustSortOffset = 1;
    [Tooltip("Also puff some dust when a dash starts.")]
    public bool dustOnDash = true;
    public int dashDustCount = 10;

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

    // Dust (plain sprites, pooled)
    private sealed class Puff
    {
        public Transform t;
        public SpriteRenderer sr;
        public Vector3 vel;
        public float age;
        public float life;
        public float size;
    }

    private const int MaxPuffs = 150;
    private readonly List<Puff> activePuffs = new List<Puff>();
    private readonly Stack<Puff> puffPool = new Stack<Puff>();
    private Transform dustRoot;
    private Sprite dustSprite;
    private SpriteRenderer sortReference;
    private float dustEmitAccumulator;
    private Vector3 lastDustDirection = Vector3.back;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
        animator = GetComponent<Animator>();
        SetupFootsteps();
        SetupDust();

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
        UpdateDust();
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

        // Burst of dust when the dash starts
        if (sprintDust && dustOnDash && isGrounded)
        {
            for (int i = 0; i < dashDustCount; i++)
                SpawnPuff(dashDirection);
        }
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

    // ---------------- Sprint dust ----------------

    void SetupDust()
    {
        // Not parented to the player, so the puffs stay where they were left (a real trail)
        dustRoot = new GameObject("SprintDust").transform;

        Texture2D tex = dustTexture != null ? dustTexture : CreateSoftPuffTexture();
        // pixelsPerUnit = width -> the puff sprite is exactly 1 unit wide, so scale = size in meters
        dustSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width);

        // Sort relative to the tallest sprite under the player (the character)
        float best = 0f;
        foreach (SpriteRenderer sr in GetComponentsInChildren<SpriteRenderer>())
        {
            if (sr.bounds.size.y > best)
            {
                best = sr.bounds.size.y;
                sortReference = sr;
            }
        }

        if (sortReference == null)
            Debug.LogWarning("PlayerMovement: no SpriteRenderer found under the player, dust uses sorting order = Dust Sort Offset.", this);
    }

    void UpdateDust()
    {
        if (dustRoot == null) return;

        // Remember which way we're running so the dust goes behind us
        Vector3 h = rb != null
            ? new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z)
            : moveDirection;
        if (h.sqrMagnitude > 0.01f) lastDustDirection = h.normalized;

        bool sprinting = dustDebugAlwaysOn || (sprintDust
            && isGrounded
            && !isDashJumping
            && dashTimer <= 0f
            && currentSpeed > walkSpeed + 0.01f
            && HorizontalSpeed() > 0.1f
            && (rb == null || Mathf.Abs(rb.linearVelocity.y) < 0.5f)
            && Time.timeScale > 0f);

        if (sprinting)
        {
            dustEmitAccumulator += dustRate * Time.deltaTime;
            while (dustEmitAccumulator >= 1f)
            {
                SpawnPuff(lastDustDirection);
                dustEmitAccumulator -= 1f;
            }
        }
        else
        {
            dustEmitAccumulator = 0f;
        }

        UpdatePuffs();
    }

    void SpawnPuff(Vector3 movingDirection)
    {
        if (dustRoot == null || dustSprite == null) return;
        if (activePuffs.Count >= MaxPuffs) return;

        movingDirection.y = 0f;
        if (movingDirection.sqrMagnitude < 0.0001f) movingDirection = lastDustDirection;
        movingDirection.Normalize();
        Vector3 back = -movingDirection;

        Puff p = puffPool.Count > 0 ? puffPool.Pop() : CreatePuff();
        p.t.gameObject.SetActive(true);

        p.size = Random.Range(dustSize.x, dustSize.y);
        p.life = Random.Range(dustLifetime.x, dustLifetime.y);
        p.age = 0f;

        // Lifted by half the puff size so it isn't cut in half by the ground
        float footY = col != null ? col.bounds.min.y : transform.position.y;
        Vector3 pos = new Vector3(transform.position.x, footY + dustHeight + p.size * 0.5f, transform.position.z);
        pos += back * dustBackOffset + Random.insideUnitSphere * 0.05f;
        p.t.position = pos;

        // Drift backwards with a random spread, and a little upwards
        Vector3 dir = Quaternion.AngleAxis(Random.Range(-dustSpread, dustSpread), Vector3.up) * back;
        p.vel = dir * Random.Range(dustSpeed * 0.5f, dustSpeed) + Vector3.up * Random.Range(0.05f, 0.25f);

        // Draw on the player's layer, relative to the player's sprite
        if (sortReference != null)
        {
            p.sr.sortingLayerID = sortReference.sortingLayerID;
            p.sr.sortingOrder = Mathf.Clamp(sortReference.sortingOrder + dustSortOffset, -32768, 32767);
        }
        else
        {
            p.sr.sortingOrder = Mathf.Clamp(dustSortOffset, -32768, 32767);
        }

        p.t.localScale = Vector3.one * p.size * 0.6f;
        activePuffs.Add(p);
    }

    Puff CreatePuff()
    {
        GameObject go = new GameObject("DustPuff");
        go.transform.SetParent(dustRoot, false);

        Puff p = new Puff();
        p.t = go.transform;
        p.sr = go.AddComponent<SpriteRenderer>();
        p.sr.sprite = dustSprite;
        return p;
    }

    void UpdatePuffs()
    {
        Camera cam = Camera.main;
        float dt = Time.deltaTime;

        for (int i = activePuffs.Count - 1; i >= 0; i--)
        {
            Puff p = activePuffs[i];
            p.age += dt;

            if (p.age >= p.life)
            {
                p.t.gameObject.SetActive(false);
                activePuffs.RemoveAt(i);
                puffPool.Push(p);
                continue;
            }

            float t = p.age / p.life;

            p.vel *= Mathf.Max(0f, 1f - 2.5f * dt); // slows down like a puff of air
            p.t.position += p.vel * dt;
            p.t.localScale = Vector3.one * Mathf.Lerp(p.size * 0.6f, p.size * 1.5f, t); // grows as it fades
            if (cam != null) p.t.rotation = cam.transform.rotation;

            // Fade in quickly, then fade out
            float fade = t < 0.15f ? t / 0.15f : 1f - (t - 0.15f) / 0.85f;
            Color c = dustColor;
            c.a *= Mathf.Clamp01(fade);
            p.sr.color = c;
        }
    }

    // Generates a soft round puff so no image asset is needed
    static Texture2D CreateSoftPuffTexture()
    {
        const int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        float c = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float r = Vector2.Distance(new Vector2(x, y), new Vector2(c, c)) / (size * 0.5f);
                float a = Mathf.Pow(Mathf.Clamp01(1f - r), 1.5f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        return tex;
    }

    void OnDestroy()
    {
        if (dustRoot != null) Destroy(dustRoot.gameObject);
    }

    // ---------------- Physics / animation ----------------

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