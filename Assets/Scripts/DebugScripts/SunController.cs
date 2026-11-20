using UnityEngine;

/// <summary>
/// Put this on your "Directional Light". It is the sun.
/// Manual mode: Azimuth + Elevation sliders.
/// Auto Cycle: a full day/night loop (sunrise > noon > sunset > night).
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(Light))]
public class SunController : MonoBehaviour
{
    [Header("Manual Direction (when Auto Cycle is off)")]
    [Range(0f, 360f)] public float azimuth = -30f;
    [Range(0f, 90f)] public float elevation = 45f;
    public Color sunColor = new Color(1f, 0.99f, 0.96f);
    [Min(0f)] public float intensity = 2f;

    [Header("Shadows")]
    [Range(0f, 1f)] public float shadowStrength = 1f;
    [Tooltip("Hard shadows are much cheaper than Soft. This was hard-coded to Soft before.")]
    public LightShadows shadowType = LightShadows.Hard;

    [Header("Day / Night Cycle")]
    public bool autoCycle = true;
    [Min(5f)] public float dayLengthSeconds = 120f;
    [Range(0f, 1f)] public float timeOfDay = 0.2f;   // 0 sunrise, .25 noon, .5 sunset, .75 midnight
    [Range(10f, 85f)] public float maxElevation = 65f;
    public Gradient colorOverDay;
    public AnimationCurve intensityOverDay;

    Light _light;

    void OnEnable()
    {
        _light = GetComponent<Light>();
        _light.type = LightType.Directional;
        _light.shadows = shadowType;
        EnsureCurves();
        Apply();
    }

    void OnValidate()
    {
        if (_light == null) _light = GetComponent<Light>();
        EnsureCurves();
        Apply();
    }

    void Update()
    {
        if (!autoCycle) return;
        if (Application.isPlaying)
            timeOfDay = Mathf.Repeat(timeOfDay + Time.deltaTime / dayLengthSeconds, 1f);
        Apply();
    }

    void Apply()
    {
        if (_light == null) return;

        float az = azimuth, el = elevation, inten = intensity;
        Color col = sunColor;

        if (autoCycle)
        {
            float t = timeOfDay;
            az = Mathf.Repeat(90f + t * 360f, 360f);               // east > south > west > under the ground
            el = Mathf.Max(Mathf.Sin(t * Mathf.PI * 2f) * maxElevation, 8f);
            col = colorOverDay.Evaluate(t);
            inten = intensityOverDay.Evaluate(t);
        }

        transform.rotation = Quaternion.Euler(el, az, 0f);
        _light.color = col;
        _light.intensity = inten;
        _light.shadowStrength = shadowStrength;
    }

    void EnsureCurves()
    {
        if (colorOverDay == null || colorOverDay.colorKeys.Length == 0 ||
            intensityOverDay == null || intensityOverDay.length == 0)
            ResetDayCycle();
    }

    [ContextMenu("Reset Day Cycle")]
    public void ResetDayCycle()
    {
        var g = new Gradient();
        g.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.72f, 0.55f), 0f),     // sunrise
                new GradientColorKey(new Color(1f, 0.99f, 0.96f), 0.25f),  // noon
                new GradientColorKey(new Color(1f, 0.6f, 0.45f), 0.5f),    // sunset
                new GradientColorKey(new Color(0.4f, 0.5f, 0.9f), 0.75f),  // night
                new GradientColorKey(new Color(1f, 0.72f, 0.55f), 1f)      // back to sunrise
            },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        colorOverDay = g;

        intensityOverDay = new AnimationCurve(
            new Keyframe(0f, 0.8f), new Keyframe(0.25f, 2f), new Keyframe(0.5f, 0.8f),
            new Keyframe(0.75f, 0.2f), new Keyframe(1f, 0.8f));
    }
}