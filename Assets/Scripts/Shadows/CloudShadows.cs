using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
public class CloudShadows : MonoBehaviour
{
    [Header("Projected cloud shadows")]
    [Range(0f, 1f)] public float strength = 0.55f;
    [Tooltip("Roughly how big one cloud is, in world units.")]
    [Min(2f)] public float cloudSize = 22f;
    [Tooltip("Higher = fewer clouds, lower = more of the ground is shaded.")]
    [Range(0.2f, 0.8f)] public float coverage = 0.52f;
    [Tooltip("Edge softness of the shadows.")]
    [Range(0.01f, 0.4f)] public float softness = 0.12f;
    [Tooltip("How high the clouds are. Higher = shadows shift further when the sun is low.")]
    public float height = 30f;

    [Header("Wind")]
    [Range(0f, 360f)] public float windAngle = 20f;
    [Min(0f)] public float windSpeed = 1.5f;

    [Header("Optional: real shadow-casting clouds")]
    public bool useRealShadowCasters = false;
    public Material shadowMaterial;
    public Transform follow;
    [Range(1, 60)] public int cloudCount = 14;
    public Vector2 casterSize = new Vector2(8f, 20f);
    public float areaSize = 90f;
    public int seed = 0;

    static readonly int ParamsId = Shader.PropertyToID("_CloudParams");
    static readonly int ScrollId = Shader.PropertyToID("_CloudScroll");

    Vector2 _scroll;
    Transform[] _casters;
    float[] _casterSpeed;

    void OnEnable()
    {
        Push();
        if (Application.isPlaying && useRealShadowCasters) BuildCasters();
    }

    void OnDisable()
    {
        Shader.SetGlobalVector(ParamsId, Vector4.zero);
        DestroyCasters();
    }

    void OnValidate() => Push();

    void Update()
    {
        if (Application.isPlaying)
        {
            float rad = windAngle * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad));
            _scroll += dir * (windSpeed * Time.deltaTime / Mathf.Max(cloudSize, 0.1f));
            MoveCasters();
        }
        Push();
    }

    void Push()
    {
        Shader.SetGlobalVector(ParamsId, new Vector4(1f / Mathf.Max(cloudSize, 0.1f), coverage, softness, strength));
        Shader.SetGlobalVector(ScrollId, new Vector4(_scroll.x, _scroll.y, height, 0f));
    }

    public float SampleShadow(Vector3 groundPos, Vector3 lightDir)
    {
        if (!isActiveAndEnabled) return 0f;

        float t = (height - groundPos.y) / Mathf.Max(0.05f, -lightDir.y);
        Vector3 p = groundPos - lightDir * t;

        float s = 1f / Mathf.Max(cloudSize, 0.1f);
        float u = 1000f + p.x * s - _scroll.x;
        float v = 1000f + p.z * s - _scroll.y;

        float n = Mathf.PerlinNoise(u, v) * 0.65f
                + Mathf.PerlinNoise(u * 2.1f + 31.7f, v * 2.1f + 17.3f) * 0.35f;

        float k = Mathf.Clamp01((n - (coverage - softness)) / Mathf.Max(0.0001f, softness * 2f));
        return k * k * (3f - 2f * k);
    }

    void BuildCasters()
    {
        if (shadowMaterial == null) { Debug.LogWarning("CloudShadows: assign Shadow Material to use real shadow casters."); return; }

        var rng = seed == 0 ? new System.Random() : new System.Random(seed);
        _casters = new Transform[cloudCount];
        _casterSpeed = new float[cloudCount];
        Vector3 c = Center();

        for (int i = 0; i < cloudCount; i++)
        {
            var go = new GameObject("Cloud Caster " + i) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(transform, false);

            float w = Mathf.Lerp(casterSize.x, casterSize.y, (float)rng.NextDouble());
            float aspect = Mathf.Lerp(0.55f, 1f, (float)rng.NextDouble());
            float yaw = (float)rng.NextDouble() * 360f;
            go.transform.position = new Vector3(
                c.x + ((float)rng.NextDouble() - 0.5f) * areaSize, height,
                c.z + ((float)rng.NextDouble() - 0.5f) * areaSize);
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = new Vector3(w, w * aspect, 1f);

            var tex = MakeCloudTexture(96, rng.Next(0, 100000), 0.5f);
            var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sharedMaterial = shadowMaterial;
            sr.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            sr.receiveShadows = false;

            _casters[i] = go.transform;
            _casterSpeed[i] = windSpeed * (1f + ((float)rng.NextDouble() * 2f - 1f) * 0.35f);
        }
    }

    void MoveCasters()
    {
        if (_casters == null) return;
        float rad = windAngle * Mathf.Deg2Rad;
        Vector3 wind = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
        Vector3 c = Center();
        float half = areaSize * 0.5f;

        for (int i = 0; i < _casters.Length; i++)
        {
            if (_casters[i] == null) continue;
            Vector3 p = _casters[i].position + wind * (_casterSpeed[i] * Time.deltaTime);
            if (p.x - c.x > half) p.x -= areaSize; else if (p.x - c.x < -half) p.x += areaSize;
            if (p.z - c.z > half) p.z -= areaSize; else if (p.z - c.z < -half) p.z += areaSize;
            p.y = height;
            _casters[i].position = p;
        }
    }

    void DestroyCasters()
    {
        if (_casters == null) return;
        foreach (var t in _casters) if (t != null) { if (Application.isPlaying) Destroy(t.gameObject); else DestroyImmediate(t.gameObject); }
        _casters = null;
    }

    Vector3 Center()
    {
        if (follow != null) return follow.position;
        if (Camera.main != null) return Camera.main.transform.position;
        return Vector3.zero;
    }

    static Texture2D MakeCloudTexture(int size, int seed, float threshold)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color32[size * size];
        float ox = seed * 0.713f, oy = seed * 1.291f;

        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float nx = (x / (float)(size - 1)) * 2f - 1f;
                float ny = (y / (float)(size - 1)) * 2f - 1f;
                float r = Mathf.Sqrt(nx * nx + ny * ny);
                float n = 0f, amp = 0.55f, freq = 2.2f;
                for (int o = 0; o < 3; o++)
                {
                    n += Mathf.PerlinNoise(ox + (nx + 1f) * freq, oy + (ny + 1f) * freq) * amp;
                    amp *= 0.5f; freq *= 2.1f;
                }
                float v = n * Mathf.Clamp01(1f - r * r) * 1.9f;
                float a = Mathf.Clamp01((v - threshold) * 6f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }

        tex.SetPixels32(px);
        tex.Apply(false, true);
        return tex;
    }
}