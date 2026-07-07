using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public static class MemoryUIBlurRendererFeatureInstaller
{
    private const string RendererAssetPath = "Assets/Settings/URP-HighFidelity-Renderer.asset";

    [MenuItem("Memory Garden/UI/Install Memory UI Blur Feature")]
    public static void Install()
    {
        UniversalRendererData rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererAssetPath);
        if (rendererData == null)
        {
            Debug.LogError($"[MemoryUIBlurRendererFeatureInstaller] Could not load renderer asset at '{RendererAssetPath}'.");
            return;
        }

        foreach (ScriptableRendererFeature feature in rendererData.rendererFeatures)
        {
            if (feature is MemoryUIBackgroundBlurRendererFeature)
            {
                Selection.activeObject = rendererData;
                EditorGUIUtility.PingObject(feature);
                Debug.Log("[MemoryUIBlurRendererFeatureInstaller] Memory UI blur feature is already installed.");
                return;
            }
        }

        MemoryUIBackgroundBlurRendererFeature newFeature = ScriptableObject.CreateInstance<MemoryUIBackgroundBlurRendererFeature>();
        newFeature.name = "MemoryUIBackgroundBlur";
        Undo.RegisterCreatedObjectUndo(newFeature, "Install Memory UI Blur Feature");

        AssetDatabase.AddObjectToAsset(newFeature, rendererData);
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(newFeature, out string _, out long localId);

        SerializedObject serializedObject = new SerializedObject(rendererData);
        SerializedProperty rendererFeatures = serializedObject.FindProperty("m_RendererFeatures");
        SerializedProperty rendererFeatureMap = serializedObject.FindProperty("m_RendererFeatureMap");

        rendererFeatures.arraySize++;
        rendererFeatures.GetArrayElementAtIndex(rendererFeatures.arraySize - 1).objectReferenceValue = newFeature;

        rendererFeatureMap.arraySize++;
        rendererFeatureMap.GetArrayElementAtIndex(rendererFeatureMap.arraySize - 1).longValue = localId;

        serializedObject.ApplyModifiedProperties();
        rendererData.SetDirty();
        EditorUtility.SetDirty(newFeature);
        EditorUtility.SetDirty(rendererData);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = rendererData;
        EditorGUIUtility.PingObject(newFeature);
        Debug.Log("[MemoryUIBlurRendererFeatureInstaller] Installed Memory UI blur feature on URP-HighFidelity-Renderer.");
    }
}
