using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class MemoryUIGlassDebugTools
{
    private const string FrostedMaterialAssetPath = "Assets/_project/Art/Memory Materials/MemoryUIFrostedGlass.mat";
    private const string ExpectedShaderName = "MemoryGarden/UI/Frosted Glass";

    [MenuItem("Memory Garden/UI/Enable Frosted Glass Debug View")]
    public static void EnableDebugView()
    {
        SetDebugView(1f);
    }

    [MenuItem("Memory Garden/UI/Disable Frosted Glass Debug View")]
    public static void DisableDebugView()
    {
        SetDebugView(0f);
    }

    [MenuItem("Memory Garden/UI/Show Frosted Glass Blurred Scene Debug")]
    public static void ShowBlurredSceneDebugView()
    {
        SetDebugView(2f);
    }

    [MenuItem("Memory Garden/UI/Show Renderer Feature Blur Debug")]
    public static void ShowRendererFeatureBlurDebugView()
    {
        SetDebugView(3f);
    }

    [MenuItem("Memory Garden/UI/Diagnose HiFi Glass")]
    public static void Diagnose()
    {
        MemoryUIBeveledPanel3D[] panels = Object.FindObjectsByType<MemoryUIBeveledPanel3D>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        StringBuilder builder = new StringBuilder();
        builder.AppendLine($"[MemoryUIGlassDebugTools] Found {panels.Length} beveled panels.");

        foreach (MemoryUIBeveledPanel3D panel in panels)
        {
            if (panel == null)
            {
                continue;
            }

            MeshRenderer renderer = panel.GetComponent<MeshRenderer>();
            Material material = renderer != null ? renderer.sharedMaterial : null;
            Image parentImage = panel.transform.parent != null ? panel.transform.parent.GetComponent<Image>() : null;
            string shaderName = material != null && material.shader != null ? material.shader.name : "<none>";

            builder.AppendLine(
                $"- {GetHierarchyPath(panel.transform)} | renderer:{(renderer != null)} enabled:{(renderer != null && renderer.enabled)} " +
                $"material:{(material != null ? material.name : "<none>")} shader:{shaderName} " +
                $"expected:{(shaderName == ExpectedShaderName)} overlayAlpha:{(parentImage != null ? parentImage.color.a.ToString("0.###") : "<none>")} " +
                $"lossyScale:{panel.transform.lossyScale}");
        }

        Debug.Log(builder.ToString());
    }

    [MenuItem("Memory Garden/UI/Apply Recommended HiFi Glass Tuning")]
    public static void ApplyRecommendedTuning()
    {
        MemoryUIBeveledPanel3D[] panels = Object.FindObjectsByType<MemoryUIBeveledPanel3D>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (MemoryUIBeveledPanel3D panel in panels)
        {
            if (panel == null)
            {
                continue;
            }

            Undo.RecordObject(panel, "Apply Recommended HiFi Glass Tuning");
            panel.ApplyRecommendedGlassTuning();
            EditorUtility.SetDirty(panel);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[MemoryUIGlassDebugTools] Applied recommended glass tuning to {panels.Length} beveled panels.");
    }

    private static void SetDebugView(float value)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(FrostedMaterialAssetPath);
        if (material == null)
        {
            Debug.LogError($"[MemoryUIGlassDebugTools] Could not load material at '{FrostedMaterialAssetPath}'.");
            return;
        }

        if (!material.HasProperty("_DebugView"))
        {
            Debug.LogError("[MemoryUIGlassDebugTools] Frosted glass material does not expose _DebugView.");
            return;
        }

        material.SetFloat("_DebugView", value);
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = material;
        string mode = value switch
        {
            >= 2.5f => "renderer feature blur",
            >= 1.5f => "blurred scene",
            >= 0.5f => "surface pattern",
            _ => "disabled"
        };

        Debug.Log($"[MemoryUIGlassDebugTools] Set frosted glass debug view to {value:0.##} ({mode}).");
    }

    private static string GetHierarchyPath(Transform transform)
    {
        if (transform == null)
        {
            return "<null>";
        }

        StringBuilder builder = new StringBuilder(transform.name);
        Transform current = transform.parent;
        while (current != null)
        {
            builder.Insert(0, current.name + "/");
            current = current.parent;
        }

        return builder.ToString();
    }
}
