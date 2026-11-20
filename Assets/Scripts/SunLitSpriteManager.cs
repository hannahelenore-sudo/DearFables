#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Put in Assets/Editor/SpriteSunLitTools.cs
/// Tools > Sprite Sun Lighting > ...
/// Swaps the default sprite material for the sun-lit one on every SpriteRenderer
/// and turns on shadow casting/receiving. Sprites that already use a custom
/// material (e.g. your glowing thrown card) are left alone.
/// </summary>
public static class SpriteSunLitTools
{
    const string ShaderName = "Sprites/Sun Lit (2.5D)";
    const string MaterialPath = "Assets/Materials/SpriteSunLit.mat";
    const string GroundMaterialPath = "Assets/Materials/SpriteSunLitGround.mat";

    static Material GetOrCreateMaterial()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (mat != null) return mat;

        var shader = Shader.Find(ShaderName);
        if (shader == null)
        {
            Debug.LogError($"Shader '{ShaderName}' not found. Is SpriteSunLit.shader in your project and compiling?");
            return null;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(MaterialPath));
        mat = new Material(shader) { name = "SpriteSunLit" };
        AssetDatabase.CreateAsset(mat, MaterialPath);
        AssetDatabase.SaveAssets();
        return mat;
    }

    // Same shader, but with the rim light off so tile borders don't get outlined.
    static Material GetOrCreateGroundMaterial(Material baseMat)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(GroundMaterialPath);
        if (mat != null) return mat;
        mat = new Material(baseMat) { name = "SpriteSunLitGround" };
        mat.SetFloat("_RimStrength", 0f);
        AssetDatabase.CreateAsset(mat, GroundMaterialPath);
        AssetDatabase.SaveAssets();
        return mat;
    }

    // Only replace the stock sprite materials, never a custom one.
    static bool IsReplaceable(Material m)
    {
        if (m == null || m.shader == null) return true;
        string n = m.shader.name;
        return n == "Hidden/InternalErrorShader"
            || n == "Universal Render Pipeline/2D/Sprite-Lit-Default"
            || n == "Universal Render Pipeline/2D/Sprite-Unlit-Default"
            || n == "Sprites/Default";
    }

    static int Apply(Renderer sr, Material mat, bool castShadows)
    {
        if (!IsReplaceable(sr.sharedMaterial)) return 0;
        Undo.RecordObject(sr, "Apply Sun Lit Sprite");
        sr.sharedMaterial = mat;
        sr.shadowCastingMode = castShadows ? UnityEngine.Rendering.ShadowCastingMode.On
                                           : UnityEngine.Rendering.ShadowCastingMode.Off;
        sr.receiveShadows = true;
        EditorUtility.SetDirty(sr);
        return 1;
    }

    [MenuItem("Tools/Sprite Sun Lighting/Apply To Open Scene")]
    public static void ApplyToScene()
    {
        var mat = GetOrCreateMaterial();
        if (mat == null) return;

        var groundMat = GetOrCreateGroundMaterial(mat);
        int changed = 0, skipped = 0;
        foreach (var sr in Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include))
        {
            if (Apply(sr, mat, true) == 1) changed++; else skipped++;
        }
        foreach (var tr in Object.FindObjectsByType<TilemapRenderer>(FindObjectsInactive.Include))
        {
            if (Apply(tr, groundMat, false) == 1) changed++; else skipped++;
        }
        foreach (var mr in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include))
        {
            Undo.RecordObject(mr, "Enable Shadows");
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            mr.receiveShadows = true;
            EditorUtility.SetDirty(mr);
        }
        EditorSceneManager.MarkAllScenesDirty();
        Debug.Log($"Sprite Sun Lighting: {changed} sprites updated, {skipped} skipped (custom material).");
    }

    [MenuItem("Tools/Sprite Sun Lighting/Apply To All Prefabs In Assets")]
    public static void ApplyToPrefabs()
    {
        var mat = GetOrCreateMaterial();
        if (mat == null) return;

        var groundMat = GetOrCreateGroundMaterial(mat);
        int changed = 0, skipped = 0, prefabs = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var root = PrefabUtility.LoadPrefabContents(path);
            bool dirty = false;
            foreach (var sr in root.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (Apply(sr, mat, true) == 1) { changed++; dirty = true; } else skipped++;
            }
            foreach (var tr in root.GetComponentsInChildren<TilemapRenderer>(true))
            {
                if (Apply(tr, groundMat, false) == 1) { changed++; dirty = true; } else skipped++;
            }
            foreach (var mr in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                mr.receiveShadows = true;
                dirty = true;
            }
            if (dirty) { PrefabUtility.SaveAsPrefabAsset(root, path); prefabs++; }
            PrefabUtility.UnloadPrefabContents(root);
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"Sprite Sun Lighting: {changed} sprites updated in {prefabs} prefabs, {skipped} skipped (custom material).");
    }
}
#endif