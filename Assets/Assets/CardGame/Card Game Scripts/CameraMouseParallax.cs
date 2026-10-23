using UnityEngine;
using UnityEngine.InputSystem;

public class CameraMouseParallax : MonoBehaviour
{
    [Header("Parallax Settings")]
    [Tooltip("Maximum offset distance the camera can move.")]
    public Vector2 maxOffset = new Vector2(1.5f, 1.0f);

    [Tooltip("How smoothly the camera moves towards the target position.")]
    public float smoothSpeed = 5.0f;

    private Vector3 initialPosition;

    void Start()
    {
        initialPosition = transform.position;
    }

    void Update()
    {
        Vector2 mousePos = Mouse.current.position.ReadValue();

        float mouseX = (mousePos.x / Screen.width - 0.5f) * 2f;
        float mouseY = (mousePos.y / Screen.height - 0.5f) * 2f;

        Vector3 targetPosition = initialPosition + new Vector3(mouseX * maxOffset.x, mouseY * maxOffset.y, 0f);
        transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * smoothSpeed);
    }
}