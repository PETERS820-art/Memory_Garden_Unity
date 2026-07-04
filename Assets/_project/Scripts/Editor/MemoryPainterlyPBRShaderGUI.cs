#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public class MemoryPainterlyPBRShaderGUI : ShaderGUI
{
    private static bool surfaceOptionsFoldout = true;
    private static bool surfaceInputsFoldout = true;
    private static bool advancedOptionsFoldout = false;
    private static bool painterlyPaletteFoldout = true;
    private static bool flatteningFoldout = true;
    private static bool strokesFoldout = true;
    private static bool viewProjectionFoldout = false;
    private static bool shadowEdgeFoldout = false;
    private static bool growthFoldout = false;
    private static bool painterlyTexturesFoldout = false;

    private MaterialEditor materialEditor;
    private MaterialProperty[] properties;

    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        this.materialEditor = materialEditor;
        this.properties = properties;

        foreach (Object target in materialEditor.targets)
        {
            if (target is Material targetMaterial)
            {
                MemoryPainterlyPBRMaterialUtility.SetupMaterial(targetMaterial);
            }
        }

        Material material = materialEditor.target as Material;
        if (material == null)
        {
            return;
        }

        EditorGUI.BeginChangeCheck();

        DrawSurfaceOptions();
        DrawSurfaceInputs();
        DrawAdvancedOptions();
        DrawPainterlyPalette();
        DrawFlattening();
        DrawPainterlyStrokes();
        DrawViewProjection();
        DrawShadowEdges();
        DrawGrowth();
        DrawPainterlyTextures();

        if (EditorGUI.EndChangeCheck())
        {
            foreach (Object target in materialEditor.targets)
            {
                if (target is Material targetMaterial)
                {
                    MemoryPainterlyPBRMaterialUtility.SetupMaterial(targetMaterial);
                    EditorUtility.SetDirty(targetMaterial);
                }
            }
        }
    }

    private void DrawSurfaceOptions()
    {
        surfaceOptionsFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(surfaceOptionsFoldout, "Surface Options");
        if (surfaceOptionsFoldout)
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.Popup("Workflow Mode", 0, new[] { "Metallic" });
            }

            materialEditor.ShaderProperty(FindProperty("_Surface"), "Surface Type");
            if (FindProperty("_Surface").floatValue >= 0.5f)
            {
                materialEditor.ShaderProperty(FindProperty("_Blend"), "Blend Mode");
            }

            DrawRenderFaceProperty();
            materialEditor.ShaderProperty(FindProperty("_AlphaClip"), "Alpha Clipping");
            if (FindProperty("_AlphaClip").floatValue >= 0.5f)
            {
                materialEditor.ShaderProperty(FindProperty("_Cutoff"), "Alpha Cutoff");
            }

            materialEditor.ShaderProperty(FindProperty("_ReceiveShadows"), "Receive Shadows");
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space(2f);
    }

    private void DrawSurfaceInputs()
    {
        surfaceInputsFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(surfaceInputsFoldout, "Surface Inputs");
        if (surfaceInputsFoldout)
        {
            MaterialProperty baseMap = FindProperty("_BaseMap");
            MaterialProperty baseColor = FindProperty("_BaseColor");
            materialEditor.TexturePropertySingleLine(new GUIContent("Base Map"), baseMap, baseColor);
            materialEditor.TextureScaleOffsetProperty(baseMap);

            MaterialProperty normalMap = FindProperty("_BumpMap");
            materialEditor.TexturePropertySingleLine(new GUIContent("Normal Map"), normalMap, FindProperty("_BumpScale"));
            materialEditor.TextureScaleOffsetProperty(normalMap);

            MaterialProperty metallicGlossMap = FindProperty("_MetallicGlossMap");
            materialEditor.TexturePropertySingleLine(new GUIContent("Metallic Smoothness Map"), metallicGlossMap);
            materialEditor.TextureScaleOffsetProperty(metallicGlossMap);
            materialEditor.ShaderProperty(FindProperty("_Metallic"), "Metallic");
            materialEditor.ShaderProperty(FindProperty("_Smoothness"), "Smoothness");

            MaterialProperty occlusionMap = FindProperty("_OcclusionMap");
            materialEditor.TexturePropertySingleLine(new GUIContent("Occlusion Map"), occlusionMap, FindProperty("_OcclusionStrength"));
            materialEditor.TextureScaleOffsetProperty(occlusionMap);

            MaterialProperty emissionMap = FindProperty("_EmissionMap");
            materialEditor.TexturePropertySingleLine(new GUIContent("Emission Map"), emissionMap, FindProperty("_EmissionColor"));
            materialEditor.TextureScaleOffsetProperty(emissionMap);

            materialEditor.ShaderProperty(FindProperty("_PainterlyScale"), "Painterly Scale");
            materialEditor.ShaderProperty(FindProperty("_MemoryBlend"), "Memory Blend");
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space(2f);
    }

    private void DrawAdvancedOptions()
    {
        advancedOptionsFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(advancedOptionsFoldout, "Advanced Options");
        if (advancedOptionsFoldout)
        {
            materialEditor.ShaderProperty(FindProperty("_SpecularHighlights"), "Specular Highlights");
            materialEditor.ShaderProperty(FindProperty("_EnvironmentReflections"), "Environment Reflections");
            materialEditor.ShaderProperty(FindProperty("_QueueOffset"), "Queue Offset");
            materialEditor.EnableInstancingField();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space(2f);
    }

    private void DrawPainterlyPalette()
    {
        painterlyPaletteFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(painterlyPaletteFoldout, "Painterly Palette");
        if (painterlyPaletteFoldout)
        {
            materialEditor.ShaderProperty(FindProperty("_ShadowColor"), "Shadow Color");
            materialEditor.ShaderProperty(FindProperty("_LightTintColor"), "Light Tint Color");
            materialEditor.ShaderProperty(FindProperty("_AccentColor"), "Accent Color");
            materialEditor.ShaderProperty(FindProperty("_AccentColorStrength"), "Accent Color Strength");
            materialEditor.ShaderProperty(FindProperty("_EmotionTintColor"), "Emotion Tint Color");
            materialEditor.ShaderProperty(FindProperty("_EmotionTintStrength"), "Emotion Tint Strength");
            materialEditor.ShaderProperty(FindProperty("_RimColor"), "Rim Color");
            materialEditor.ShaderProperty(FindProperty("_RimStrength"), "Rim Strength");
            materialEditor.ShaderProperty(FindProperty("_RimPower"), "Rim Power");
            materialEditor.ShaderProperty(FindProperty("_Saturation"), "Saturation");
            materialEditor.ShaderProperty(FindProperty("_Brightness"), "Brightness");
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space(2f);
    }

    private void DrawFlattening()
    {
        flatteningFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(flatteningFoldout, "Flattening");
        if (flatteningFoldout)
        {
            materialEditor.ShaderProperty(FindProperty("_FlattenAmount"), "Flatten Amount");
            materialEditor.ShaderProperty(FindProperty("_LightRangeCompression"), "Light Range Compression");
            materialEditor.ShaderProperty(FindProperty("_ShadeSteps"), "Shade Steps");
            materialEditor.ShaderProperty(FindProperty("_NormalFlatten"), "Normal Flatten");
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space(2f);
    }

    private void DrawPainterlyStrokes()
    {
        strokesFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(strokesFoldout, "Painterly Strokes");
        if (strokesFoldout)
        {
            materialEditor.ShaderProperty(FindProperty("_StrokeDensity"), "Stroke Density");
            materialEditor.ShaderProperty(FindProperty("_StrokeContrast"), "Stroke Contrast");
            materialEditor.ShaderProperty(FindProperty("_ShadowThreshold"), "Shadow Threshold");
            materialEditor.ShaderProperty(FindProperty("_ShadowSoftness"), "Shadow Softness");
            materialEditor.ShaderProperty(FindProperty("_RampInfluence"), "Ramp Influence");
            materialEditor.ShaderProperty(FindProperty("_BrushGrainStrength"), "Brush Grain Strength");
            materialEditor.ShaderProperty(FindProperty("_DryBrushStrength"), "Dry Brush Strength");
            materialEditor.ShaderProperty(FindProperty("_WatercolorStrength"), "Watercolor Strength");
            materialEditor.ShaderProperty(FindProperty("_EdgeBreakStrength"), "Edge Break Strength");
            materialEditor.ShaderProperty(FindProperty("_EdgeDistortion"), "Edge Distortion");
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space(2f);
    }

    private void DrawViewProjection()
    {
        viewProjectionFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(viewProjectionFoldout, "View Projection");
        if (viewProjectionFoldout)
        {
            materialEditor.ShaderProperty(FindProperty("_ViewProjectionBlend"), "View Projection Blend");
            materialEditor.ShaderProperty(FindProperty("_ViewBrushStrength"), "View Brush Strength");
            materialEditor.ShaderProperty(FindProperty("_ScreenGrainStrength"), "Screen Grain Strength");
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space(2f);
    }

    private void DrawShadowEdges()
    {
        shadowEdgeFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(shadowEdgeFoldout, "Shadow Edge Breakup");
        if (shadowEdgeFoldout)
        {
            materialEditor.ShaderProperty(FindProperty("_ShadowEdgeBreakStrength"), "Shadow Edge Break Strength");
            materialEditor.ShaderProperty(FindProperty("_ShadowEdgeNoiseScale"), "Shadow Edge Noise Scale");
            materialEditor.ShaderProperty(FindProperty("_ShadowEdgeBrushInfluence"), "Shadow Edge Brush Influence");
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space(2f);
    }

    private void DrawGrowth()
    {
        growthFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(growthFoldout, "Runtime Growth");
        if (growthFoldout)
        {
            materialEditor.ShaderProperty(FindProperty("_GrowthMaxRadius"), "Growth Max Radius");
            materialEditor.ShaderProperty(FindProperty("_GrowthSoftness"), "Growth Softness");
            materialEditor.ShaderProperty(FindProperty("_GrowthNoiseStrength"), "Growth Noise Strength");
            materialEditor.ShaderProperty(FindProperty("_GrowthBlend"), "Growth Blend");
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space(2f);
    }

    private void DrawPainterlyTextures()
    {
        painterlyTexturesFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(painterlyTexturesFoldout, "Painterly Textures");
        if (painterlyTexturesFoldout)
        {
            materialEditor.TexturePropertySingleLine(new GUIContent("Brush Ramp"), FindProperty("_BrushRampTex"));
            materialEditor.TexturePropertySingleLine(new GUIContent("Brush Grain"), FindProperty("_BrushGrainTex"));
            materialEditor.TexturePropertySingleLine(new GUIContent("Dry Brush"), FindProperty("_DryBrushTex"));
            materialEditor.TexturePropertySingleLine(new GUIContent("Watercolor"), FindProperty("_WatercolorTex"));
            materialEditor.TexturePropertySingleLine(new GUIContent("Edge Break"), FindProperty("_EdgeBreakTex"));
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void DrawRenderFaceProperty()
    {
        MaterialProperty cullProperty = FindProperty("_Cull");
        int popupIndex = CullValueToPopupIndex(Mathf.RoundToInt(cullProperty.floatValue));
        EditorGUI.BeginChangeCheck();
        popupIndex = EditorGUILayout.Popup("Render Face", popupIndex, new[] { "Both", "Front", "Back" });
        if (EditorGUI.EndChangeCheck())
        {
            cullProperty.floatValue = PopupIndexToCullValue(popupIndex);
        }
    }

    private MaterialProperty FindProperty(string propertyName)
    {
        return FindProperty(propertyName, properties);
    }

    private static int CullValueToPopupIndex(int cullValue)
    {
        switch (cullValue)
        {
            case 0:
                return 0;
            case 1:
                return 1;
            default:
                return 2;
        }
    }

    private static int PopupIndexToCullValue(int popupIndex)
    {
        switch (popupIndex)
        {
            case 0:
                return 0;
            case 1:
                return 1;
            default:
                return 2;
        }
    }
}
#endif
