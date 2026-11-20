using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Draws a soft dark blob flat on the ground under a sprite so it looks planted.
/// Added automatically by ContactShadowSpawner, or put it on any sprite by hand.
/// The shadow is a separate object (not a child), so it never inherits your sprite's
/// tiny scale or rotation. It shrinks and fades when the sprite is in the air (jump).
/// </summary>
[DisallowMultipleComponent]
public class ContactShadow : MonoBehaviour
{
    [Header("Look")]
    [Tooltip("Shadow width as a fraction of the sprite's width. Lower for trees (wide canopy).")]
    public float widthScale = 0.7f;
    [Tooltip("Depth (front-back) as a fraction of the shadow width. Smaller = flatter blob.")]
    public float depthRatio = 0.45f;
    [Range(0f, 1f)] public float opacity = 0.35f;
    [Tooltip("Dark purple-blue so it matches the colour grading.")]
    public Color color = new Color(0.10f, 0.05f, 0.18f, 1f);

    [Header("Placement")]
    [Tooltip("Lift above the ground to avoid z-fighting.")]
    public float groundOffset = 0.02f;
    [Tooltip("Nudge toward the camera (negative z) so the blob peeks out in front of the feet.")]
    public float zOffset = -0.03f;
    [Tooltip("What counts as ground for the downward raycast.")]
    public LayerMask groundMask = ~0;
    [Tooltip("Sprite sorting order minus this value. Must stay behind the sprite itself.")]
    public int sortingOrderOffset = 1;

    [Header("Jumping")]
    [Tooltip("Height at which the shadow is fully shrunk/faded.")]
    public float maxHeightFade = 2f;

    SpriteRenderer target;
    SpriteRenderer shadow;
    Transform shadowT;
    float fallbackGroundY;
    float width;

    static Sprite blobSprite;
    static Transform container;
    static readonly RaycastHit[] hits = new RaycastHit[8];

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        blobSprite = null;
        container = null;
    }

    void Start()
    {
        target = GetComponent<SpriteRenderer>();
        if (target == null) target = GetComponentInChildren<SpriteRenderer>();
        if (target == null || target.sprite == null)
        {
            enabled = false;
            return;
        }

        if (container == null)
            container = new GameObject("Contact Shadows").transform;

        var go = new GameObject("ContactShadowBlob");
        shadowT = go.transform;
        shadowT.SetParent(container, false);
        shadowT.rotation = Quaternion.Euler(90f, 0f, 0f); // lie flat, facing up

        shadow = go.AddComponent<SpriteRenderer>(); // default sprite material, unlit
        shadow.sprite = GetBlobSprite();
        shadow.shadowCastingMode = ShadowCastingMode.Off;
        shadow.receiveShadows = false;

        fallbackGroundY = target.bounds.min.y;
        width = target.bounds.size.x * widthScale;
    }

    void OnEnable()
    {
        if (shadow != null) shadow.enabled = true;
    }

    void OnDisable()
    {
        if (shadow != null) shadow.enabled = false;
    }

    void OnDestroy()
    {
        if (shadowT != null) Destroy(shadowT.gameObject);
    }

    void LateUpdate()
    {
        if (shadow == null) return;

        if (!target.enabled || !target.gameObject.activeInHierarchy || target.sprite == null)
        {
            shadow.enabled = false;
            return;
        }
        shadow.enabled = true;

        Bounds b = target.bounds;
        float feetY = b.min.y;
        Vector3 origin = new Vector3(b.center.x, feetY + 0.3f, transform.position.z);

        float groundY;
        if (!TryFindGround(origin, out groundY)) groundY = fallbackGroundY;

        // Smooth width so animation frames of different widths don't make it jitter.
        float targetWidth = b.size.x * widthScale;
        width = Mathf.Lerp(width, targetWidth, 1f - Mathf.Exp(-12f * Time.deltaTime));

        float height = Mathf.Max(0f, feetY - groundY);
        float t = maxHeightFade > 0.0001f ? Mathf.Clamp01(height / maxHeightFade) : 0f;
        float size = Mathf.Lerp(1f, 0.6f, t);
        float alpha = opacity * Mathf.Lerp(1f, 0.4f, t);

        shadowT.position = new Vector3(b.center.x, groundY + groundOffset, transform.position.z + zOffset);
        // Sprite is 1 unit wide/tall; after rotating flat, local Y maps to world Z (depth).
        shadowT.localScale = new Vector3(width * size, width * depthRatio * size, 1f);

        shadow.color = new Color(color.r, color.g, color.b, alpha);
        shadow.sortingLayerID = target.sortingLayerID;
        shadow.sortingOrder = target.sortingOrder - Mathf.Abs(sortingOrderOffset);
    }

    bool TryFindGround(Vector3 origin, out float groundY)
    {
        int n = Physics.RaycastNonAlloc(origin, Vector3.down, hits, 30f, groundMask,
                                        QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        groundY = 0f;
        bool found = false;
        for (int i = 0; i < n; i++)
        {
            Collider c = hits[i].collider;
            if (c.transform.IsChildOf(transform)) continue;      // our own collider
            if (c.attachedRigidbody != null) continue;           // other characters/enemies
            if (hits[i].distance < best)
            {
                best = hits[i].distance;
                groundY = hits[i].point.y;
                found = true;
            }
        }
        return found;
    }

    static Sprite GetBlobSprite()
    {
        if (blobSprite != null) return blobSprite;

        const int S = 64;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave
        };
        for (int y = 0; y < S; y++)
        {
            for (int x = 0; x < S; x++)
            {
                float dx = (x + 0.5f) / S * 2f - 1f;
                float dy = (y + 0.5f) / S * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = 1f - Mathf.SmoothStep(0.1f, 1f, d); // soft falloff to the edge
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();

        blobSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), S);
        blobSprite.hideFlags = HideFlags.HideAndDontSave;
        return blobSprite;
    }
}
