using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Tilemaps;

public class LightRays : MonoBehaviour
{
    [Header("Look (live)")]
    public Color rayColor = new Color(1f, 0.86f, 0.62f, 1f);
    [Tooltip("Overall brightness. Start low (0.2-0.5): the beams add up with Bloom.")]
    [Range(0f, 2f)] public float intensity = 0.35f;
    [Tooltip("Makes every beam wider or thinner.")]
    [Range(0.2f, 4f)] public float widthMultiplier = 1f;
    [Tooltip("Higher = softer, blurrier sides.")]
    [Range(0.05f, 1f)] public float edgeSoftness = 0.7f;
    [Tooltip("How much slow shimmering streaks show inside the beams. 0 = smooth.")]
    [Range(0f, 1f)] public float streaks = 0.35f;
    [Tooltip("How much of the top of the beam fades in (0.01-1).")]
    [Range(0.01f, 1f)] public float topFade = 0.35f;
    [Tooltip("How much of the bottom fades out where the beam meets the ground (0.01-1).")]
    [Range(0.01f, 1f)] public float bottomFade = 0.5f;

    [Header("Direction")]
    [Tooltip("ON: beams follow your Directional Light, so they match your shadows.")]
    public bool useSceneLight = true;
    [Tooltip("Used when Use Scene Light is off. The way the light travels (pointing down).")]
    public Vector3 manualDirection = new Vector3(0.35f, -1f, 0.2f);

    [Header("Beams (refills shortly after you change these)")]
    [Range(1, 60)] public int rayCount = 14;
    [Tooltip("Narrowest and widest beam, in world units.")]
    public Vector2 widthRange = new Vector2(0.8f, 2.4f);
    [Tooltip("How high above the ground the beams start.")]
    public float height = 9f;
    [Tooltip("Changes the random layout. Try other numbers until you like it.")]
    public int seed = 7;

    [Header("Where (refills when changed)")]
    [Tooltip("ON: beams spawn only on painted tiles of your tilemaps. OFF (or no tiles found): over Manual Area Size around this object.")]
    public bool fitToTilemaps = true;
    [Tooltip("Your ground Tilemaps. Empty = every Tilemap in the scene.")]
    public Tilemap[] tilemaps;
    public Vector2 manualAreaSize = new Vector2(20f, 20f);

    [Header("Movement (live)")]
    [Tooltip("How far each beam slowly drifts side to side.")]
    public float swayDistance = 0.6f;
    public float swaySpeed = 0.25f;
    [Tooltip("How much each beam slowly brightens and dims. 0 = steady.")]
    [Range(0f, 1f)] public float pulse = 0.35f;
    public float pulseSpeed = 0.4f;

    [Header("Clouds (live)")]
    [Tooltip("Beams dim where a cloud shadow drifts over them. Empty = finds the CloudShadows in the scene.")]
    public CloudShadows clouds;
    [Tooltip("How much a cloud dims the beams. 1 = they vanish completely under a cloud, 0 = clouds ignored.")]
    [Range(0f, 1f)] public float cloudDimming = 0.9f;

    [Header("Sorting")]
    [Tooltip("Leave empty for the default layer.")]
    public string sortingLayerName = "";
    [Tooltip("Higher than your sprites so the light shines over them. Lower it if you want the light behind your characters.")]
    public int sortingOrder = 400;

    [Header("Other")]
    [Tooltip("Drag LightRayBeam.shader here (keeps it in builds). Empty = looked up by name.")]
    public Shader beamShader;
    [Tooltip("Empty = main camera.")]
    public Camera targetCamera;

    class Ray
    {
        public Transform t;
        public MeshRenderer mr;
        public Vector3 center;
        public float width;
        public float length;
        public float phase;
        public float baseStrength;
        public float seed01;
    }

