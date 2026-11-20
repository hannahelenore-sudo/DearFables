// Put this file in Assets/Editor/ApplyPostProcessing.cs
// Then run: Tools > Apply Subtle Post-Processing
// It finds the profile used by your Global Volume and adds/overrides the effects.
// Safe to run repeatedly. Tweak the values below, run again, and compare.

using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class ApplyPostProcessing
{
    // The profile referenced by "Global Volume" in GameScene.unity
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

        // ---------- Bloom: soft glow on bright things ----------
        var bloom = GetOrAdd<Bloom>(profile);
        bloom.threshold.Override(0.9f);   // lower if the glow doesn't show on cards/stars
        bloom.intensity.Override(0.35f);
        bloom.scatter.Override(0.6f);
        bloom.tint.Override(Color.white);

        // ---------- Color Adjustments: slight saturation + contrast ----------
        var color = GetOrAdd<ColorAdjustments>(profile);
        color.contrast.Override(10f);
        color.saturation.Override(12f);

        // ---------- Vignette: subtle edge darkening ----------
        var vignette = GetOrAdd<Vignette>(profile);
        vignette.intensity.Override(0.25f);
        vignette.smoothness.Override(0.4f);
        vignette.rounded.Override(false);

        // ---------- Depth of Field (Gaussian): tilt-shift feel ----------
        // Camera sits ~14.4 units from the ground focus point (0,12,-8 looking down 55 deg),
        // so blur only starts a bit beyond that, i.e. the far (top) edge of the screen.
        var dof = GetOrAdd<DepthOfField>(profile);
        dof.mode.Override(DepthOfFieldMode.Gaussian);
        dof.gaussianStart.Override(15f);
        dof.gaussianEnd.Override(35f);
        dof.gaussianMaxRadius.Override(1.0f);
        dof.highQualitySampling.Override(false);

        // ---------- Color grading: purple/blue shadows, warm highlights ----------
        var split = GetOrAdd<SplitToning>(profile);
        split.shadows.Override(new Color(0.35f, 0.30f, 0.60f));    // purple-blue
        split.highlights.Override(new Color(1.00f, 0.85f, 0.65f)); // warm
        split.balance.Override(0f);

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        Debug.Log("Post-processing applied to: " + path);
    }

    static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
    {
        if (!profile.TryGet(out T comp))
            comp = profile.Add<T>(false); // false = parameters start un-overridden
        comp.active = true;
        return comp;
    }
}
