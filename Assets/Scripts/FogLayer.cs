using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Tilemaps;

/// <summary>
/// 3D mist. Fills a VOLUME (not a flat sheet) with big, soft, drifting puffs at many heights, so it wraps
/// around trees and objects, passes in front of and behind them, and gives real depth.
/// Needs no art and no collider.
///
/// Setup: make an empty object, add this script, put the object at your GROUND height. Done.
///
/// Keeping the tilemap ground clear: the mist is drawn on top of everything (high Sorting Order) but is hidden
/// wherever the ground tilemap is drawn, using a stencil mask. This needs the two shader files
/// MistParticle.shader and MistGroundMask.shader somewhere in your Assets folder. The script swaps the tilemap's
/// material for the mask material while playing and puts the original back afterwards.
///
/// Optional distance fog (Use Distance Fog) also hazes 3D objects by depth. Sprites and tilemaps ignore it.
///
/// Almost everything can be changed while playing. Volume settings (area, heights, count, puff size, lifetime)
/// refill the mist automatically half a second after you stop changing them.
/// </summary>
public class FogLayer : MonoBehaviour
{
    public enum Facing { FaceCamera = 0, LieFlat = 1 }

    [Header("Look (live)")]
    [Tooltip("Colour of the mist. Alpha of this colour also scales the strength.")]
    public Color fogColor = new Color(0.75f, 0.8f, 0.9f, 1f);
    [Tooltip("How strong the mist is. Updates instantly.")]
    [Range(0f, 2f)] public float density = 0.5f;
    [Tooltip("Makes ALL puffs bigger / smaller, instantly, even the ones already floating.")]
    [Range(0.1f, 4f)] public float sizeMultiplier = 1f;
    [Tooltip("Face Camera: round puffs like smoke (best 3D look). Lie Flat: puffs lie horizontally like ground mist.")]
    public Facing facing = Facing.FaceCamera;

    [Header("Volume (refills when changed)")]
    [Tooltip("On: the volume follows what the camera looks at, so the whole screen is always misted. Off: fixed volume around this object.")]
    public bool followCamera = true;
    [Tooltip("Width (x) and depth (z) of the mist volume in world units. Make it bigger than the screen.")]
    public Vector2 areaSize = new Vector2(100f, 100f);
    [Tooltip("Bottom of the mist, relative to this object's height. Negative = under the ground, fills the void under the map.")]
    public float minHeight = 0f;
    [Tooltip("Top of the mist, relative to this object's height.")]
    public float maxHeight = 5f;
    [Tooltip("How many puffs exist at once. More = thicker and smoother, but heavier.")]
    [Range(10, 1500)] public int puffCount = 250;
    public float minPuffSize = 8f;
    public float maxPuffSize = 18f;
    [Tooltip("Seconds a puff lives. Longer = fewer new puffs needed.")]
    public float lifetime = 40f;

    [Header("Movement (live)")]
    [Tooltip("3D wind direction. x = left/right, y = up/down, z = forward/back.")]
    public Vector3 windDirection = new Vector3(1f, 0f, 0.3f);
    public float driftSpeed = 0.5f;
    [Tooltip("Swirling, billowing motion. 0 = puffs drift in a straight line.")]
    [Range(0f, 3f)] public float turbulence = 0.4f;

    [Header("Background and depth fog (live)")]
    [Tooltip("Fills the empty black void behind everything with Backdrop Color (changes the camera background).")]
    public bool tintCameraBackground = false;
    public Color backdropColor = new Color(0.55f, 0.6f, 0.7f, 1f);
    [Tooltip("Also haze 3D objects by distance using Unity's built-in fog. Sprites and tilemaps are not affected.")]
    public bool useDistanceFog = false;
    [Range(0f, 0.1f)] public float distanceFogDensity = 0.01f;

    [Header("Sorting")]
    [Tooltip("Leave empty for the default layer.")]
    public string sortingLayerName = "";
    [Tooltip("Must be HIGHER than the Order in Layer of your trees, grass, tilemap and player, so the mist is drawn over them.")]
    public int sortingOrder = 500;

