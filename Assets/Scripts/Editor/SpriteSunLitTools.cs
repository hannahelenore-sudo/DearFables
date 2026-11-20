using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// Put this on an empty GameObject in your scene (e.g. "SunLitManager") and drag
/// the SpriteSunLit material into the Material slot.
/// Every half second it makes sure that:
///  - every SpriteRenderer uses the sun-lit material (including things spawned after Play starts)
///  - every TilemapRenderer uses a ground version of it (no rim outline on tile borders)
///  - sprites/tilemaps/meshes receive shadows; sprites cast them
/// Anything with a custom material (e.g. your glowing card) keeps its material.
/// </summary>
public class SunLitSpriteManager : MonoBehaviour
{
    public Material sunLitMaterial;
    [Tooltip("Optional. Leave empty to auto-create one from the sun-lit material with the rim light off.")]
    public Material tilemapMaterial;
    [Min(0.1f)] public float scanInterval = 0.5f;
    public bool spritesCastShadows = true;
    [Tooltip("Leave OFF for a ground tilemap (casting onto itself causes stripes). Turn on for tilemaps that are walls/props.")]
    public bool tilemapsCastShadows = false;
    public bool meshesCastAndReceiveShadows = true;

    const string SunLitShader = "Sprites/Sun Lit (2.5D)";
    static SunLitSpriteManager _instance;
    float _timer;

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
        SceneManager.sceneLoaded += (_, __) => Scan();
    }

    void Start() => Scan();

    void Update()
    {
        _timer += Time.unscaledDeltaTime;
        if (_timer >= scanInterval) { _timer = 0f; Scan(); }
    }

    void Scan()
    {
        if (sunLitMaterial == null) return;

        foreach (var sr in FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include))
            Fix(sr, sunLitMaterial, spritesCastShadows);

        foreach (var tr in FindObjectsByType<TilemapRenderer>(FindObjectsInactive.Include))
            Fix(tr, tilemapMaterial != null ? tilemapMaterial : sunLitMaterial, tilemapsCastShadows);

        if (meshesCastAndReceiveShadows)
        {
            foreach (var mr in FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include))
            {
                if (mr.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.On)
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                if (!mr.receiveShadows) mr.receiveShadows = true;
            }
        }
    }

    void Fix(Renderer r, Material target, bool cast)
    {
        // leave shadow-only renderers (e.g. CloudShadows clouds) alone
        if (r.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly) return;

        var m = r.sharedMaterial;
        if (Replaceable(m)) { r.sharedMaterial = target; m = target; }

        // only touch shadow flags on renderers that use our shader
        if (m != null && m.shader != null && m.shader.name == SunLitShader)
        {
            var mode = cast ? UnityEngine.Rendering.ShadowCastingMode.On
                            : UnityEngine.Rendering.ShadowCastingMode.Off;
            if (r.shadowCastingMode != mode) r.shadowCastingMode = mode;
            if (!r.receiveShadows) r.receiveShadows = true;
        }
    }

    // missing material, broken (magenta) shader, or one of the stock sprite shaders
    static bool Replaceable(Material m)
    {
        if (m == null || m.shader == null) return true;
        string n = m.shader.name;
        return n == "Hidden/InternalErrorShader"
            || n == "Universal Render Pipeline/2D/Sprite-Lit-Default"
            || n == "Universal Render Pipeline/2D/Sprite-Unlit-Default"
            || n == "Sprites/Default";
    }
}