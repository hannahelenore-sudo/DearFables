using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shows the items you own as little icons above the player's head.
/// The selected item is big and bright, the others are small and dim. Added automatically by CardShooter.
/// </summary>
public class HeldItemIcons : MonoBehaviour
{
    [Tooltip("Height of the selected icon as a fraction of the character's height.")]
    public float iconHeightRatio = 0.28f;
    [Tooltip("Size of the items that are not selected (1 = same as selected).")]
    [Range(0.2f, 1f)] public float otherIconScale = 0.6f;
    [Tooltip("Gap between icons, in icon heights.")]
    public float spacing = 1.2f;
    [Tooltip("Extra gap above the head, as a fraction of the character's height.")]
    public float heightAbove = 0.15f;
    [Range(0f, 1f)] public float otherAlpha = 0.55f;
    public float popTime = 0.25f;
    public float popStrength = 0.35f;

    private SpriteRenderer character;
    private Transform root;
    private readonly List<SpriteRenderer> icons = new List<SpriteRenderer>();
    private readonly List<float> scaleMult = new List<float>();
    private int selected;
    private float popTimer;

    public void Init(SpriteRenderer characterRenderer)
    {
        character = characterRenderer;
        EnsureRoot();
    }

    private void EnsureRoot()
    {
        // Not parented to the player, so the player's scale/rotation can't distort the icons
        if (root == null) root = new GameObject("HeldItemIcons").transform;
    }

    public void SetItems(List<Sprite> sprites, int selectedIndex)
    {
        EnsureRoot();

        bool same = sprites.Count == icons.Count;
        for (int i = 0; same && i < icons.Count; i++)
            if (icons[i] == null || icons[i].sprite != sprites[i]) same = false;

        if (!same)
        {
            foreach (SpriteRenderer old in icons)
                if (old != null) Destroy(old.gameObject);
            icons.Clear();
            scaleMult.Clear();

            for (int i = 0; i < sprites.Count; i++)
            {
                GameObject go = new GameObject("Icon" + i);
                go.transform.SetParent(root, false);
                SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprites[i];
                if (character != null)
                {
                    sr.sortingLayerID = character.sortingLayerID;
                    sr.sortingOrder = character.sortingOrder + 10;
                }
                else sr.sortingOrder = 10;

                icons.Add(sr);
                scaleMult.Add(0f); // grows in
            }
        }

        if (!same || selectedIndex != selected) popTimer = popTime;
        selected = selectedIndex;
    }

    private void LateUpdate()
    {
        Camera cam = Camera.main;
        if (cam == null || root == null || icons.Count == 0) return;

        float dt = Time.unscaledDeltaTime;

        float charH = 2f;
        Vector3 head = transform.position + Vector3.up * 2f;
        if (character != null)
        {
            Bounds b = character.bounds;
            charH = Mathf.Max(0.5f, b.size.y);
            head = new Vector3(transform.position.x, b.max.y, transform.position.z);
        }

        float baseSize = charH * iconHeightRatio;
        head += Vector3.up * (charH * heightAbove + baseSize * 0.5f);

        root.position = head;
        root.rotation = cam.transform.rotation; // always faces the camera

        popTimer = Mathf.Max(0f, popTimer - dt);
        float pop = popTime > 0f ? 1f + popStrength * Mathf.Sin((1f - popTimer / popTime) * Mathf.PI) : 1f;

        for (int i = 0; i < icons.Count; i++)
        {
            SpriteRenderer sr = icons[i];
            if (sr == null) continue;

            bool isSel = i == selected;
            float target = isSel ? 1f : otherIconScale;
            scaleMult[i] = Mathf.Lerp(scaleMult[i], target, 1f - Mathf.Exp(-18f * dt));
            float m = scaleMult[i] * (isSel ? pop : 1f);

            float spriteH = sr.sprite != null ? Mathf.Max(0.0001f, sr.sprite.bounds.size.y) : 1f;
            Transform t = sr.transform;
            t.localScale = Vector3.one * (baseSize / spriteH * m);
            t.localPosition = new Vector3((i - (icons.Count - 1) * 0.5f) * spacing * baseSize, 0f, 0f);

            Color c = sr.color;
            c.a = Mathf.Lerp(c.a, isSel ? 1f : otherAlpha, 1f - Mathf.Exp(-14f * dt));
            sr.color = c;
        }
    }

    private void OnDestroy()
    {
        if (root != null) Destroy(root.gameObject);
    }
}