    static readonly int ColorId = Shader.PropertyToID("_Color");
    static readonly int IntensityId = Shader.PropertyToID("_Intensity");
    static readonly int StrengthId = Shader.PropertyToID("_Strength");
    static readonly int SeedId = Shader.PropertyToID("_Seed");
    static readonly int EdgeId = Shader.PropertyToID("_EdgeSoftness");
    static readonly int StreaksId = Shader.PropertyToID("_Streaks");
    static readonly int TopFadeId = Shader.PropertyToID("_TopFade");
    static readonly int BottomFadeId = Shader.PropertyToID("_BottomFade");

    readonly List<Ray> rays = new List<Ray>();
    Transform root;
    Material mat;
    Mesh quad;
    MaterialPropertyBlock mpb;
    Vector3 dir = Vector3.down;
    float rebuildAt = -1f;
    bool warnedCam;

    void Start()
    {
        mpb = new MaterialPropertyBlock();
        if (clouds == null) clouds = FindFirstObjectByType<CloudShadows>();
        Build();
    }

    void OnDestroy()
    {
        if (root != null) Destroy(root.gameObject);
        if (mat != null) Destroy(mat);
        if (quad != null) Destroy(quad);
    }

    void OnValidate()
    {
        if (Application.isPlaying) rebuildAt = Time.unscaledTime + 0.4f;
    }

    [ContextMenu("Rebuild Rays")]
    public void Build()
    {
        if (mpb == null) mpb = new MaterialPropertyBlock();
        ClearRays();

        if (mat == null)
        {
            Shader sh = beamShader != null ? beamShader : Shader.Find("LightRays/Beam");
            if (sh == null)
            {
                Debug.LogWarning("LightRays: shader 'LightRays/Beam' not found. Put LightRayBeam.shader under Assets/ and drag it into Beam Shader.", this);
                return;
            }
            mat = new Material(sh);
        }

        if (quad == null) quad = MakeQuad();

        var tilePool = new List<(Tilemap map, Vector3Int cell)>();
        if (fitToTilemaps) CollectTiles(tilePool);
        bool useTiles = fitToTilemaps && tilePool.Count > 0;

        Vector3 areaCenter = transform.position;
        Vector2 areaSize = manualAreaSize;

        dir = GetDirection();
        float groundY = transform.position.y;
        float length = height / Mathf.Max(0.05f, -dir.y);

        root = new GameObject("LightRayBeams").transform;
        root.SetParent(transform, false);

        var rng = new System.Random(seed);
        for (int i = 0; i < rayCount; i++)
        {
            Vector3 ground;
            if (useTiles)
            {
                var pick = tilePool[rng.Next(tilePool.Count)];
                ground = RandomPointInCell(pick.map, pick.cell, (float)rng.NextDouble(), (float)rng.NextDouble());
                ground.y = groundY;
            }
            else
            {
                float gx = areaCenter.x + ((float)rng.NextDouble() - 0.5f) * areaSize.x;
                float gz = areaCenter.z + ((float)rng.NextDouble() - 0.5f) * areaSize.y;
                ground = new Vector3(gx, groundY, gz);
            }

            Vector3 top = ground - dir * length;

            var go = new GameObject("Beam " + i);
            go.transform.SetParent(root, false);

            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = quad;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.sortingOrder = sortingOrder;
            if (!string.IsNullOrEmpty(sortingLayerName)) mr.sortingLayerName = sortingLayerName;

            float w = Mathf.Lerp(Mathf.Min(widthRange.x, widthRange.y), Mathf.Max(widthRange.x, widthRange.y), (float)rng.NextDouble());

            rays.Add(new Ray
            {
                t = go.transform,
                mr = mr,
                center = (ground + top) * 0.5f,
                width = w,
                length = length,
                phase = (float)rng.NextDouble() * Mathf.PI * 2f,
                baseStrength = Mathf.Lerp(0.5f, 1f, (float)rng.NextDouble()),
                seed01 = (float)rng.NextDouble()
            });
        }

        rebuildAt = -1f;
    }

    void ClearRays()
    {
        rays.Clear();
        if (root != null) Destroy(root.gameObject);
        root = null;
    }

