using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform player;

    public float distance = 15f;
    public float rotateCameraAngle = 20f;
    public float smoothTime = 0.15f;

    [Header("Sneak Peek")]
    [Tooltip("Move the mouse toward any side or corner to look a little in that direction. The further from the middle, the more it peeks.")]
    public bool enablePeek = true;
    [Tooltip("How far the camera may slide at full strength, in world units. X = left/right, Y = ahead/behind on the ground.")]
    public Vector2 maxPeekOffset = new Vector2(3f, 3f);
    [Range(0f, 0.9f)]
    [Tooltip("Calm area around the middle of the screen where nothing happens (0.1 = the central 10%). Outside it, the peek grows the further your mouse is from the middle.")]
    public float deadZone = 0.1f;
    [Range(0.5f, 3f)]
    [Tooltip("1 = peek grows evenly toward the edge. Above 1 = gentle near the middle, stronger near the edge. Below 1 = reacts quickly even near the middle.")]
    public float responseCurve = 1.5f;
    [Tooltip("Seconds to slide out.")]
    public float peekSmoothTime = 0.25f;
    [Tooltip("Seconds to slide back to normal.")]
    public float returnSmoothTime = 0.2f;

    private Vector3 velocity = Vector3.zero;
    private bool isInitialized = false;

    private Vector2 peekOffset;
    private Vector2 peekVelocity;

    private void LateUpdate()
    {
        if (player == null)
            return;

        Quaternion cameraRotation = Quaternion.Euler(
            rotateCameraAngle,
            0f,
            0f
        );

        Vector3 offset = cameraRotation * Vector3.back * distance;
        Vector3 targetPosition = player.position + offset;

        // Snap camera into position when the game starts
        if (!isInitialized)
        {
            transform.position = targetPosition;
            transform.rotation = cameraRotation;
            isInitialized = true;
            return;
        }

        // Sneak peek: shifts the point the camera is following toward the mouse corner
        UpdatePeek();
        if (peekOffset.sqrMagnitude > 0.0000001f)
        {
            Vector3 right = cameraRotation * Vector3.right;
            Vector3 forward = cameraRotation * Vector3.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;

            targetPosition += right * peekOffset.x + forward * peekOffset.y;
        }

        // Slight smooth drag after starting
        transform.position = Vector3.SmoothDamp(
            transform.position,
            targetPosition,
            ref velocity,
            smoothTime
        );

        transform.rotation = cameraRotation;
    }

    // ------------------------------------------------------------------
    // Sneak peek
    // ------------------------------------------------------------------

    private void UpdatePeek()
    {
        Vector2 target = enablePeek ? GetPeekTarget() : Vector2.zero;

        bool returning = target.sqrMagnitude < peekOffset.sqrMagnitude;
        float smooth = Mathf.Max(0.001f, returning ? returnSmoothTime : peekSmoothTime);

        peekOffset = Vector2.SmoothDamp(peekOffset, target, ref peekVelocity, smooth, Mathf.Infinity, Time.unscaledDeltaTime);
    }

    private Vector2 GetPeekTarget()
    {
        // Mouse gone / unusable -> back to normal
        if (!Application.isFocused) return Vector2.zero;
        if (Cursor.lockState == CursorLockMode.Locked || !Cursor.visible) return Vector2.zero;
        if (Screen.width <= 0 || Screen.height <= 0) return Vector2.zero;

        Vector3 mouse = Input.mousePosition;
        if (mouse.x < 0f || mouse.y < 0f || mouse.x > Screen.width || mouse.y > Screen.height) return Vector2.zero;

        // -1 (far left / bottom) .. 0 (middle) .. +1 (far right / top)
        float nx = (mouse.x / Screen.width - 0.5f) * 2f;
        float ny = (mouse.y / Screen.height - 0.5f) * 2f;

        return new Vector2(Response(nx) * maxPeekOffset.x, Response(ny) * maxPeekOffset.y);
    }

    // Turns "how far the mouse is from the middle" into a peek strength from -1 to +1
    private float Response(float v)
    {
        float amount = Mathf.InverseLerp(deadZone, 1f, Mathf.Abs(v));
        return Mathf.Sign(v) * Mathf.Pow(amount, responseCurve);
    }
}