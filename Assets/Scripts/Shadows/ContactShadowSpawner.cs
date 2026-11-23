using System;
using System.Collections.Generic;
using UnityEngine;

public class ContactShadowSpawner : MonoBehaviour
{
    [Tooltip("Seconds between rescans for newly spawned sprites. 0 = scan once at start.")]
    public float rescanInterval = 3f;

    [Tooltip("Sprites whose object name contains any of these are skipped (case-insensitive).")]
    public string[] excludeNameContains =
    {
        "Shadow", "Cloud", "Fog", "Icon", "Arrow", "Card", "Star", "Bullet", "Particle", "Grass"
    };

    [Tooltip("Skip sprites that are tilted/lying flat (e.g. the Card).")]
    public bool skipNonUpright = true;

    static readonly string[] AlwaysExclude = { "FloorBG", "Picnic" };

    readonly HashSet<int> _decided = new HashSet<int>();

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
            int id = r.GetInstanceID();
            if (_decided.Contains(id)) continue;
            if (skipNonUpright && r.transform.up.y < 0.9f) continue;

            _decided.Add(id);
            if (r.TryGetComponent<ContactShadow>(out _)) continue;
            if (r.GetComponentInParent<Canvas>() != null) continue;
            if (IsExcluded(r.gameObject.name)) continue;

            r.gameObject.AddComponent<ContactShadow>();
        }
    }

    bool IsExcluded(string objectName)
    {
        foreach (var part in AlwaysExclude)
            if (objectName.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0) return true;

        if (excludeNameContains != null)
        {
            foreach (var part in excludeNameContains)
            {
                if (string.IsNullOrEmpty(part)) continue;
                if (objectName.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
        }
        return false;
    }
}