using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class ApplyPostProcessing
{
    const string ProfileGuid = "54438f96e002fe746a8d8cee04b08ff8";

    [MenuItem("Tools/Apply Subtle Post-Processing")]
    static void Apply()
    {
        string path = AssetDatabase.GUIDToAssetPath(ProfileGuid);
        var profile = string.IsNullOrEmpty(path)
            ? null
            : AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);

        if (profile == null)
        {
            Debug.LogError("Volume profile not found. Select your Global Volume's profile and update ProfileGuid.");
            return;
        }

        var bloom = GetOrAdd<Bloom>(profile);
        bloom.threshold.Override(0.9f);
        bloom.intensity.Override(0.35f);
        bloom.scatter.Override(0.6f);
        bloom.tint.Override(Color.white);

        var color = GetOrAdd<ColorAdjustments>(profile);
        color.contrast.Override(10f);
        color.saturation.Override(12f);

        var vignette = GetOrAdd<Vignette>(profile);
        vignette.intensity.Override(0.25f);
        vignette.smoothness.Override(0.4f);
        vignette.rounded.Override(false);

        var dof = GetOrAdd<DepthOfField>(profile);
        dof.mode.Override(DepthOfFieldMode.Gaussian);
        dof.gaussianStart.Override(15f);
        dof.gaussianEnd.Override(35f);
        dof.gaussianMaxRadius.Override(1.0f);
        dof.highQualitySampling.Override(false);

        var split = GetOrAdd<SplitToning>(profile);
        split.shadows.Override(new Color(0.35f, 0.30f, 0.60f));
        split.highlights.Override(new Color(1.00f, 0.85f, 0.65f));
        split.balance.Override(0f);

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        Debug.Log("Post-processing applied to: " + path);
    }

    static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
    {
        if (!profile.TryGet(out T comp))
            comp = profile.Add<T>(false);
        comp.active = true;
        return comp;
    }
}