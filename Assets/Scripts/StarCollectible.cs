
using UnityEngine;

public class StarHover : MonoBehaviour
{
    public float bounceHeight = 0.15f;
    public float bounceSpeed = 3f;
    public float starValue = 1f;

    private Vector3 originalPosition;
    private float bounceTime;
    private bool collected;

    void Start()
    {
        originalPosition = transform.localPosition;
        Debug.Log("Star is active: " + gameObject.name);
    }

    void Update()
    {
        bounceTime += Time.deltaTime * bounceSpeed;

        Vector3 pos = originalPosition;
        pos.y += (Mathf.Sin(bounceTime) + 1f) / 2f * bounceHeight;

        transform.localPosition = pos;
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("Star touched by: " + other.name);

        if (collected)
            return;

        Transform player = other.transform;

        if (!player.CompareTag("Player"))
            player = player.root;

        if (!player.CompareTag("Player"))
        {
            Debug.Log("Object is not tagged Player.");
            return;
        }

        collected = true;

        if (StarManager.Instance != null)
        {
            StarManager.Instance.CollectStar(Mathf.RoundToInt(starValue));
        }
        else
        {
            Debug.LogWarning("StarManager is missing!");
        }

        Debug.Log("Star collected!");
        Destroy(gameObject);
    }
}
