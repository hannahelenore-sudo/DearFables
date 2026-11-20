using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Spawns two separate groups of grass inside the Spawn Area:
///   1. Default Prefabs  - the main grass. Own amount, own size.
///   2. Variants         - the extra / rarer pieces. Own amount, per-entry size and spawn weight.
/// </summary>
public class GrassSpawner : MonoBehaviour
{
    [Serializable]
    public class GrassVariant
    {
        [Tooltip("The picture for this variant. This is all you need: no prefab required.")]
        public Sprite sprite;

        [Tooltip("Optional. Only if this one entry needs its own prefab. Normally leave empty.")]
        public GameObject prefab;

        [Tooltip("How often this one spawns compared to the other variants. 2 = twice as often as 1, 0 = never.")]
        [Min(0f)] public float spawnWeight = 1f;

        [Tooltip("ON: this entry uses its own size range below. OFF: it uses the Variant Size at the bottom.")]
        public bool useOwnScale = false;
        public float minScale = 0.8f;
        public float maxScale = 1.2f;
    }

    [Header("Spawn Area")]
    public BoxCollider spawnArea;

    // ------------------------------------------------------------------
    [Header("Default Prefabs (the main grass, its own group)")]
    [Tooltip("Add as many as you like. Each piece picks one of them at random. These do not use the Variants list.")]
    public GameObject[] defaultPrefabs;
    [Tooltip("How many default pieces to spawn in total.")]
    public int defaultAmount = 100;
    [Tooltip("Size range of the default pieces.")]
    public float defaultMinScale = 0.8f;
    public float defaultMaxScale = 1.2f;

    // ------------------------------------------------------------------
    [Header("Variants (extra pieces, a separate group)")]
    [Tooltip("Optional. One prefab used by every variant (e.g. a plain sprite object). Empty = variants are made as simple sprite objects, no prefab needed.")]
    public GameObject variantPrefab;
    public GrassVariant[] variants;
    [Tooltip("How many variant pieces to spawn in total. The weights decide how they are split between the entries.")]
    [FormerlySerializedAs("amount")]
    public int variantAmount = 30;
    [Header("Variant Size (used by entries without their own size)")]
    public float minScale = 0.8f;
    public float maxScale = 1.2f;

    // ------------------------------------------------------------------
    [Header("Height")]
    [Tooltip("Raises or lowers all spawned sprites. Positive = up, negative = down.")]
    public float heightOffset = 0f;
    [Tooltip("Optional random +/- variation added on top of the height offset.")]
    public float heightVariation = 0f;

    [Header("Live size (change it while playing)")]
    [Tooltip("Multiplies the size of EVERYTHING spawned (defaults and variants). Drag it during play to see how big things are. 1 = normal.")]
    [Range(0.05f, 5f)] public float sizeMultiplier = 1f;

    // Remembers every spawned piece so its size can be re-applied when you change a value in play mode
    private class SpawnedPiece
    {
        public Transform transform;
        public bool isDefault;
        public int variantIndex;
        public float sizeRoll; // 0..1: where in the min..max range this piece landed
    }

    private readonly List<SpawnedPiece> spawnedPieces = new List<SpawnedPiece>();

    // ------------------------------------------------------------------

    void Start()
    {
        SpawnAll();
    }

    void SpawnAll()
    {
        if (spawnArea == null) spawnArea = GetComponent<BoxCollider>();
        if (spawnArea == null)
        {
            Debug.LogWarning("GrassSpawner: nothing spawned because Spawn Area is empty. Drag a BoxCollider into it.", this);
            return;
        }

        int defaults = 0, extras = 0;

        // Group 1: defaults
        if (CountDefaults() == 0)
        {
            Debug.LogWarning("GrassSpawner: no Default Prefabs set, so no default grass spawned.", this);
        }
        else
        {
            for (int i = 0; i < defaultAmount; i++)
                if (SpawnDefault()) defaults++;
        }

        // Group 2: variants
        if (variants == null || variants.Length == 0)
        {
            // no variants is fine, they are optional
        }
        else if (TotalVariantWeight() <= 0f)
        {
            Debug.LogWarning("GrassSpawner: no variants spawned. Each entry needs a Sprite and a Spawn Weight above 0.", this);
        }
        else
        {
            for (int i = 0; i < variantAmount; i++)
                if (SpawnVariant()) extras++;
        }

        Debug.Log("GrassSpawner: spawned " + defaults + " default and " + extras + " variant piece(s).", this);
    }

    // ------------------------------------------------------------------
    // Spawning
    // ------------------------------------------------------------------

    bool SpawnDefault()
    {
        GameObject prefab = PickDefaultPrefab();
        if (prefab == null) return false;

        GameObject grass = Place(prefab);
        spawnedPieces.Add(NewPiece(grass, true, -1));
        ApplyScale(spawnedPieces[spawnedPieces.Count - 1]);
        return true;
    }

