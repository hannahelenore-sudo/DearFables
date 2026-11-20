using System;
using UnityEngine;

/// <summary>
/// Drop this on one empty GameObject in the scene. It finds every world SpriteRenderer
/// and gives it a ContactShadow. It rescans periodically, so trees, enemies and other
/// things spawned at runtime get shadows too.
/// </summary>
public class ContactShadowSpawner : MonoBehaviour
{
    [Tooltip("Seconds between rescans for newly spawned sprites. 0 = scan once at start.")]
    public float rescanInterval = 1f;

    [Tooltip("Sprites whose object name contains any of these are skipped (case-insensitive).")]
    public string[] excludeNameContains =
    {
        "Shadow", "Cloud", "Fog", "Icon", "Arrow", "Card", "Star", "Bullet", "Particle", "Grass"
    };

    [Tooltip("Skip sprites that are tilted/lying flat (e.g. the Card).")]
    public bool skipNonUpright = true;

    void Start()
    {
        Scan();
        if (rescanInterval > 0f)
            InvokeRepeating(nameof(Scan), rescanInterval, rescanInterval);
    }

    void Scan()
    {
        var all = FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None);
        foreach (var r in all)
        {
            if (r.sprite == null) continue;
            if (r.TryGetComponent<ContactShadow>(out _)) continue;
            if (r.GetComponentInParent<Canvas>() != null) continue;          // UI
            if (skipNonUpright && r.transform.up.y < 0.9f) continue;         // flat/tilted
            if (IsExcluded(r.gameObject.name)) continue;

            r.gameObject.AddComponent<ContactShadow>();
        }
    }

    bool IsExcluded(string objectName)
    {
        foreach (var part in excludeNameContains)
        {
            if (string.IsNullOrEmpty(part)) continue;
            if (objectName.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0) return true;
        }
        return false;
    }
}
