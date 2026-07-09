using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MemoryUIBeveledPanel3D))]
[CanEditMultipleObjects]
public class MemoryUIBeveledPanel3DEditor : Editor
{
    private SerializedProperty targetRectProperty;
    private SerializedProperty overrideMeshProperty;
    private SerializedProperty sharedMaterialProperty;
    private SerializedProperty syncWithTargetProperty;
    private SerializedProperty sizePaddingProperty;
    private SerializedProperty zOffsetProperty;
    private SerializedProperty thicknessProperty;
    private SerializedProperty cornerRadiusProperty;
    private SerializedProperty bevelSizeProperty;
    private SerializedProperty cornerSegmentsProperty;
    private SerializedProperty tintColorProperty;
    private SerializedProperty emissionColorProperty;
    private SerializedProperty emissionIntensityProperty;
    private SerializedProperty blurPixelsProperty;
    private SerializedProperty tintStrengthProperty;
    private SerializedProperty backgroundInfluenceProperty;
    private SerializedProperty backgroundLumaThresholdProperty;
    private SerializedProperty backgroundLumaKneeProperty;
    private SerializedProperty brightSceneAbsorptionProperty;
    private SerializedProperty fresnelPowerProperty;
    private SerializedProperty edgeStrengthProperty;
    private SerializedProperty alphaSoftnessProperty;
    private SerializedProperty refractionStrengthProperty;
    private SerializedProperty distortionStrengthProperty;
    private SerializedProperty distortionScaleProperty;

    private void OnEnable()
    {
        targetRectProperty = serializedObject.FindProperty("targetRect");
        overrideMeshProperty = serializedObject.FindProperty("overrideMesh");
        sharedMaterialProperty = serializedObject.FindProperty("sharedMaterial");
        syncWithTargetProperty = serializedObject.FindProperty("syncWithTarget");
        sizePaddingProperty = serializedObject.FindProperty("sizePadding");
        zOffsetProperty = serializedObject.FindProperty("zOffset");
        thicknessProperty = serializedObject.FindProperty("thickness");
        cornerRadiusProperty = serializedObject.FindProperty("cornerRadius");
        bevelSizeProperty = serializedObject.FindProperty("bevelSize");
        cornerSegmentsProperty = serializedObject.FindProperty("cornerSegments");
        tintColorProperty = serializedObject.FindProperty("tintColor");
        emissionColorProperty = serializedObject.FindProperty("emissionColor");
        emissionIntensityProperty = serializedObject.FindProperty("emissionIntensity");
        blurPixelsProperty = serializedObject.FindProperty("blurPixels");
        tintStrengthProperty = serializedObject.FindProperty("tintStrength");
        backgroundInfluenceProperty = serializedObject.FindProperty("backgroundInfluence");
        backgroundLumaThresholdProperty = serializedObject.FindProperty("backgroundLumaThreshold");
        backgroundLumaKneeProperty = serializedObject.FindProperty("backgroundLumaKnee");
        brightSceneAbsorptionProperty = serializedObject.FindProperty("brightSceneAbsorption");
        fresnelPowerProperty = serializedObject.FindProperty("fresnelPower");
        edgeStrengthProperty = serializedObject.FindProperty("edgeStrength");
        alphaSoftnessProperty = serializedObject.FindProperty("alphaSoftness");
        refractionStrengthProperty = serializedObject.FindProperty("refractionStrength");
        distortionStrengthProperty = serializedObject.FindProperty("distortionStrength");
        distortionScaleProperty = serializedObject.FindProperty("distortionScale");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.HelpBox(
            "This component is the primary control surface for hi-fi glass. These values are pushed to the shader via MaterialPropertyBlock, so tune blur and glass response here rather than on the shared material.",
            MessageType.Info);

        DrawToolbarButtons();

        EditorGUI.BeginChangeCheck();

        DrawSection("Binding", DrawBindingSection);
        EditorGUILayout.Space(4f);
        DrawSection("Geometry", DrawGeometrySection);
        EditorGUILayout.Space(4f);
        DrawSection("Glass Response", DrawGlassSection);

        bool changed = EditorGUI.EndChangeCheck();
        serializedObject.ApplyModifiedProperties();

        if (changed)
        {
            SyncTargets();
        }
    }

