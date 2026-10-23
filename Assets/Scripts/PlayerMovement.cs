using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    public float walkSpeed = 3f;
    public float runSpeed = 7f;
    public float runDragDistance = 150f;

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
    private Animator animator;

    private Vector3 mouseStart;
    private Vector3 moveDirection;
    private Vector3 lastFacing = Vector3.down;

    private bool isDragging = false;
    private float currentSpeed = 0f;
    private string currentAnimation = "";

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();
        PlayFacingIdle();
    }

    void Update()
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
            currentSpeed = walkSpeed;
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

        UpdateAnimation();
    }

    void FixedUpdate()
    {
        if (rb == null || currentSpeed <= 0f)
            return;

        Vector3 movement = moveDirection * currentSpeed * Time.fixedDeltaTime;
        rb.MovePosition(rb.position + movement);
    }

    void UpdateAnimation()
    {
        if (animator == null)
            return;

        if (currentSpeed > 0f)
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