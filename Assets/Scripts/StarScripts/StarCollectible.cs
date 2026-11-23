using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class StarHover : MonoBehaviour
{
    public float bounceHeight = 0.15f;
    public float bounceSpeed = 3f;
    public float starValue = 1f;

    [Header("Audio")]
    public AudioClip collectSound;
    [Range(0f, 1f)] public float collectVolume = 1f;

    [Header("Respawn")]
    [Tooltip("ON: once collected, this star stays gone when the scene loads again (e.g. after a card game). Turn OFF for stars that should always come back.")]
    public bool stayGoneWhenCollected = true;
    [Tooltip("Optional. Unique name for this star. Leave empty: it is worked out from the scene, the object's path and its position.")]
    public string starId = "";

    private Vector3 originalPosition;
    private float bounceTime;
    private bool collected;
    private string rememberedKey;

    private static readonly HashSet<string> collectedStars = new HashSet<string>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnLaunch()
    {
        collectedStars.Clear();
    }

    public static void ResetCollected()
    {
        collectedStars.Clear();
    }

    private string BuildKey()
    {
        string id = starId;
        if (string.IsNullOrEmpty(id))
        {
            string path = name;
            for (Transform t = transform.parent; t != null; t = t.parent) path = t.name + "/" + path;
            Vector3 p = transform.position;
            id = path + "@" + p.x.ToString("F2") + "," + p.y.ToString("F2") + "," + p.z.ToString("F2");
        }
        return gameObject.scene.name + "/" + id;
    }

    void Awake()
    {
        if (!stayGoneWhenCollected) return;

        rememberedKey = BuildKey();
        if (collectedStars.Contains(rememberedKey))
        {
            collected = true;
            Destroy(gameObject);
        }
    }

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
        if (stayGoneWhenCollected && !string.IsNullOrEmpty(rememberedKey))
            collectedStars.Add(rememberedKey);

        if (StarManager.Instance != null)
        {
            StarManager.Instance.CollectStar(Mathf.RoundToInt(starValue));
        }
        else
        {
            Debug.LogWarning("StarManager is missing!");
        }

        if (collectSound != null)
        {
            AudioSource.PlayClipAtPoint(collectSound, transform.position, collectVolume);
        }

        Debug.Log("Star collected!");
        Destroy(gameObject);
    }
}