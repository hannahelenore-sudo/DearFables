using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Tilemaps;

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

    [Header("Default Prefabs (the main grass, its own group)")]
    [Tooltip("Add as many as you like. Each piece picks one of them at random. These do not use the Variants list.")]
    public GameObject[] defaultPrefabs;
    [Tooltip("How many default pieces to spawn in total.")]
    public int defaultAmount = 100;
    [Tooltip("Size range of the default pieces.")]
    public float defaultMinScale = 0.8f;
    public float defaultMaxScale = 1.2f;

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

    [Header("Where it may spawn")]
    [Tooltip("ON: nothing spawns on the ground tilemap, only outside it (the hazy background).")]
    public bool onlyOutsideTilemap = true;
    [Tooltip("Your ground Tilemaps. Empty = every Tilemap in the scene.")]
    public Tilemap[] groundTilemaps;
    [Tooltip("Keep this far away from the ground edge (world units). Pushes the grass further into the haze.")]
    public float distanceFromGround = 1f;
    [Tooltip("How many random spots to try per piece before giving up on it.")]
    public int maxPlacementTries = 40;

    [Header("Height")]
    [Tooltip("Raises or lowers all spawned sprites. Positive = up, negative = down.")]
    public float heightOffset = 0f;
    [Tooltip("Optional random +/- variation added on top of the height offset.")]
    public float heightVariation = 0f;

    [Header("Live size (change it while playing)")]
    [Tooltip("Multiplies the size of EVERYTHING spawned (defaults and variants). Drag it during play to see how big things are. 1 = normal.")]
    [Range(0.05f, 5f)] public float sizeMultiplier = 1f;

    [Header("Sway (live)")]
    [Tooltip("The default grass sways in the wind, bending around its base.")]
    public bool swayDefaults = true;
    [Tooltip("Also sway the variant pieces.")]
    public bool swayVariants = false;
    [Tooltip("Maximum lean in degrees. 3-8 is gentle, 15+ is stormy.")]
    [Range(0f, 25f)] public float swayAngle = 5f;
    [Tooltip("How fast each blade rocks back and forth.")]
    public float swaySpeed = 1.2f;
    [Tooltip("0 = every blade rocks on its own. 1 = a wind gust sweeps across the field and leans them together.")]
    [Range(0f, 1f)] public float gust = 0.5f;
    [Tooltip("Size of the gusts. Smaller = wider waves across the field.")]
    public float gustScale = 0.15f;
    [Tooltip("How fast gusts travel across the field.")]
    public float gustSpeed = 0.35f;

    [Header("Distance Culling (less lag)")]
    [Tooltip("ON: grass far away from the player is switched off, and switched back on when the player comes close.")]
    public bool cullByDistance = true;
    [Tooltip("The player. Empty = finds the object tagged 'Player', or falls back to the main camera.")]
    public Transform player;
    [Tooltip("Grass closer than this to the player (world units) is visible.")]
    public string playerTag = "Player";
    public float activeDistance = 30f;
    [Tooltip("Extra distance before an active piece switches off, so pieces at the edge don't flicker on and off.")]
    public float cullBuffer = 2f;
    [Tooltip("How often (seconds) the distances are checked. 0.25 is plenty.")]
    public float cullCheckInterval = 0.25f;

    private float nextCullCheck;

    private class SpawnedPiece
    {
        public bool active = true;
        public Transform transform;
        public SpriteRenderer sprite;
        public bool isDefault;
        public int variantIndex;
        public float sizeRoll;
        public Vector3 basePos;
        public Vector3 feet;
        public float phase;
    }

    private readonly List<SpawnedPiece> spawnedPieces = new List<SpawnedPiece>();
    private Tilemap[] groundMaps;

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

        FindGroundMaps();

        int defaults = 0, extras = 0, skipped = 0;

        if (CountDefaults() == 0)
        {
            Debug.LogWarning("GrassSpawner: no Default Prefabs set, so no default grass spawned.", this);
        }
        else
        {
            for (int i = 0; i < defaultAmount; i++)
            {
                if (SpawnDefault()) defaults++; else skipped++;
            }
        }

        if (variants != null && variants.Length > 0)
        {
            if (TotalVariantWeight() <= 0f)
            {
                Debug.LogWarning("GrassSpawner: no variants spawned. Each entry needs a Sprite and a Spawn Weight above 0.", this);
            }
            else
            {
                for (int i = 0; i < variantAmount; i++)
                {
                    if (SpawnVariant()) extras++; else skipped++;
                }
            }
        }

        Debug.Log("GrassSpawner: spawned " + defaults + " default and " + extras + " variant piece(s)."
                  + (skipped > 0 ? " " + skipped + " skipped (no free spot outside the tilemap, try a bigger Spawn Area or a smaller Distance From Ground)." : ""), this);
    }

    void FindGroundMaps()
    {
        groundMaps = (groundTilemaps != null && groundTilemaps.Length > 0)
            ? groundTilemaps
            : FindObjectsByType<Tilemap>(FindObjectsSortMode.None);

        if (onlyOutsideTilemap && (groundMaps == null || groundMaps.Length == 0))
            Debug.LogWarning("GrassSpawner: 'Only Outside Tilemap' is on but no Tilemap was found, so grass can spawn anywhere.", this);
    }

    bool OnGround(Vector3 p)
    {
        if (groundMaps == null) return false;
        foreach (Tilemap m in groundMaps)
        {
            if (m == null) continue;
            Vector3Int c = m.WorldToCell(new Vector3(p.x, m.transform.position.y, p.z));
            c.z = 0;
            if (m.HasTile(c)) return true;
        }
        return false;
    }

    bool IsBlocked(Vector3 p)
    {
        if (!onlyOutsideTilemap) return false;
        if (OnGround(p)) return true;

        if (distanceFromGround > 0.001f)
        {
            for (int k = 1; k <= 2; k++)
            {
                float r = distanceFromGround * k * 0.5f;
                for (int i = 0; i < 8; i++)
                {
                    float a = i * Mathf.PI * 0.25f;
                    if (OnGround(p + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r))) return true;
                }
            }
        }
        return false;
    }

    bool SpawnDefault()
    {
        GameObject prefab = PickDefaultPrefab();
        if (prefab == null) return false;

        GameObject grass = Place(prefab);
        if (grass == null) return false;

        SpawnedPiece piece = NewPiece(grass, true, -1);
        spawnedPieces.Add(piece);
        ApplyScale(piece);
        return true;
    }

    bool SpawnVariant()
    {
        GrassVariant variant = PickVariant();
        if (variant == null) return false;

        GameObject prefab = variant.prefab != null ? variant.prefab : variantPrefab;

        GameObject grass = Place(prefab);
        if (grass == null) return false;

        SpriteRenderer sprite = grass.GetComponent<SpriteRenderer>();
        if (sprite != null && variant.sprite != null) sprite.sprite = variant.sprite;

        SpawnedPiece piece = NewPiece(grass, false, Array.IndexOf(variants, variant));
        spawnedPieces.Add(piece);
        ApplyScale(piece);
        return true;
    }

    GameObject Place(GameObject prefab)
    {
        Bounds bounds = spawnArea.bounds;

        Vector3 position = Vector3.zero;
        bool found = false;
        int tries = Mathf.Max(1, maxPlacementTries);
        for (int t = 0; t < tries; t++)
        {
            float x = UnityEngine.Random.Range(bounds.min.x, bounds.max.x);
            float z = UnityEngine.Random.Range(bounds.min.z, bounds.max.z);
            float y = transform.position.y
                      + heightOffset
                      + UnityEngine.Random.Range(-heightVariation, heightVariation);

            Vector3 candidate = new Vector3(x, y, z);
            if (IsBlocked(candidate)) continue;

            position = candidate;
            found = true;
            break;
        }
        if (!found) return null;

        GameObject grass;
        if (prefab != null)
        {
            grass = Instantiate(prefab, position, Quaternion.identity, transform);
        }
        else
        {
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
            sprite = grass.GetComponent<SpriteRenderer>(),
            isDefault = isDefault,
            variantIndex = variantIndex,
            sizeRoll = UnityEngine.Random.value,
            basePos = grass.transform.position,
            phase = UnityEngine.Random.value * Mathf.PI * 2f
        };
    }

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
        if (v.sprite == null && v.prefab == null && variantPrefab == null) return 0f;
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
        return last;
    }

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

        if (piece.sprite != null && piece.sprite.sprite != null)
            piece.feet = new Vector3(0f, piece.sprite.sprite.bounds.min.y * piece.transform.lossyScale.y, 0f);
        else
            piece.feet = Vector3.zero;
    }

    void ApplyAllScales()
    {
        for (int i = spawnedPieces.Count - 1; i >= 0; i--)
        {
            if (spawnedPieces[i].transform == null) { spawnedPieces.RemoveAt(i); continue; }
            ApplyScale(spawnedPieces[i]);
        }
    }

    void UpdateCulling()
    {
        if (player == null)
        {
            GameObject found = GameObject.FindGameObjectWithTag(playerTag);
            if (found != null) player = found.transform;
            else if (Camera.main != null) player = Camera.main.transform;
            if (player == null) return;
        }

        Vector3 pp = player.position;
        float onSqr = activeDistance * activeDistance;
        float offDist = activeDistance + Mathf.Max(0f, cullBuffer);
        float offSqr = offDist * offDist;

        for (int i = 0; i < spawnedPieces.Count; i++)
        {
            SpawnedPiece p = spawnedPieces[i];
            if (p.transform == null) continue;

            float dx = p.basePos.x - pp.x;
            float dz = p.basePos.z - pp.z;
            float sqr = dx * dx + dz * dz;

            bool shouldBeActive = p.active ? sqr <= offSqr : sqr <= onSqr;
            if (shouldBeActive != p.active)
            {
                p.active = shouldBeActive;
                p.transform.gameObject.SetActive(shouldBeActive);
            }
        }
    }

    void Update()
    {
        if (spawnedPieces.Count == 0) return;

        if (cullByDistance && Time.time >= nextCullCheck)
        {
            nextCullCheck = Time.time + cullCheckInterval;
            UpdateCulling();
        }
        else if (!cullByDistance)
        {
            for (int i = 0; i < spawnedPieces.Count; i++)
            {
                SpawnedPiece q = spawnedPieces[i];
                if (q.transform != null && !q.active)
                {
                    q.active = true;
                    q.transform.gameObject.SetActive(true);
                }
            }
        }

        float time = Time.time;
        for (int i = 0; i < spawnedPieces.Count; i++)
        {
            SpawnedPiece p = spawnedPieces[i];
            if (p.transform == null || !p.active) continue;

            bool sways = (p.isDefault ? swayDefaults : swayVariants) && swayAngle > 0.001f;
            if (!sways)
            {
                if (p.transform.rotation != Quaternion.identity)
                {
                    p.transform.rotation = Quaternion.identity;
                    p.transform.position = p.basePos;
                }
                continue;
            }

            float own = Mathf.Sin(time * swaySpeed + p.phase);
            float wind = Mathf.PerlinNoise(p.basePos.x * gustScale + time * gustSpeed, p.basePos.z * gustScale) * 2f - 1f;
            float angle = swayAngle * Mathf.Lerp(own, wind, gust);

            Quaternion rot = Quaternion.Euler(0f, 0f, angle);
            p.transform.rotation = rot;
            p.transform.position = p.basePos + p.feet - rot * p.feet;
        }
    }

    void OnValidate()
    {
        if (Application.isPlaying && spawnedPieces.Count > 0) ApplyAllScales();
    }

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