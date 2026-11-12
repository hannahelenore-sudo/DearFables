using UnityEngine;

/// <summary>
/// Shows the CURRENTLY SELECTED item as one small icon above the player's head.
/// It pops in when you switch items or use one, stays for 'showTime' seconds, then fades out.
/// Added automatically by CardShooter.
/// </summary>
public class HeldItemIcons : MonoBehaviour
{
    [Tooltip("Height of the icon as a fraction of the character's height.")]
    public float iconHeightRatio = 0.28f;
    [Tooltip("Extra gap above the head, as a fraction of the character's height.")]
    public float heightAbove = 0.15f;
    [Tooltip("Seconds the icon stays after you switch or use an item.")]
    public float showTime = 1.5f;
    public float fadeSpeed = 6f;
    public float popTime = 0.25f;
    public float popStrength = 0.35f;

    private SpriteRenderer character;
    private Transform root;
    private SpriteRenderer icon;
    private float visibleTimer;
    private float popTimer;
    private float alpha;

    public void Init(SpriteRenderer characterRenderer)
    {
        character = characterRenderer;
        EnsureIcon();
    }

    private void EnsureIcon()
    {
        if (root != null) return;

        // Not parented to the player, so the player's scale/rotation can't distort the icon
        root = new GameObject("HeldItemIcon").transform;
        icon = root.gameObject.AddComponent<SpriteRenderer>();
        icon.enabled = false;
        if (character != null)
        {
            icon.sortingLayerID = character.sortingLayerID;
            icon.sortingOrder = character.sortingOrder + 10;
        }
        else icon.sortingOrder = 10;
    }

    /// <summary>Shows this sprite for 'showTime' seconds. pop = bounce (use when switching items).</summary>
    public void Show(Sprite sprite, bool pop)
    {
        EnsureIcon();

        if (icon.sprite != sprite) pop = true;
        icon.sprite = sprite;
        visibleTimer = showTime;
        if (pop) popTimer = popTime;
    }

    private void LateUpdate()
    {
        Camera cam = Camera.main;
        if (cam == null || icon == null) return;

        float dt = Time.unscaledDeltaTime;

        visibleTimer = Mathf.Max(0f, visibleTimer - dt);
        popTimer = Mathf.Max(0f, popTimer - dt);
        alpha = Mathf.MoveTowards(alpha, visibleTimer > 0f ? 1f : 0f, fadeSpeed * dt);

        if (alpha <= 0.01f || icon.sprite == null)
        {
            icon.enabled = false;
            return;
        }
        icon.enabled = true;

        float charH = 2f;
        Vector3 head = transform.position + Vector3.up * 2f;
        if (character != null)
        {
            Bounds b = character.bounds;
            charH = Mathf.Max(0.5f, b.size.y);
            head = new Vector3(transform.position.x, b.max.y, transform.position.z);
        }

        float size = charH * iconHeightRatio;
        head += Vector3.up * (charH * heightAbove + size * 0.5f);

        float pop = popTime > 0f ? 1f + popStrength * Mathf.Sin((1f - popTimer / popTime) * Mathf.PI) : 1f;
        float spriteH = Mathf.Max(0.0001f, icon.sprite.bounds.size.y);
        float appear = Mathf.Lerp(0.6f, 1f, alpha); // grows slightly as it fades in

        root.position = head;
        root.rotation = cam.transform.rotation; // always faces the camera
        root.localScale = Vector3.one * (size / spriteH * pop * appear);

        Color c = icon.color;
        c.a = alpha;
        icon.color = c;
    }

    private void OnDestroy()
    {
        if (root != null) Destroy(root.gameObject);
    }
}