using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public class MemoryModeShaderManager : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int PainterlyBaseColorId = Shader.PropertyToID("_PainterlyBaseColor");
    private static readonly int ShadowColorId = Shader.PropertyToID("_ShadowColor");
    private static readonly int LightTintColorId = Shader.PropertyToID("_LightTintColor");
    private static readonly int AccentColorId = Shader.PropertyToID("_AccentColor");
    private static readonly int AccentColorStrengthId = Shader.PropertyToID("_AccentColorStrength");
    private static readonly int EmotionTintColorId = Shader.PropertyToID("_EmotionTintColor");
    private static readonly int RimColorId = Shader.PropertyToID("_RimColor");
    private static readonly int EmotionTintStrengthId = Shader.PropertyToID("_EmotionTintStrength");
    private static readonly int RimStrengthId = Shader.PropertyToID("_RimStrength");
    private static readonly int RimPowerId = Shader.PropertyToID("_RimPower");
    private static readonly int FlattenAmountId = Shader.PropertyToID("_FlattenAmount");
    private static readonly int LightRangeCompressionId = Shader.PropertyToID("_LightRangeCompression");
    private static readonly int ShadeStepsId = Shader.PropertyToID("_ShadeSteps");
    private static readonly int NormalFlattenId = Shader.PropertyToID("_NormalFlatten");
    private static readonly int StrokeDensityId = Shader.PropertyToID("_StrokeDensity");
    private static readonly int StrokeContrastId = Shader.PropertyToID("_StrokeContrast");
    private static readonly int SaturationId = Shader.PropertyToID("_Saturation");
    private static readonly int BrightnessId = Shader.PropertyToID("_Brightness");
    private static readonly int ShadowThresholdId = Shader.PropertyToID("_ShadowThreshold");
    private static readonly int ShadowSoftnessId = Shader.PropertyToID("_ShadowSoftness");
    private static readonly int RampInfluenceId = Shader.PropertyToID("_RampInfluence");
    private static readonly int BrushGrainStrengthId = Shader.PropertyToID("_BrushGrainStrength");
    private static readonly int DryBrushStrengthId = Shader.PropertyToID("_DryBrushStrength");
    private static readonly int WatercolorStrengthId = Shader.PropertyToID("_WatercolorStrength");
    private static readonly int EdgeBreakStrengthId = Shader.PropertyToID("_EdgeBreakStrength");
    private static readonly int EdgeDistortionId = Shader.PropertyToID("_EdgeDistortion");
    private static readonly int ViewProjectionBlendId = Shader.PropertyToID("_ViewProjectionBlend");
    private static readonly int ViewBrushStrengthId = Shader.PropertyToID("_ViewBrushStrength");
    private static readonly int ScreenGrainStrengthId = Shader.PropertyToID("_ScreenGrainStrength");
    private static readonly int ShadowEdgeBreakStrengthId = Shader.PropertyToID("_ShadowEdgeBreakStrength");
    private static readonly int ShadowEdgeNoiseScaleId = Shader.PropertyToID("_ShadowEdgeNoiseScale");
    private static readonly int ShadowEdgeBrushInfluenceId = Shader.PropertyToID("_ShadowEdgeBrushInfluence");
    private static readonly int PainterlyScaleId = Shader.PropertyToID("_PainterlyScale");
    private static readonly int GrowthOriginId = Shader.PropertyToID("_GrowthOrigin");
    private static readonly int GrowthRadiusId = Shader.PropertyToID("_GrowthRadius");
    private static readonly int GrowthMaxRadiusId = Shader.PropertyToID("_GrowthMaxRadius");
    private static readonly int GrowthSoftnessId = Shader.PropertyToID("_GrowthSoftness");
    private static readonly int GrowthNoiseStrengthId = Shader.PropertyToID("_GrowthNoiseStrength");
    private static readonly int GrowthBlendId = Shader.PropertyToID("_GrowthBlend");
    private static readonly int RuntimeTransitionActiveId = Shader.PropertyToID("_RuntimeTransitionActive");
    private static readonly int MemoryBlendId = Shader.PropertyToID("_MemoryBlend");
    private static readonly int BrushRampTexId = Shader.PropertyToID("_BrushRampTex");
    private static readonly int BrushGrainTexId = Shader.PropertyToID("_BrushGrainTex");
    private static readonly int DryBrushTexId = Shader.PropertyToID("_DryBrushTex");
    private static readonly int WatercolorTexId = Shader.PropertyToID("_WatercolorTex");
    private static readonly int EdgeBreakTexId = Shader.PropertyToID("_EdgeBreakTex");

    private static readonly string[] AutoExcludedNameKeywords =
    {
        "xr rig",
        "xrrig",
        "controller",
        "debug",
        "ui"
    };

    private struct ShaderState
    {
        public Color baseColor;
        public Color shadowColor;
        public Color lightTintColor;
        public Color accentColor;
        public Color emotionTintColor;
        public Color rimColor;
        public float emotionTintStrength;
        public float rimStrength;
        public float flattenAmount;
        public float viewProjectionBlend;
        public float viewBrushStrength;
        public float shadowEdgeBreakStrength;
        public float memoryBlend;
        public Vector3 growthOrigin;
        public float growthRadius;
        public float growthMaxRadius;
        public float growthSoftness;
        public float growthNoiseStrength;
        public float growthBlend;
    }

    private sealed class RendererState
    {
        public Renderer renderer;
        public int[] stylizedMaterialIndices;
        public MaterialPropertyBlock[] originalPropertyBlocks;
    }

    [Header("References")]
    [SerializeField] private MemoryModeManager memoryModeManager;
    [SerializeField] private Material memoryPainterlyTemplate;
    [SerializeField] private EmotionMaterialLog emotionMaterialLog;

    [Header("Stylizable Renderers")]
    [SerializeField] private List<Transform> stylizableRoots = new List<Transform>();
    [SerializeField] private LayerMask stylizableLayers;
    [SerializeField] private List<Transform> excludedRoots = new List<Transform>();
    [SerializeField] private LayerMask excludedLayers;

    [Header("Shader Transition")]
    [SerializeField] private float shaderTransitionDuration = 0.35f;
    [SerializeField] private float growthMaxRadius = 12f;
    [SerializeField] private float growthSoftness = 1.25f;
    [SerializeField] private float growthNoiseStrength = 0.5f;
    [FormerlySerializedAs("shaderTransitionCurve")]
    [SerializeField] private AnimationCurve growthCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Debug")]
    [SerializeField] private bool liveSyncSourceMaterial = true;
    [SerializeField] private bool debugLogs;

    private readonly List<RendererState> activeRendererStates = new List<RendererState>();
    private readonly HashSet<Renderer> rendererSet = new HashSet<Renderer>();
    private MaterialPropertyBlock workingPropertyBlock;
    private static readonly int[] PainterlyColorPropertyIds =
    {
        ShadowColorId,
        LightTintColorId,
        AccentColorId,
        EmotionTintColorId,
        RimColorId
    };

    private static readonly int[] PainterlyFloatPropertyIds =
    {
        AccentColorStrengthId,
        EmotionTintStrengthId,
        RimStrengthId,
        RimPowerId,
        FlattenAmountId,
        LightRangeCompressionId,
        ShadeStepsId,
        NormalFlattenId,
        StrokeDensityId,
        StrokeContrastId,
        SaturationId,
        BrightnessId,
        ShadowThresholdId,
        ShadowSoftnessId,
        RampInfluenceId,
        BrushGrainStrengthId,
        DryBrushStrengthId,
        WatercolorStrengthId,
        EdgeBreakStrengthId,
        EdgeDistortionId,
        ViewProjectionBlendId,
        ViewBrushStrengthId,
        ScreenGrainStrengthId,
        ShadowEdgeBreakStrengthId,
        ShadowEdgeNoiseScaleId,
        ShadowEdgeBrushInfluenceId,
        PainterlyScaleId
    };

    private static readonly int[] PainterlyTexturePropertyIds =
    {
        BrushRampTexId,
        BrushGrainTexId,
        DryBrushTexId,
        WatercolorTexId,
        EdgeBreakTexId
    };

    private Coroutine shaderTransitionCoroutine;
    private bool isSubscribed;
    private Vector3 activeGrowthOrigin;
    private float activeGrowthMaxRadius;
    private Material activeEmotionMaterial;
    private ShaderState currentShaderState;
    private bool hasCurrentShaderState;

    private void Awake()
    {
        workingPropertyBlock = new MaterialPropertyBlock();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (emotionMaterialLog == null)
        {
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:EmotionMaterialLog", new[] { "Assets/_project/ScriptableObjects/EmotionMaterialLog" });
            if (guids.Length > 0)
            {
                string assetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                emotionMaterialLog = UnityEditor.AssetDatabase.LoadAssetAtPath<EmotionMaterialLog>(assetPath);
            }
        }

        if (memoryPainterlyTemplate == null && emotionMaterialLog != null && emotionMaterialLog.FallbackMaterial != null)
        {
            memoryPainterlyTemplate = emotionMaterialLog.FallbackMaterial;
        }
    }