    [Header("Keep the ground clear (set before pressing Play)")]
    [Tooltip("Hides the mist wherever the ground tilemap is drawn.")]
    public bool maskGround = true;
    [Tooltip("Your ground Tilemap Renderer(s). Empty = every Tilemap Renderer in the scene.")]
    public TilemapRenderer[] groundRenderers;

    [Header("Camera")]
    [Tooltip("Empty = main camera (needs the MainCamera tag).")]
    public Camera targetCamera;

    private ParticleSystem ps;
    private ParticleSystemRenderer psr;
    private Material mat;
    private Texture2D atlas;
    private Material groundMat;
    private TilemapRenderer[] maskedRenderers;
    private Material[] savedGroundMats;
    private readonly AnimationCurve sizeCurve = new AnimationCurve(
        new Keyframe(0f, 0.85f), new Keyframe(1f, 1.15f));

    private float lastSignature;
    private float refillTimer = -1f;
    private bool warnedNoCamera;

    // saved so we can put things back
    private bool bgSaved;
    private CameraClearFlags savedClear;
    private Color savedBg;
    private Camera bgCamera;
    private bool fogSaved;
    private bool savedFog;
    private FogMode savedFogMode;
    private Color savedFogColor;
    private float savedFogDensity;

    // ------------------------------------------------------------------

    void Start()
    {
        Build();
    }

    void OnDestroy()
    {
        RestoreGround();
        RestoreCameraBackground();
        RestoreFog();
        if (ps != null) Destroy(ps.gameObject);
        if (mat != null) Destroy(mat);
        if (atlas != null) Destroy(atlas);
    }

    void Build()
    {
        atlas = MakeAtlas(128);

        var go = new GameObject("MistParticles");
        go.transform.SetParent(transform, false);
        ps = go.AddComponent<ParticleSystem>();
        psr = go.GetComponent<ParticleSystemRenderer>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); // must be stopped to change duration

        Shader mistShader = Shader.Find("Mist/Particle");
        mat = new Material(mistShader != null ? mistShader : Shader.Find("Sprites/Default"));
        mat.mainTexture = atlas;
        if (maskGround) ApplyGroundMask(mistShader != null);
        psr.sharedMaterial = mat;
        psr.sortMode = ParticleSystemSortMode.Distance;
        psr.shadowCastingMode = ShadowCastingMode.Off;
        psr.receiveShadows = false;