    private void DrawToolbarButtons()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Sync Now"))
            {
                SyncTargets();
            }

            if (GUILayout.Button("Apply Recommended"))
            {
                foreach (Object targetObject in targets)
                {
                    MemoryUIBeveledPanel3D panel = (MemoryUIBeveledPanel3D)targetObject;
                    Undo.RecordObject(panel, "Apply Recommended HiFi Glass Tuning");
                    panel.ApplyRecommendedGlassTuning();
                    EditorUtility.SetDirty(panel);
                }

                serializedObject.Update();
            }
        }
    }

    private void DrawSection(string title, System.Action drawContent)
    {
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        using (new EditorGUI.IndentLevelScope())
        {
            drawContent();
        }
    }

    private void DrawBindingSection()
    {
        EditorGUILayout.PropertyField(targetRectProperty);
        EditorGUILayout.PropertyField(overrideMeshProperty);
        EditorGUILayout.PropertyField(sharedMaterialProperty);
        EditorGUILayout.PropertyField(syncWithTargetProperty);
    }

    private void DrawGeometrySection()
    {
        EditorGUILayout.PropertyField(sizePaddingProperty);
        EditorGUILayout.PropertyField(zOffsetProperty);
        EditorGUILayout.PropertyField(thicknessProperty);
        EditorGUILayout.PropertyField(cornerRadiusProperty);
        EditorGUILayout.PropertyField(bevelSizeProperty);
        EditorGUILayout.PropertyField(cornerSegmentsProperty);
    }

    private void DrawGlassSection()
    {
        EditorGUILayout.PropertyField(tintColorProperty, new GUIContent("Tint Color"));
        EditorGUILayout.PropertyField(emissionColorProperty, new GUIContent("Edge / Glow Color"));
        EditorGUILayout.PropertyField(emissionIntensityProperty, new GUIContent("Glow Intensity"));
        EditorGUILayout.Space(2f);
        EditorGUILayout.PropertyField(blurPixelsProperty, new GUIContent("Blur Pixels"));
        EditorGUILayout.PropertyField(backgroundInfluenceProperty, new GUIContent("Background Influence"));
        EditorGUILayout.PropertyField(backgroundLumaThresholdProperty, new GUIContent("BG Brightness Threshold"));
        EditorGUILayout.PropertyField(backgroundLumaKneeProperty, new GUIContent("BG Highlight Roll-Off"));
        EditorGUILayout.PropertyField(brightSceneAbsorptionProperty, new GUIContent("Bright Scene Absorption"));
        EditorGUILayout.PropertyField(tintStrengthProperty, new GUIContent("Tint Strength"));
        EditorGUILayout.PropertyField(alphaSoftnessProperty, new GUIContent("Glass Alpha"));
        EditorGUILayout.Space(2f);
        EditorGUILayout.PropertyField(refractionStrengthProperty, new GUIContent("Refraction Strength"));
        EditorGUILayout.PropertyField(distortionStrengthProperty, new GUIContent("Distortion Strength"));
        EditorGUILayout.PropertyField(distortionScaleProperty, new GUIContent("Distortion Scale"));
        EditorGUILayout.Space(2f);
        EditorGUILayout.PropertyField(fresnelPowerProperty, new GUIContent("Fresnel Power"));
        EditorGUILayout.PropertyField(edgeStrengthProperty, new GUIContent("Edge Strength"));
    }

    private void SyncTargets()
    {
        foreach (Object targetObject in targets)
        {
            MemoryUIBeveledPanel3D panel = (MemoryUIBeveledPanel3D)targetObject;
            if (panel == null)
            {
                continue;
            }

            panel.QueueSync();
            EditorUtility.SetDirty(panel);
        }
    }
}
