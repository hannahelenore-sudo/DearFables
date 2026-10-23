using UnityEngine;

public class NPCHover : MonoBehaviour
{
    public float bounceHeight = 0.15f;
    public float bounceSpeed = 3f;
    public float smoothSpeed = 5f;

    private Vector3 originalPosition;
    private Vector3 velocity;

    private bool hovering = false;
    private float bounceTime = 0f;

    void Start()
    {
        originalPosition = transform.localPosition;
    }

    void Update()
    {
        Vector3 targetPosition = originalPosition;

        if (hovering)
        {
            
            float bounce = (Mathf.Sin(bounceTime) + 1f) / 2f;

            targetPosition.y = originalPosition.y +
                               bounce * bounceHeight;

            bounceTime += Time.deltaTime * bounceSpeed;
        }
        else
        {
            
            bounceTime = 0f;
        }

        
        transform.localPosition = Vector3.SmoothDamp(
            transform.localPosition,
            targetPosition,
            ref velocity,
            1f / smoothSpeed
        );
    }

    private void OnMouseEnter()
    {
        hovering = true;
    }

    private void OnMouseExit()
    {
        hovering = false;
    }
}