    bool SpawnVariant()
    {
        GrassVariant variant = PickVariant();
        if (variant == null) return false;

        // Prefab order: this entry's own prefab > the shared Variant Prefab > (none) a plain sprite object is created
        GameObject prefab = variant.prefab != null ? variant.prefab : variantPrefab;

        GameObject grass = Place(prefab);

        SpriteRenderer sprite = grass.GetComponent<SpriteRenderer>();
        if (sprite != null && variant.sprite != null) sprite.sprite = variant.sprite;

        spawnedPieces.Add(NewPiece(grass, false, Array.IndexOf(variants, variant)));
        ApplyScale(spawnedPieces[spawnedPieces.Count - 1]);
        return true;
    }

    GameObject Place(GameObject prefab)
    {
        Bounds bounds = spawnArea.bounds;

        float x = UnityEngine.Random.Range(bounds.min.x, bounds.max.x);
        float z = UnityEngine.Random.Range(bounds.min.z, bounds.max.z);

        float y = transform.position.y
                  + heightOffset
                  + UnityEngine.Random.Range(-heightVariation, heightVariation);

        Vector3 position = new Vector3(x, y, z);
        GameObject grass;
        if (prefab != null)
        {
            grass = Instantiate(prefab, position, Quaternion.identity, transform);
        }
        else
        {
            // No prefab at all: make a plain object that just shows the variant's sprite
            grass = new GameObject("GrassVariant");
            grass.transform.SetParent(transform, false);
            grass.transform.position = position;
            grass.AddComponent<SpriteRenderer>();
        }

        SpriteRenderer sprite = grass.GetComponent<SpriteRenderer>();
        if (sprite != null) sprite.flipX = UnityEngine.Random.value > 0.5f;

        return grass;
    }

    SpawnedPiece NewPiece(GameObject grass, bool isDefault, int variantIndex)
    {
        return new SpawnedPiece
        {
            transform = grass.transform,
            isDefault = isDefault,
            variantIndex = variantIndex,
            sizeRoll = UnityEngine.Random.value
        };
    }

    // ------------------------------------------------------------------
    // Picking
    // ------------------------------------------------------------------

    int CountDefaults()
    {
        int count = 0;
        if (defaultPrefabs != null)
            foreach (GameObject p in defaultPrefabs) if (p != null) count++;
        return count;
    }

    GameObject PickDefaultPrefab()
    {
        int count = CountDefaults();
        if (count == 0) return null;

        int pick = UnityEngine.Random.Range(0, count);
        foreach (GameObject p in defaultPrefabs)
        {
            if (p == null) continue;
            if (pick-- == 0) return p;
        }
        return null;
    }

    float WeightOf(GrassVariant v)
    {
        if (v == null) return 0f;
        if (v.sprite == null && v.prefab == null && variantPrefab == null) return 0f; // nothing to show
        return v.spawnWeight;
    }

    float TotalVariantWeight()
    {
        float total = 0f;
        foreach (GrassVariant v in variants) total += WeightOf(v);
        return total;
    }

    GrassVariant PickVariant()
    {
        float total = TotalVariantWeight();
        if (total <= 0f) return null;

        float roll = UnityEngine.Random.value * total;
        GrassVariant last = null;
        foreach (GrassVariant v in variants)
        {
            float w = WeightOf(v);
            if (w <= 0f) continue;
            last = v;
            roll -= w;
            if (roll <= 0f) return v;
        }
        return last; // floating point leftovers
    }

    // ------------------------------------------------------------------
    // Live sizing
    // ------------------------------------------------------------------

    float ScaleFor(SpawnedPiece piece)
    {
        float lo, hi;

        if (piece.isDefault)
        {
            lo = defaultMinScale;
            hi = defaultMaxScale;
        }
        else
        {
            GrassVariant v = (variants != null && piece.variantIndex >= 0 && piece.variantIndex < variants.Length)
                ? variants[piece.variantIndex] : null;
            bool own = v != null && v.useOwnScale;
            lo = own ? v.minScale : minScale;
            hi = own ? v.maxScale : maxScale;
        }

        return Mathf.Lerp(lo, hi, piece.sizeRoll) * sizeMultiplier;
    }

    void ApplyScale(SpawnedPiece piece)
    {
        if (piece.transform == null) return;
        float scale = ScaleFor(piece);
        piece.transform.localScale = new Vector3(scale, scale, scale);
    }

    void ApplyAllScales()
    {
        for (int i = spawnedPieces.Count - 1; i >= 0; i--)
        {
            if (spawnedPieces[i].transform == null) { spawnedPieces.RemoveAt(i); continue; }
            ApplyScale(spawnedPieces[i]);
        }
    }

    // Called by Unity whenever a value changes in the inspector (also while the game is running)
    void OnValidate()
    {
        if (Application.isPlaying && spawnedPieces.Count > 0) ApplyAllScales();
    }

    /// <summary>Right-click the component header > "Respawn" to re-roll everything while playing.</summary>
    [ContextMenu("Respawn")]
    public void Respawn()
    {
        if (!Application.isPlaying) return;

        foreach (SpawnedPiece piece in spawnedPieces)
            if (piece.transform != null) Destroy(piece.transform.gameObject);
        spawnedPieces.Clear();

        SpawnAll();
    }
}