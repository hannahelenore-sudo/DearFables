using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// Put this on an empty GameObject in your scene (e.g. "SunLitManager") and drag
/// the SpriteSunLit material into the Material slot.
/// Periodically makes sure that:
///  - every SpriteRenderer uses the sun-lit material (including things spawned after Play starts)
///  - every TilemapRenderer uses a ground version of it (no rim outline on tile borders)
///  - sprites/tilemaps/meshes receive shadows; sprites cast them
/// Anything with a custom material (e.g. your glowing card) keeps its material.
///
/// Optimised: each renderer is processed ONCE (tracked by instance id), shader checks are
/// cached instead of reading shader.name (which allocates a string) for every renderer on
/// every scan, and the flat ContactShadow blobs no longer cast shadows.
/// </summary>
public class SunLitSpriteManager : MonoBehaviour
{
    public Material sunLitMaterial;
    [Tooltip("Optional. Leave empty to auto-create one from the sun-lit material with the rim light off.")]
    public Material tilemapMaterial;
    [Tooltip("Safety-net scan for newly spawned renderers. Already-processed ones are skipped, so this is cheap.")]
    [Min(0.1f)] public float scanInterval = 2f;
    public bool spritesCastShadows = true;
    [Tooltip("Leave OFF for a ground tilemap (casting onto itself causes stripes). Turn on for tilemaps that are walls/props.")]
    public bool tilemapsCastShadows = false;
    public bool meshesCastAndReceiveShadows = true;

    const string SunLitShaderName = "Sprites/Sun Lit (2.5D)";
    static SunLitSpriteManager _instance;
    float _timer;
    bool _dirty;

    readonly HashSet<int> _done = new HashSet<int>();
    readonly Dictionary<Shader, bool> _isReplaceable = new Dictionary<Shader, bool>();
    readonly Dictionary<Shader, bool> _isSunLit = new Dictionary<Shader, bool>();

    /// <summary>Optional: call this right after spawning lots of objects to process them immediately.</summary>
    public static void RequestScan() { if (_instance != null) _instance._dirty = true; }

    void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        if (transform.parent == null) DontDestroyOnLoad(gameObject);

        if (tilemapMaterial == null && sunLitMaterial != null)
        {
            tilemapMaterial = new Material(sunLitMaterial) { name = "SpriteSunLitGround (runtime)" };
            tilemapMaterial.SetFloat("_RimStrength", 0f);
        }
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (_instance == this) _instance = null;
    }

    void OnSceneLoaded(Scene s, LoadSceneMode m)
    {
        _done.Clear();
        Scan();
    }

    void Start() => Scan();

    void Update()
    {
        _timer += Time.unscaledDeltaTime;
        if (_dirty || _timer >= scanInterval) { _timer = 0f; _dirty = false; Scan(); }
    }

    void Scan()
    {
        if (sunLitMaterial == null) return;

        foreach (var sr in FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!_done.Add(sr.GetInstanceID())) continue;
            // the flat ground blobs from ContactShadow must not cast shadows themselves
            bool cast = spritesCastShadows && sr.gameObject.name != ContactShadow.BlobName;
            Fix(sr, sunLitMaterial, cast);
        }

        foreach (var tr in FindObjectsByType<TilemapRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!_done.Add(tr.GetInstanceID())) continue;
            Fix(tr, tilemapMaterial != null ? tilemapMaterial : sunLitMaterial, tilemapsCastShadows);
        }

        if (meshesCastAndReceiveShadows)
        {
            foreach (var mr in FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!_done.Add(mr.GetInstanceID())) continue;
                if (mr.shadowCastingMode != ShadowCastingMode.On) mr.shadowCastingMode = ShadowCastingMode.On;
                if (!mr.receiveShadows) mr.receiveShadows = true;
            }
        }
    }

    void Fix(Renderer r, Material target, bool cast)
    {
        // leave shadow-only renderers (e.g. CloudShadows clouds) alone
        if (r.shadowCastingMode == ShadowCastingMode.ShadowsOnly) return;

        var m = r.sharedMaterial;
        if (Replaceable(m)) { r.sharedMaterial = target; m = target; }

        // only touch shadow flags on renderers that use our shader
        if (m != null && IsSunLit(m.shader))
        {
            var mode = cast ? ShadowCastingMode.On : ShadowCastingMode.Off;
            if (r.shadowCastingMode != mode) r.shadowCastingMode = mode;
            if (!r.receiveShadows) r.receiveShadows = true;
        }
    }

    bool IsSunLit(Shader s)
    {
        if (s == null) return false;
        if (!_isSunLit.TryGetValue(s, out bool v)) { v = s.name == SunLitShaderName; _isSunLit[s] = v; }
        return v;
    }

    // missing material, broken (magenta) shader, or one of the stock sprite shaders
    bool Replaceable(Material m)
    {
        if (m == null || m.shader == null) return true;
        var s = m.shader;
        if (!_isReplaceable.TryGetValue(s, out bool v))
        {
            string n = s.name;
            v = n == "Hidden/InternalErrorShader"
             || n == "Universal Render Pipeline/2D/Sprite-Lit-Default"
             || n == "Universal Render Pipeline/2D/Sprite-Unlit-Default"
             || n == "Sprites/Default";
            _isReplaceable[s] = v;
        }
        return v;
    }
}