        var main = ps.main;
        main.playOnAwake = false;
        main.loop = true;
        main.duration = 10f;
        main.prewarm = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startSpeed = 0f;
        main.gravityModifier = 0f;
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 1f, 1f, 0.5f), new Color(1f, 1f, 1f, 1f));

        var emission = ps.emission;
        emission.enabled = true;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.position = Vector3.zero;
        shape.rotation = Vector3.zero;

        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.separateAxes = false;

        var rol = ps.rotationOverLifetime;
        rol.enabled = true;
        rol.z = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);

        // fade in and out so puffs never pop
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(g);

        // the atlas holds 4 different cloud pictures (2x2); each puff picks one at random
        var ts = ps.textureSheetAnimation;
        ts.enabled = true;
        ts.mode = ParticleSystemAnimationMode.Grid;
        ts.numTilesX = 2;
        ts.numTilesY = 2;
        ts.animation = ParticleSystemAnimationType.WholeSheet;
        ts.frameOverTime = new ParticleSystem.MinMaxCurve(0f);
        ts.startFrame = new ParticleSystem.MinMaxCurve(0f, 0.999f);

        ApplySettings(ResolveCamera());
        Refill();
    }

    [ContextMenu("Refill Mist")]
    public void Refill()
    {
        if (ps == null) return;
        ApplySettings(ResolveCamera());
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ps.Simulate(ps.main.startLifetime.constantMax, true, true);
        ps.Play();
        lastSignature = Signature();
        refillTimer = -1f;
    }

    // ------------------------------------------------------------------

    void Update()
    {
        if (ps == null) return;

        Camera cam = ResolveCamera();
        ApplySettings(cam);

        // auto refill shortly after volume settings stop changing
        float sig = Signature();
        if (!Mathf.Approximately(sig, lastSignature))
        {
            lastSignature = sig;
            refillTimer = 0.5f;
        }
        if (refillTimer >= 0f)
        {
            refillTimer -= Time.unscaledDeltaTime;
            if (refillTimer < 0f) Refill();
        }
    }

    Camera ResolveCamera()
    {
        Camera cam = targetCamera != null ? targetCamera : Camera.main;
        if (cam == null && !warnedNoCamera)
        {
            Debug.LogWarning("FogLayer: no camera found. Tag your camera MainCamera or drag it into Target Camera.", this);
            warnedNoCamera = true;
        }
        return cam;
    }

    float Signature()
    {
        return areaSize.x * 1.1f + areaSize.y * 2.3f + minHeight * 3.1f + maxHeight * 4.7f
             + puffCount * 5.3f + minPuffSize * 6.1f + maxPuffSize * 7.9f + lifetime * 8.3f
             + (followCamera ? 11f : 0f);
    }

    void ApplySettings(Camera cam)
    {
        // ---- look
        Color tint = fogColor;
        tint.a = Mathf.Clamp01(fogColor.a * density);
        mat.color = tint;
        psr.renderMode = facing == Facing.FaceCamera ? ParticleSystemRenderMode.Billboard : ParticleSystemRenderMode.HorizontalBillboard;
        psr.sortingOrder = sortingOrder;
        if (!string.IsNullOrEmpty(sortingLayerName)) psr.sortingLayerName = sortingLayerName;

        var sol = ps.sizeOverLifetime;
        sol.size = new ParticleSystem.MinMaxCurve(sizeMultiplier, sizeCurve);

        // ---- volume / counts
        float life = Mathf.Max(1f, lifetime);
        var main = ps.main;
        main.maxParticles = Mathf.Max(1, puffCount);
        main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.8f, life * 1.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(Mathf.Min(minPuffSize, maxPuffSize), Mathf.Max(minPuffSize, maxPuffSize));

        var emission = ps.emission;
        emission.rateOverTime = puffCount / life;

        float hMin = Mathf.Min(minHeight, maxHeight);
        float hMax = Mathf.Max(minHeight, maxHeight);
        var shape = ps.shape;
        shape.scale = new Vector3(Mathf.Max(0.1f, areaSize.x), Mathf.Max(0.1f, hMax - hMin), Mathf.Max(0.1f, areaSize.y));

        Vector3 center = transform.position;
        if (followCamera && cam != null)
        {
            // the point on the ground the camera is looking at
            var plane = new Plane(Vector3.up, transform.position);
            Ray r = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (plane.Raycast(r, out float t) && t < 500f) center = r.GetPoint(t);
            else center = new Vector3(cam.transform.position.x, transform.position.y, cam.transform.position.z);
        }
        ps.transform.position = new Vector3(center.x, transform.position.y + (hMin + hMax) * 0.5f, center.z);

        // ---- movement
        Vector3 w = windDirection.sqrMagnitude > 0.0001f ? windDirection.normalized * driftSpeed : Vector3.zero;
        var vel = ps.velocityOverLifetime;
        vel.x = new ParticleSystem.MinMaxCurve(w.x);
        vel.y = new ParticleSystem.MinMaxCurve(w.y);
        vel.z = new ParticleSystem.MinMaxCurve(w.z);

        var noise = ps.noise;
        noise.enabled = turbulence > 0.001f;
        if (noise.enabled)
        {
            noise.strength = new ParticleSystem.MinMaxCurve(turbulence);
            noise.frequency = 0.1f;
            noise.scrollSpeed = 0.1f;
            noise.damping = true;
            noise.quality = ParticleSystemNoiseQuality.Low;
        }

        // ---- camera background and depth fog
        if (tintCameraBackground && cam != null) ApplyCameraBackground(cam);
        else RestoreCameraBackground();

        if (useDistanceFog) ApplyFog();
        else RestoreFog();
    }

    // ------------------------------------------------------------------
    // Background / fog helpers
    // ------------------------------------------------------------------

    void ApplyGroundMask(bool particleShaderOk)
    {
        Shader maskShader = Shader.Find("Mist/GroundMask");
        if (!particleShaderOk || maskShader == null)
        {
            Debug.LogWarning("FogLayer: shaders 'Mist/Particle' and 'Mist/GroundMask' were not found. Add MistParticle.shader and MistGroundMask.shader anywhere under Assets. Until then the mist cannot avoid the ground.", this);
            return;
        }

        TilemapRenderer[] targets = groundRenderers;
        if (targets == null || targets.Length == 0) targets = FindObjectsOfType<TilemapRenderer>();
        if (targets.Length == 0)
        {
            Debug.LogWarning("FogLayer: no Tilemap Renderer found to keep clear. Drag your ground tilemap into Ground Renderers.", this);
            return;
        }

        groundMat = new Material(maskShader);
        maskedRenderers = targets;
        savedGroundMats = new Material[targets.Length];
        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] == null) continue;
            savedGroundMats[i] = targets[i].sharedMaterial;
            targets[i].sharedMaterial = groundMat;
        }
    }

    void RestoreGround()
    {
        if (maskedRenderers != null)
        {
            for (int i = 0; i < maskedRenderers.Length; i++)
                if (maskedRenderers[i] != null && savedGroundMats[i] != null)
                    maskedRenderers[i].sharedMaterial = savedGroundMats[i];
            maskedRenderers = null;
        }
        if (groundMat != null) Destroy(groundMat);
        groundMat = null;
    }

    void ApplyCameraBackground(Camera cam)
    {
        if (!bgSaved)
        {
            bgCamera = cam;
            savedClear = cam.clearFlags;
            savedBg = cam.backgroundColor;
            bgSaved = true;
        }
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = backdropColor;
    }

    void RestoreCameraBackground()
    {
        if (!bgSaved) return;
        if (bgCamera != null)
        {
            bgCamera.clearFlags = savedClear;
            bgCamera.backgroundColor = savedBg;
        }
        bgSaved = false;
    }

    void ApplyFog()
    {
        if (!fogSaved)
        {
            savedFog = RenderSettings.fog;
            savedFogMode = RenderSettings.fogMode;
            savedFogColor = RenderSettings.fogColor;
            savedFogDensity = RenderSettings.fogDensity;
            fogSaved = true;
        }
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        Color c = fogColor; c.a = 1f;
        RenderSettings.fogColor = c;
        RenderSettings.fogDensity = distanceFogDensity;
    }

    void RestoreFog()
    {
        if (!fogSaved) return;
        RenderSettings.fog = savedFog;
        RenderSettings.fogMode = savedFogMode;
        RenderSettings.fogColor = savedFogColor;
        RenderSettings.fogDensity = savedFogDensity;
        fogSaved = false;
    }

    // ------------------------------------------------------------------
    // Painting the soft puffs: a 2x2 atlas with 4 different clouds
    // ------------------------------------------------------------------

    static Texture2D MakeAtlas(int tile)
    {
        int size = tile * 2;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        var px = new Color32[size * size];
        for (int ty = 0; ty < 2; ty++)
        {
            for (int tx = 0; tx < 2; tx++)
            {
                float seed = 50f + (ty * 2 + tx) * 217f;
                for (int y = 0; y < tile; y++)
                {
                    for (int x = 0; x < tile; x++)
                    {
                        float u = x / (float)(tile - 1);
                        float v = y / (float)(tile - 1);

                        // round soft edge: solid in the middle, zero at the border (never a hard edge)
                        float dx = u - 0.5f, dy = v - 0.5f;
                        float r = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
                        float falloff = 1f - Mathf.SmoothStep(0.15f, 1f, r);

                        float n = 0f, amp = 0.55f, freq = 2.5f;
                        for (int o = 0; o < 3; o++)
                        {
                            n += Mathf.PerlinNoise(seed + u * freq, seed + v * freq) * amp;
                            amp *= 0.5f;
                            freq *= 2f;
                        }

                        float a = Mathf.Clamp01(falloff * (0.3f + n * 1.2f));
                        a = a * a * (3f - 2f * a);
                        px[(ty * tile + y) * size + (tx * tile + x)] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                    }
                }
            }
        }

        tex.SetPixels32(px);
        tex.Apply(false, true);
        return tex;
    }
}