    void Update()
    {
        if (rebuildAt > 0f && Time.unscaledTime >= rebuildAt) Build();
    }

    void LateUpdate()
    {
        if (mat == null || rays.Count == 0) return;

        mat.SetColor(ColorId, rayColor);
        mat.SetFloat(IntensityId, intensity);
        mat.SetFloat(EdgeId, edgeSoftness);
        mat.SetFloat(StreaksId, streaks);
        mat.SetFloat(TopFadeId, topFade);
        mat.SetFloat(BottomFadeId, bottomFade);

        Camera cam = targetCamera != null ? targetCamera : Camera.main;
        if (cam == null)
        {
            if (!warnedCam) { Debug.LogWarning("LightRays: no camera found. Tag your camera MainCamera or drag it into Target Camera.", this); warnedCam = true; }
            return;
        }

        Vector3 side = Vector3.Cross(Vector3.up, dir);
        side = side.sqrMagnitude > 0.0001f ? side.normalized : Vector3.right;
        float time = Time.time;

        foreach (Ray r in rays)
        {
            if (r.t == null) continue;

            Vector3 pos = r.center + side * (Mathf.Sin(time * swaySpeed + r.phase) * swayDistance);

            Vector3 y = dir;
            Vector3 away = pos - cam.transform.position;
            Vector3 z = away - Vector3.Dot(away, y) * y;
            if (z.sqrMagnitude < 0.0001f) z = Vector3.forward - Vector3.Dot(Vector3.forward, y) * y;
            z.Normalize();

            r.t.SetPositionAndRotation(pos, Quaternion.LookRotation(z, y));
            r.t.localScale = new Vector3(r.width * widthMultiplier, r.length, 1f);

            float breathe = 1f - pulse + pulse * (0.5f + 0.5f * Mathf.Sin(time * pulseSpeed + r.phase * 1.7f));
            float cloudMul = 1f;
            if (clouds != null && cloudDimming > 0f)
            {
                Vector3 groundPoint = pos + dir * (r.length * 0.5f);
                cloudMul = 1f - clouds.SampleShadow(groundPoint, dir) * cloudDimming;
            }

            mpb.SetFloat(StrengthId, r.baseStrength * breathe * cloudMul);
            mpb.SetFloat(SeedId, r.seed01);
            r.mr.SetPropertyBlock(mpb);
        }
    }

    Vector3 GetDirection()
    {
        Vector3 d = manualDirection;

        if (useSceneLight)
        {
            Light sun = RenderSettings.sun;
            if (sun == null)
            {
                foreach (Light l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if (l.type == LightType.Directional) { sun = l; break; }
            }
            if (sun != null) d = sun.transform.forward;
        }

        d.y = Mathf.Min(d.y, -0.25f);
        return d.normalized;
    }

    void CollectTiles(List<(Tilemap map, Vector3Int cell)> list)
    {
        Tilemap[] maps = (tilemaps != null && tilemaps.Length > 0)
            ? tilemaps
            : FindObjectsByType<Tilemap>(FindObjectsSortMode.None);

        foreach (var m in maps)
        {
            if (m == null) continue;
            foreach (Vector3Int cell in m.cellBounds.allPositionsWithin)
                if (m.HasTile(cell)) list.Add((m, cell));
        }
    }

    static Vector3 RandomPointInCell(Tilemap m, Vector3Int cell, float u, float v)
    {
        Vector3 o = m.CellToWorld(cell);
        Vector3 ex = m.CellToWorld(cell + new Vector3Int(1, 0, 0)) - o;
        Vector3 ey = m.CellToWorld(cell + new Vector3Int(0, 1, 0)) - o;
        return o + ex * u + ey * v;
    }

    static Mesh MakeQuad()
    {
        var m = new Mesh { name = "LightRayQuad" };
        m.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
            new Vector3(-0.5f,  0.5f, 0f), new Vector3(0.5f,  0.5f, 0f)
        };
        m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
        m.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        m.RecalculateBounds();
        return m;
    }
}