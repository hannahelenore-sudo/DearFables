using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class OutsideHaze : MonoBehaviour
{
    [Header("Play area")]
    [Tooltip("Your ground Tilemaps. Empty = every Tilemap in the scene.")]
    public Tilemap[] playAreaTilemaps;
    [Tooltip("World size of one sample in the distance map. Smaller = more accurate, slower to build.")]
    public float sampleSize = 0.25f;

    [Header("Haze")]
    [Tooltip("Colour things fade toward. Also used as a multiplier when 'Tint Sprite Color' is on, so keep it dark indigo.")]
    public Color hazeColor = new Color(0.30f, 0.27f, 0.55f, 1f);
    [Range(0f, 1f)] public float maxHaze = 0.85f;
    [Tooltip("How far outside the ground (world units) before haze begins. 0 = right at the edge.")]
    public float startDistance = 0.5f;
    [Tooltip("Distance over which haze ramps from none to max.")]
    public float fadeDistance = 4f;

    [Header("Stronger haze for background clutter (e.g. grass outside the ground)")]
    [Tooltip("Sprites whose name contains any of these get a higher MINIMUM haze once they are outside the ground. Ground-side ones stay clear.")]
    public string[] strongHazeNames = { "Grass" };
    [Range(0f, 1f)] public float strongHazeMin = 0.95f;
    [Tooltip("Distance outside the ground (world units) over which it ramps up to that minimum.")]
    public float strongHazeRamp = 0.5f;

    [Header("Which sprites")]
    [Tooltip("Drag objects here to exclude them from the haze. Their children are excluded too.")]
    public GameObject[] excludeObjects;
    [Tooltip("Any sprite whose name contains one of these is skipped (case-insensitive).")]
    public string[] excludeNameContains = { "Shadow", "Cloud", "Fog", "Icon", "Arrow", "Card", "FloorBG", "Picnic" };
    public bool skipNonUpright = true;
    [Tooltip("Seconds between rescans for newly spawned sprites. 0 = once at start.")]
    public float rescanInterval = 1f;

    [Header("How the haze is applied")]
    [Tooltip("ON: works with any sprite shader by multiplying SpriteRenderer.color (darkens + tints). " +
             "Turn OFF once your shader reads _HazeAmount/_HazeColor itself (see the shader snippet).")]
    public bool tintSpriteColor = true;

    class Entry
    {
        public SpriteRenderer r;
        public Color baseColor;
        public Color applied;
        public float lastAmount = -1f;
        public bool strong;
    }

    static readonly int HazeAmountId = Shader.PropertyToID("_HazeAmount");
    static readonly int HazeColorId = Shader.PropertyToID("_HazeColor");

    readonly Dictionary<SpriteRenderer, Entry> entries = new Dictionary<SpriteRenderer, Entry>();
    readonly List<SpriteRenderer> dead = new List<SpriteRenderer>();
    MaterialPropertyBlock mpb;

    float[] dist;
    int nx, nz;
    float originX, originZ;
    bool built;
    float scanTimer;

    void Start()
    {
        mpb = new MaterialPropertyBlock();
        Rebuild();
        Scan();
        scanTimer = rescanInterval;
    }

    void OnEnable()
    {
        if (built) Scan();
    }

    void OnDisable()
    {
        ResetAll();
    }

    [ContextMenu("Rebuild Distance Map")]
    public void Rebuild()
    {
        Tilemap[] maps = (playAreaTilemaps != null && playAreaTilemaps.Length > 0)
            ? playAreaTilemaps
            : FindObjectsByType<Tilemap>(FindObjectsSortMode.None);

        bool any = false;
        Bounds wb = new Bounds();
        foreach (var m in maps)
        {
            if (m == null) continue;
            BoundsInt cb = m.cellBounds;
            if (cb.size.x <= 0 || cb.size.y <= 0) continue;
            for (int ix = 0; ix < 2; ix++)
            {
                for (int iy = 0; iy < 2; iy++)
                {
                    Vector3 w = m.CellToWorld(new Vector3Int(ix == 0 ? cb.xMin : cb.xMax,
                                                             iy == 0 ? cb.yMin : cb.yMax, 0));
                    if (!any) { wb = new Bounds(w, Vector3.zero); any = true; }
                    else wb.Encapsulate(w);
                }
            }
        }

        if (!any)
        {
            Debug.LogWarning("OutsideHaze: no Tilemap with tiles found. Drag your ground tilemaps into Play Area Tilemaps.", this);
            built = false;
            return;
        }

        float s = Mathf.Max(0.05f, sampleSize);
        float margin = startDistance + fadeDistance + 1f;
        originX = wb.min.x - margin;
        originZ = wb.min.z - margin;
        nx = Mathf.CeilToInt((wb.size.x + margin * 2f) / s) + 1;
        nz = Mathf.CeilToInt((wb.size.z + margin * 2f) / s) + 1;
        dist = new float[nx * nz];

        const float Big = 1e6f;
        for (int z = 0; z < nz; z++)
        {
            for (int x = 0; x < nx; x++)
            {
                float wx = originX + x * s;
                float wz = originZ + z * s;
                bool inside = false;
                foreach (var m in maps)
                {
                    if (m == null) continue;
                    Vector3Int c = m.WorldToCell(new Vector3(wx, m.transform.position.y, wz));
                    c.z = 0;
                    if (m.HasTile(c)) { inside = true; break; }
                }
                dist[z * nx + x] = inside ? 0f : Big;
            }
        }

        const float D1 = 1f, D2 = 1.41421356f;
        for (int z = 0; z < nz; z++)
        {
            for (int x = 0; x < nx; x++)
            {
                int i = z * nx + x;
                float d = dist[i];
                if (x > 0) d = Mathf.Min(d, dist[i - 1] + D1);
                if (z > 0)
                {
                    d = Mathf.Min(d, dist[i - nx] + D1);
                    if (x > 0) d = Mathf.Min(d, dist[i - nx - 1] + D2);
                    if (x < nx - 1) d = Mathf.Min(d, dist[i - nx + 1] + D2);
                }
                dist[i] = d;
            }
        }
        for (int z = nz - 1; z >= 0; z--)
        {
            for (int x = nx - 1; x >= 0; x--)
            {
                int i = z * nx + x;
                float d = dist[i];
                if (x < nx - 1) d = Mathf.Min(d, dist[i + 1] + D1);
                if (z < nz - 1)
                {
                    d = Mathf.Min(d, dist[i + nx] + D1);
                    if (x < nx - 1) d = Mathf.Min(d, dist[i + nx + 1] + D2);
                    if (x > 0) d = Mathf.Min(d, dist[i + nx - 1] + D2);
                }
                dist[i] = d;
            }
        }
        for (int i = 0; i < dist.Length; i++) dist[i] *= s;

        built = true;
    }

    float SampleDistance(float x, float z)
    {
        float s = Mathf.Max(0.05f, sampleSize);
        float fx = (x - originX) / s;
        float fz = (z - originZ) / s;
        if (fx < 0f || fz < 0f || fx > nx - 1 || fz > nz - 1) return float.MaxValue;

        int x0 = Mathf.Min((int)fx, nx - 2);
        int z0 = Mathf.Min((int)fz, nz - 2);
        float tx = fx - x0, tz = fz - z0;

        float d00 = dist[z0 * nx + x0];
        float d10 = dist[z0 * nx + x0 + 1];
        float d01 = dist[(z0 + 1) * nx + x0];
        float d11 = dist[(z0 + 1) * nx + x0 + 1];
        return Mathf.Lerp(Mathf.Lerp(d00, d10, tx), Mathf.Lerp(d01, d11, tx), tz);
    }

    void Scan()
    {
        dead.Clear();
        foreach (var kv in entries)
        {
            if (kv.Key == null) { dead.Add(kv.Key); continue; }
            if (IsExcluded(kv.Key)) { Restore(kv.Value); dead.Add(kv.Key); }
        }
        foreach (var key in dead) entries.Remove(key);

        var all = FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None);
        foreach (var r in all)
        {
            if (r.sprite == null) continue;
            if (entries.ContainsKey(r)) continue;
            if (r.GetComponentInParent<Canvas>() != null) continue;
            if (skipNonUpright && r.transform.up.y < 0.9f) continue;
            if (IsExcluded(r)) continue;

            entries[r] = new Entry { r = r, baseColor = r.color, applied = r.color, strong = MatchesStrong(r.gameObject.name) };
        }
    }

    bool IsExcluded(SpriteRenderer r)
    {
        if (excludeObjects != null)
        {
            foreach (var go in excludeObjects)
            {
                if (go == null) continue;
                if (r.transform == go.transform || r.transform.IsChildOf(go.transform)) return true;
            }
        }

        string objectName = r.gameObject.name;
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

    bool MatchesStrong(string objectName)
    {
        if (strongHazeNames == null) return false;
        foreach (var part in strongHazeNames)
        {
            if (string.IsNullOrEmpty(part)) continue;
            if (objectName.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0) return true;
        }
        return false;
    }

    void LateUpdate()
    {
        if (!built) return;

        if (rescanInterval > 0f)
        {
            scanTimer -= Time.deltaTime;
            if (scanTimer <= 0f) { Scan(); scanTimer = rescanInterval; }
        }

        dead.Clear();
        float end = startDistance + Mathf.Max(0.01f, fadeDistance);

        foreach (var kv in entries)
        {
            Entry e = kv.Value;
            if (e.r == null) { dead.Add(kv.Key); continue; }
            if (!e.r.enabled || !e.r.gameObject.activeInHierarchy) continue;

            float d = SampleDistance(e.r.bounds.center.x, e.r.transform.position.z);
            float t = Mathf.InverseLerp(startDistance, end, d);
            float amount = maxHaze * (t * t * (3f - 2f * t));

            if (e.strong)
            {
                float k = Mathf.Clamp01(d / Mathf.Max(0.01f, strongHazeRamp));
                amount = Mathf.Max(amount, strongHazeMin * (k * k * (3f - 2f * k)));
            }

            Apply(e, amount);
        }

        foreach (var key in dead) entries.Remove(key);
    }

    void Apply(Entry e, float amount)
    {
        if (Mathf.Abs(amount - e.lastAmount) > 0.003f)
        {
            e.lastAmount = amount;
            e.r.GetPropertyBlock(mpb);
            mpb.SetFloat(HazeAmountId, amount);
            mpb.SetColor(HazeColorId, hazeColor);
            e.r.SetPropertyBlock(mpb);
        }

        if (tintSpriteColor)
        {
            if (e.r.color != e.applied) e.baseColor = e.r.color;
            Color c = e.baseColor * Color.Lerp(Color.white, hazeColor, amount);
            c.a = e.baseColor.a;
            e.r.color = c;
            e.applied = c;
        }
    }

    void Restore(Entry e)
    {
        if (e.r == null) return;
        if (tintSpriteColor) e.r.color = e.baseColor;
        e.r.GetPropertyBlock(mpb);
        mpb.SetFloat(HazeAmountId, 0f);
        e.r.SetPropertyBlock(mpb);
    }

    void ResetAll()
    {
        foreach (var kv in entries)
        {
            Entry e = kv.Value;
            if (e.r == null) continue;
            if (tintSpriteColor) e.r.color = e.baseColor;
            e.r.GetPropertyBlock(mpb);
            mpb.SetFloat(HazeAmountId, 0f);
            e.r.SetPropertyBlock(mpb);
        }
        entries.Clear();
    }
}