#endif

    private void OnEnable()
    {
        TryResolveManager();
        SubscribeToManager();
    }

    private void Start()
    {
        TryResolveManager();
        SubscribeToManager();
    }

    private void OnDisable()
    {
        UnsubscribeFromManager();
        StopActiveTransition();
        RestoreOriginalMaterialsImmediate();
    }

    private void Update()
    {
        if (!Application.isPlaying || !liveSyncSourceMaterial || shaderTransitionCoroutine != null)
        {
            return;
        }

        if (activeRendererStates.Count == 0 || activeEmotionMaterial == null || !hasCurrentShaderState)
        {
            return;
        }

        ShaderState syncedState = ReadStateFromMaterial(activeEmotionMaterial);
        syncedState = ApplyGrowthTargets(syncedState, activeGrowthOrigin, currentShaderState.growthRadius, currentShaderState.growthBlend);
        syncedState.memoryBlend = currentShaderState.memoryBlend;
        ApplyStateToAllRenderers(syncedState);
    }

    private void TryResolveManager()
    {
        if (memoryModeManager != null)
        {
            return;
        }

        memoryModeManager = MemoryModeManager.Instance;

        if (memoryModeManager == null)
        {
            memoryModeManager = FindObjectOfType<MemoryModeManager>(true);
        }
    }

    private void SubscribeToManager()
    {
        if (isSubscribed || memoryModeManager == null)
        {
            return;
        }

        memoryModeManager.OnMemoryModeEntered += HandleMemoryModeEntered;
        memoryModeManager.OnMemoryModeExited += HandleMemoryModeExited;
        isSubscribed = true;
    }

    private void UnsubscribeFromManager()
    {
        if (!isSubscribed || memoryModeManager == null)
        {
            return;
        }

        memoryModeManager.OnMemoryModeEntered -= HandleMemoryModeEntered;
        memoryModeManager.OnMemoryModeExited -= HandleMemoryModeExited;
        isSubscribed = false;
    }

    private void HandleMemoryModeEntered(MemoryObject focusedObject)
    {
        ApplyMemoryModeShader(focusedObject);
    }

    private void HandleMemoryModeExited(MemoryObject focusedObject)
    {
        BeginRestoreOriginalMaterials();
    }

    private void ApplyMemoryModeShader(MemoryObject focusedObject)
    {
        if (focusedObject == null)
        {
            Log("Focused MemoryObject is null. Shader application skipped.", true);
            return;
        }

        StopActiveTransition();
        RestoreOriginalMaterialsImmediate();

        Material targetEmotionMaterial = ResolveEmotionMaterial(focusedObject.EmotionType);
        if (targetEmotionMaterial == null)
        {
            Log($"No emotion material could be resolved for '{focusedObject.EmotionType}'. Shader application skipped.", true);
            return;
        }

        activeGrowthOrigin = focusedObject.GetObservationCenter();
        CollectStylizableRenderers(focusedObject);

        if (activeRendererStates.Count == 0)
        {
            Log(
                $"No stylizable renderers using a painterly-compatible material were found for focus '{focusedObject.name}'. " +
                "Only materials that already expose _MemoryBlend will be driven by Memory Mode.",
                false);
            return;
        }

        activeGrowthMaxRadius = ResolveGrowthMaxRadius(activeGrowthOrigin);
        activeEmotionMaterial = targetEmotionMaterial;

        ShaderState targetState = ReadStateFromMaterial(targetEmotionMaterial);
        targetState.memoryBlend = 1f;
        targetState = ApplyGrowthTargets(targetState, activeGrowthOrigin, activeGrowthMaxRadius, 1f);
        ShaderState startState = targetState;
        startState.memoryBlend = 0f;
        startState.growthRadius = 0f;
        startState.growthBlend = 0f;

        ApplyStateToAllRenderers(startState);
        shaderTransitionCoroutine = StartCoroutine(AnimateShaderState(startState, targetState, true));

        Log(
            $"Drove Memory Painterly parameters on {activeRendererStates.Count} renderer(s) for emotion '{focusedObject.EmotionType}'. " +
            $"Focused item '{focusedObject.name}' kept its own materials.",
            false);
    }

    private void BeginRestoreOriginalMaterials()
    {
        if (activeRendererStates.Count == 0)
        {
            return;
        }

        StopActiveTransition();

        ShaderState fromState = hasCurrentShaderState
            ? currentShaderState
            : ApplyGrowthTargets(ReadStateFromMaterial(activeEmotionMaterial), activeGrowthOrigin, activeGrowthMaxRadius, 1f);

        ShaderState toState = fromState;
        toState.memoryBlend = 0f;
        toState.growthRadius = 0f;
        toState.growthBlend = 0f;

        shaderTransitionCoroutine = StartCoroutine(AnimateShaderState(fromState, toState, false));
    }

    private void CollectStylizableRenderers(MemoryObject focusedObject)
    {
        bool useRoots = stylizableRoots.Count > 0;
        bool useLayers = stylizableLayers.value != 0;
        bool collectAllSceneRenderers = !useRoots && !useLayers;

        rendererSet.Clear();
        activeRendererStates.Clear();

        for (int i = 0; i < stylizableRoots.Count; i++)
        {
            Transform root = stylizableRoots[i];
            if (root == null)
            {
                continue;
            }

            Renderer[] childRenderers = root.GetComponentsInChildren<Renderer>(true);
            for (int j = 0; j < childRenderers.Length; j++)
            {
                rendererSet.Add(childRenderers[j]);
            }
        }

        Renderer[] sceneRenderers = FindObjectsOfType<Renderer>(true);
        for (int i = 0; i < sceneRenderers.Length; i++)
        {
            Renderer renderer = sceneRenderers[i];
            if (renderer == null)
            {
                continue;
            }

            if (collectAllSceneRenderers || (useLayers && IsInLayerMask(renderer.gameObject.layer, stylizableLayers)))
            {
                rendererSet.Add(renderer);
            }
        }

        foreach (Renderer renderer in rendererSet)
        {
            if (!ShouldStylizeRenderer(renderer, focusedObject))
            {
                continue;
            }

            Material[] sharedMaterials = renderer.sharedMaterials;
            if (sharedMaterials == null || sharedMaterials.Length == 0)
            {
                continue;
            }

            List<int> stylizedMaterialIndices = new List<int>();
            for (int i = 0; i < sharedMaterials.Length; i++)
            {
                if (MaterialSupportsMemoryMode(sharedMaterials[i]))
                {
                    stylizedMaterialIndices.Add(i);
                }
            }

            if (stylizedMaterialIndices.Count == 0)
            {
                continue;
            }

            MaterialPropertyBlock[] originalPropertyBlocks = new MaterialPropertyBlock[stylizedMaterialIndices.Count];
            for (int i = 0; i < stylizedMaterialIndices.Count; i++)
            {
                int materialIndex = stylizedMaterialIndices[i];
                MaterialPropertyBlock block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block, materialIndex);
                originalPropertyBlocks[i] = block;
            }

            activeRendererStates.Add(new RendererState
            {
                renderer = renderer,
                stylizedMaterialIndices = stylizedMaterialIndices.ToArray(),
                originalPropertyBlocks = originalPropertyBlocks
            });
        }
    }

    private bool ShouldStylizeRenderer(Renderer renderer, MemoryObject focusedObject)
    {
        if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
        {
            return false;
        }

        if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer))
        {
            return false;
        }

        if (focusedObject != null && renderer.transform.IsChildOf(focusedObject.transform))
        {
            return false;
        }

        if (renderer.GetComponentInParent<MemoryShaderExclude>(true) != null)
        {
            return false;
        }

        if (renderer.GetComponentInParent<Canvas>(true) != null)
        {
            return false;
        }

        if (IsInLayerMask(renderer.gameObject.layer, excludedLayers))
        {
            return false;
        }

        string layerName = LayerMask.LayerToName(renderer.gameObject.layer);
        if (!string.IsNullOrEmpty(layerName))
        {
            string lowerLayerName = layerName.ToLowerInvariant();
            if (lowerLayerName.Contains("ui") || lowerLayerName.Contains("debug"))
            {
                return false;
            }
        }

        for (int i = 0; i < excludedRoots.Count; i++)
        {
            Transform excludedRoot = excludedRoots[i];
            if (excludedRoot != null && renderer.transform.IsChildOf(excludedRoot))
            {
                return false;
            }
        }

        string lowerName = renderer.transform.root.name.ToLowerInvariant();
        string objectLowerName = renderer.name.ToLowerInvariant();
        for (int i = 0; i < AutoExcludedNameKeywords.Length; i++)
        {
            string keyword = AutoExcludedNameKeywords[i];
            if (lowerName.Contains(keyword) || objectLowerName.Contains(keyword))
            {
                return false;
            }
        }

        return true;
    }

    private Material ResolveEmotionMaterial(string emotionType)
    {
        Material fallbackMaterial = memoryPainterlyTemplate;

        if (emotionMaterialLog == null)
        {
            Log("EmotionMaterialLog is not assigned. Falling back to MemoryPainterly template.", true);
            return fallbackMaterial;
        }

        Material resolvedMaterial = emotionMaterialLog.ResolveMaterial(emotionType, fallbackMaterial);
        if (resolvedMaterial == null)
        {
            Log($"EmotionMaterialLog could not resolve a material for '{emotionType}'.", true);
            return fallbackMaterial;
        }

        return resolvedMaterial;
    }

    private ShaderState ApplyGrowthTargets(ShaderState state, Vector3 growthOrigin, float radius, float blend)
    {
        state.growthOrigin = growthOrigin;
        state.growthRadius = radius;
        state.growthMaxRadius = activeGrowthMaxRadius > 0f ? activeGrowthMaxRadius : growthMaxRadius;
        state.growthSoftness = Mathf.Max(0.01f, growthSoftness);
        state.growthNoiseStrength = Mathf.Max(0f, growthNoiseStrength);
        state.growthBlend = Mathf.Clamp01(blend);
        return state;
    }

    private IEnumerator AnimateShaderState(ShaderState fromState, ShaderState toState, bool keepOverridesAtEnd)
    {
        if (shaderTransitionDuration <= 0f)
        {
            ApplyStateToAllRenderers(toState);
            if (!keepOverridesAtEnd)
            {
                RestoreOriginalMaterialsImmediate();
            }

            shaderTransitionCoroutine = null;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < shaderTransitionDuration)
        {
            elapsed += Time.deltaTime;
            float normalizedTime = Mathf.Clamp01(elapsed / shaderTransitionDuration);
            float curveValue = growthCurve != null ? growthCurve.Evaluate(normalizedTime) : normalizedTime;
            ShaderState blendedState = LerpState(fromState, toState, curveValue);
            ApplyStateToAllRenderers(blendedState);
            yield return null;
        }

        ApplyStateToAllRenderers(toState);

        if (!keepOverridesAtEnd)
        {
            RestoreOriginalMaterialsImmediate();
        }

        shaderTransitionCoroutine = null;
    }

    private ShaderState LerpState(ShaderState fromState, ShaderState toState, float t)
    {
        return new ShaderState
        {
            baseColor = Color.LerpUnclamped(fromState.baseColor, toState.baseColor, t),
            shadowColor = Color.LerpUnclamped(fromState.shadowColor, toState.shadowColor, t),
            lightTintColor = Color.LerpUnclamped(fromState.lightTintColor, toState.lightTintColor, t),
            accentColor = Color.LerpUnclamped(fromState.accentColor, toState.accentColor, t),
            emotionTintColor = Color.LerpUnclamped(fromState.emotionTintColor, toState.emotionTintColor, t),
            rimColor = Color.LerpUnclamped(fromState.rimColor, toState.rimColor, t),
            emotionTintStrength = Mathf.LerpUnclamped(fromState.emotionTintStrength, toState.emotionTintStrength, t),
            rimStrength = Mathf.LerpUnclamped(fromState.rimStrength, toState.rimStrength, t),
            flattenAmount = Mathf.LerpUnclamped(fromState.flattenAmount, toState.flattenAmount, t),
            viewProjectionBlend = Mathf.LerpUnclamped(fromState.viewProjectionBlend, toState.viewProjectionBlend, t),
            viewBrushStrength = Mathf.LerpUnclamped(fromState.viewBrushStrength, toState.viewBrushStrength, t),
            shadowEdgeBreakStrength = Mathf.LerpUnclamped(fromState.shadowEdgeBreakStrength, toState.shadowEdgeBreakStrength, t),
            memoryBlend = Mathf.LerpUnclamped(fromState.memoryBlend, toState.memoryBlend, t),
            growthOrigin = Vector3.LerpUnclamped(fromState.growthOrigin, toState.growthOrigin, t),
            growthRadius = Mathf.LerpUnclamped(fromState.growthRadius, toState.growthRadius, t),
            growthMaxRadius = Mathf.LerpUnclamped(fromState.growthMaxRadius, toState.growthMaxRadius, t),
            growthSoftness = Mathf.LerpUnclamped(fromState.growthSoftness, toState.growthSoftness, t),
            growthNoiseStrength = Mathf.LerpUnclamped(fromState.growthNoiseStrength, toState.growthNoiseStrength, t),
            growthBlend = Mathf.LerpUnclamped(fromState.growthBlend, toState.growthBlend, t)
        };
    }

    private ShaderState ReadStateFromMaterial(Material material)
    {
        ShaderState state = default;

        if (material == null)
        {
            return state;
        }

        state.baseColor = GetColor(material, BaseColorId, Color.white);
        state.shadowColor = GetColor(material, ShadowColorId, new Color(0.55f, 0.63f, 0.72f, 1f));
        state.lightTintColor = GetColor(material, LightTintColorId, new Color(1f, 0.97f, 0.93f, 1f));
        state.accentColor = GetColor(material, AccentColorId, new Color(1f, 0.82f, 0.6f, 1f));
        state.emotionTintColor = GetColor(material, EmotionTintColorId, new Color(0.94f, 0.9f, 0.86f, 1f));
        state.rimColor = GetColor(material, RimColorId, new Color(0.85f, 0.92f, 1f, 1f));
        state.emotionTintStrength = GetFloat(material, EmotionTintStrengthId, 0f);
        state.rimStrength = GetFloat(material, RimStrengthId, 0f);
        state.flattenAmount = GetFloat(material, FlattenAmountId, 0f);
        state.viewProjectionBlend = GetFloat(material, ViewProjectionBlendId, 0f);
        state.viewBrushStrength = GetFloat(material, ViewBrushStrengthId, 0f);
        state.shadowEdgeBreakStrength = GetFloat(material, ShadowEdgeBreakStrengthId, 0f);
        state.memoryBlend = GetFloat(material, MemoryBlendId, 0f);
        state.growthOrigin = GetVector(material, GrowthOriginId, Vector3.zero);
        state.growthRadius = GetFloat(material, GrowthRadiusId, 0f);
        state.growthMaxRadius = GetFloat(material, GrowthMaxRadiusId, growthMaxRadius);
        state.growthSoftness = GetFloat(material, GrowthSoftnessId, growthSoftness);
        state.growthNoiseStrength = GetFloat(material, GrowthNoiseStrengthId, growthNoiseStrength);
        state.growthBlend = GetFloat(material, GrowthBlendId, 0f);

        return state;
    }

    private void ApplyStateToAllRenderers(ShaderState state)
    {
        if (workingPropertyBlock == null)
        {
            workingPropertyBlock = new MaterialPropertyBlock();
        }

        currentShaderState = state;
        hasCurrentShaderState = true;

        workingPropertyBlock.Clear();
        ApplyReferenceMaterialOverrides(workingPropertyBlock, activeEmotionMaterial);
        ApplyShaderStateOverrides(workingPropertyBlock, state);

        for (int i = 0; i < activeRendererStates.Count; i++)
        {
            RendererState rendererState = activeRendererStates[i];
            if (rendererState == null || rendererState.renderer == null || rendererState.stylizedMaterialIndices == null)
            {
                continue;
            }

            for (int j = 0; j < rendererState.stylizedMaterialIndices.Length; j++)
            {
                rendererState.renderer.SetPropertyBlock(workingPropertyBlock, rendererState.stylizedMaterialIndices[j]);
            }
        }
    }

    private void RestoreOriginalMaterialsImmediate()
    {
        for (int i = 0; i < activeRendererStates.Count; i++)
        {
            RendererState rendererState = activeRendererStates[i];
            if (rendererState == null || rendererState.renderer == null || rendererState.stylizedMaterialIndices == null)
            {
                continue;
            }

            for (int j = 0; j < rendererState.stylizedMaterialIndices.Length; j++)
            {
                int materialIndex = rendererState.stylizedMaterialIndices[j];
                MaterialPropertyBlock originalBlock = rendererState.originalPropertyBlocks != null && j < rendererState.originalPropertyBlocks.Length
                    ? rendererState.originalPropertyBlocks[j]
                    : null;

                if (originalBlock != null && !originalBlock.isEmpty)
                {
                    rendererState.renderer.SetPropertyBlock(originalBlock, materialIndex);
                }
                else
                {
                    rendererState.renderer.SetPropertyBlock(null, materialIndex);
                }
            }
        }

        activeRendererStates.Clear();
        activeEmotionMaterial = null;
        currentShaderState = default;
        hasCurrentShaderState = false;
    }

    private float ResolveGrowthMaxRadius(Vector3 growthOrigin)
    {
        if (growthMaxRadius > 0f)
        {
            return growthMaxRadius;
        }

        float requiredRadius = 0f;
        for (int i = 0; i < activeRendererStates.Count; i++)
        {
            RendererState rendererState = activeRendererStates[i];
            if (rendererState == null || rendererState.renderer == null)
            {
                continue;
            }

            Bounds rendererBounds = rendererState.renderer.bounds;
            float distance = Vector3.Distance(rendererBounds.center, growthOrigin) + rendererBounds.extents.magnitude;
            requiredRadius = Mathf.Max(requiredRadius, distance);
        }

        return Mathf.Max(requiredRadius, 0.1f);
    }

    private void StopActiveTransition()
    {
        if (shaderTransitionCoroutine == null)
        {
            return;
        }

        StopCoroutine(shaderTransitionCoroutine);
        shaderTransitionCoroutine = null;
    }

    private static void ApplyShaderStateOverrides(MaterialPropertyBlock propertyBlock, ShaderState state)
    {
        propertyBlock.SetFloat(MemoryBlendId, state.memoryBlend);
        propertyBlock.SetVector(GrowthOriginId, state.growthOrigin);
        propertyBlock.SetFloat(GrowthRadiusId, state.growthRadius);
        propertyBlock.SetFloat(GrowthMaxRadiusId, state.growthMaxRadius);
        propertyBlock.SetFloat(GrowthSoftnessId, state.growthSoftness);
        propertyBlock.SetFloat(GrowthNoiseStrengthId, state.growthNoiseStrength);
        propertyBlock.SetFloat(GrowthBlendId, state.growthBlend);
        propertyBlock.SetFloat(RuntimeTransitionActiveId, 1f);
    }

    private static void ApplyReferenceMaterialOverrides(MaterialPropertyBlock propertyBlock, Material material)
    {
        if (propertyBlock == null || material == null)
        {
            return;
        }

        for (int i = 0; i < PainterlyColorPropertyIds.Length; i++)
        {
            int propertyId = PainterlyColorPropertyIds[i];
            if (!material.HasProperty(propertyId))
            {
                continue;
            }

            propertyBlock.SetColor(propertyId, material.GetColor(propertyId));
        }

        if (material.HasProperty(BaseColorId))
        {
            Color painterlyBaseColor = material.HasProperty(PainterlyBaseColorId)
                ? material.GetColor(PainterlyBaseColorId)
                : new Color(1f, 1f, 1f, 0f);

            if (painterlyBaseColor.a <= 0.001f)
            {
                painterlyBaseColor = material.GetColor(BaseColorId);
                painterlyBaseColor.a = 1f;
            }

            propertyBlock.SetColor(PainterlyBaseColorId, painterlyBaseColor);
        }

        for (int i = 0; i < PainterlyFloatPropertyIds.Length; i++)
        {
            int propertyId = PainterlyFloatPropertyIds[i];
            if (!material.HasProperty(propertyId))
            {
                continue;
            }

            propertyBlock.SetFloat(propertyId, material.GetFloat(propertyId));
        }

        for (int i = 0; i < PainterlyTexturePropertyIds.Length; i++)
        {
            int propertyId = PainterlyTexturePropertyIds[i];
            if (!material.HasProperty(propertyId))
            {
                continue;
            }

            Texture texture = material.GetTexture(propertyId);
            if (texture != null)
            {
                propertyBlock.SetTexture(propertyId, texture);
            }
        }
    }

    private static bool MaterialSupportsMemoryMode(Material material)
    {
        return material != null && material.HasProperty(MemoryBlendId);
    }

    private static bool IsInLayerMask(int layer, LayerMask layerMask)
    {
        return (layerMask.value & (1 << layer)) != 0;
    }

    private static Color GetColor(Material material, int propertyId, Color fallback)
    {
        return material != null && material.HasProperty(propertyId) ? material.GetColor(propertyId) : fallback;
    }

    private static float GetFloat(Material material, int propertyId, float fallback)
    {
        return material != null && material.HasProperty(propertyId) ? material.GetFloat(propertyId) : fallback;
    }

    private static Vector3 GetVector(Material material, int propertyId, Vector3 fallback)
    {
        return material != null && material.HasProperty(propertyId) ? (Vector3)material.GetVector(propertyId) : fallback;
    }

    private void Log(string message, bool warning)
    {
        if (!debugLogs && !warning)
        {
            return;
        }

        if (warning)
        {
            Debug.LogWarning($"[MemoryModeShaderManager] {message}", this);
            return;
        }

        Debug.Log($"[MemoryModeShaderManager] {message}", this);
    }
}
