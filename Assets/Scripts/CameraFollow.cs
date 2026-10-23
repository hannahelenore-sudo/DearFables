using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform player;

    public float distance = 15f;
    public float rotateCameraAngle = 20f;
    public float smoothTime = 0.15f;

    private Vector3 velocity = Vector3.zero;
    private bool isInitialized = false;

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

        // Slight smooth drag after starting
        transform.position = Vector3.SmoothDamp(
            transform.position,
            targetPosition,
            ref velocity,
            smoothTime
        );

        transform.rotation = cameraRotation;
    }
}