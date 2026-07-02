using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public class MemoryModeShaderManager : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ShadowColorId = Shader.PropertyToID("_ShadowColor");
    private static readonly int LightTintColorId = Shader.PropertyToID("_LightTintColor");
    private static readonly int AccentColorId = Shader.PropertyToID("_AccentColor");
    private static readonly int EmotionTintColorId = Shader.PropertyToID("_EmotionTintColor");
    private static readonly int RimColorId = Shader.PropertyToID("_RimColor");
    private static readonly int EmotionTintStrengthId = Shader.PropertyToID("_EmotionTintStrength");
    private static readonly int RimStrengthId = Shader.PropertyToID("_RimStrength");
    private static readonly int FlattenAmountId = Shader.PropertyToID("_FlattenAmount");
    private static readonly int ViewProjectionBlendId = Shader.PropertyToID("_ViewProjectionBlend");
    private static readonly int ViewBrushStrengthId = Shader.PropertyToID("_ViewBrushStrength");
    private static readonly int ShadowEdgeBreakStrengthId = Shader.PropertyToID("_ShadowEdgeBreakStrength");
    private static readonly int GrowthOriginId = Shader.PropertyToID("_GrowthOrigin");
    private static readonly int GrowthRadiusId = Shader.PropertyToID("_GrowthRadius");
    private static readonly int GrowthMaxRadiusId = Shader.PropertyToID("_GrowthMaxRadius");
    private static readonly int GrowthSoftnessId = Shader.PropertyToID("_GrowthSoftness");
    private static readonly int GrowthNoiseStrengthId = Shader.PropertyToID("_GrowthNoiseStrength");
    private static readonly int GrowthBlendId = Shader.PropertyToID("_GrowthBlend");
    private static readonly int RuntimeTransitionActiveId = Shader.PropertyToID("_RuntimeTransitionActive");
    private static readonly int MemoryBlendId = Shader.PropertyToID("_MemoryBlend");
    private static readonly int BumpMapId = Shader.PropertyToID("_BumpMap");
    private static readonly int BumpScaleId = Shader.PropertyToID("_BumpScale");
    private static readonly int MetallicGlossMapId = Shader.PropertyToID("_MetallicGlossMap");
    private static readonly int MetallicId = Shader.PropertyToID("_Metallic");
    private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
    private static readonly int OcclusionMapId = Shader.PropertyToID("_OcclusionMap");
    private static readonly int OcclusionStrengthId = Shader.PropertyToID("_OcclusionStrength");
    private static readonly int EmissionMapId = Shader.PropertyToID("_EmissionMap");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private static readonly int PainterlyScaleId = Shader.PropertyToID("_PainterlyScale");
    private static readonly int BrushRampTexId = Shader.PropertyToID("_BrushRampTex");
    private static readonly int BrushGrainTexId = Shader.PropertyToID("_BrushGrainTex");
    private static readonly int DryBrushTexId = Shader.PropertyToID("_DryBrushTex");
    private static readonly int WatercolorTexId = Shader.PropertyToID("_WatercolorTex");
    private static readonly int EdgeBreakTexId = Shader.PropertyToID("_EdgeBreakTex");

    private const string BaseMapPropertyName = "_BaseMap";
    private const string MainTexPropertyName = "_MainTex";
    private const string BaseColorPropertyName = "_BaseColor";
    private const string ColorPropertyName = "_Color";

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
        public Material[] originalSharedMaterials;
        public Material[] runtimeMaterials;
        public bool usingRuntimeMaterials;
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
    [SerializeField, Range(0f, 1f)] private float targetFlattenAmount = 0.6f;
    [SerializeField, Range(0f, 1f)] private float targetViewProjectionBlend = 0.35f;
    [SerializeField, Range(0f, 1f)] private float targetViewBrushStrength = 0.38f;
    [SerializeField, Range(0f, 1f)] private float targetShadowEdgeBreakStrength = 0.58f;

    [Header("Debug")]
    [SerializeField] private bool liveSyncSourceMaterial = true;
    [SerializeField] private bool debugLogs;

    private readonly List<RendererState> activeRendererStates = new List<RendererState>();
    private readonly HashSet<Renderer> rendererSet = new HashSet<Renderer>();
    private readonly Dictionary<Material, Material> prewarmedMaterialLookup = new Dictionary<Material, Material>();
    private readonly List<Material> prewarmedMaterials = new List<Material>();

    private Coroutine shaderTransitionCoroutine;
    private bool isSubscribed;
    private Vector3 activeGrowthOrigin;
    private float activeGrowthMaxRadius;
    private Material activeEmotionMaterial;
    private Material activeRestoreBaselineMaterial;
    private ShaderState currentShaderState;
    private bool hasCurrentShaderState;

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
        PrewarmRuntimeMaterialSources();
    }

    private void OnDisable()
    {
        UnsubscribeFromManager();
        StopActiveTransition();
        RestoreOriginalMaterialsImmediate();
        DestroyPrewarmedMaterialSources();
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

        CopyMaterialPropertiesToAllRuntimeMaterials(activeEmotionMaterial);
        ApplyShaderStateOverrides(currentShaderState);
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
        BeginRestoreOriginalMaterials(focusedObject);
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

        PrewarmRuntimeMaterialSources();

        activeGrowthOrigin = focusedObject.GetObservationCenter();
        Material runtimeTemplateMaterial = ResolveRuntimeTemplateMaterial(targetEmotionMaterial);

        CollectStylizableRenderers(focusedObject, runtimeTemplateMaterial);

        if (activeRendererStates.Count == 0)
        {
            Log($"No stylizable renderers found for focus '{focusedObject.name}'.", false);
            return;
        }

        activeGrowthMaxRadius = ResolveGrowthMaxRadius(activeGrowthOrigin);

        Material baselineMaterial = runtimeTemplateMaterial;
        activeEmotionMaterial = targetEmotionMaterial;
        activeRestoreBaselineMaterial = baselineMaterial;
        ShaderState fromState = ApplyGrowthTargets(ReadStateFromMaterial(baselineMaterial), activeGrowthOrigin, 0f, 0f);
        ShaderState toState = ApplyGrowthTargets(ApplyTransitionTargets(ReadStateFromMaterial(targetEmotionMaterial)), activeGrowthOrigin, activeGrowthMaxRadius, 1f);
        fromState.memoryBlend = 0f;

        CopyMaterialPropertiesToAllRuntimeMaterials(activeEmotionMaterial);
        ApplyStateToAllMaterials(fromState);
        SetAllRendererAssignments(true);
        shaderTransitionCoroutine = StartCoroutine(AnimateShaderState(fromState, toState, false));

        Log(
            $"Applied material '{targetEmotionMaterial.name}' to {activeRendererStates.Count} renderer(s) for emotion '{focusedObject.EmotionType}'. Focused item '{focusedObject.name}' kept original materials.",
            false);
    }

    private void BeginRestoreOriginalMaterials(MemoryObject focusedObject)
    {
        if (activeRendererStates.Count == 0)
        {
            return;
        }

        StopActiveTransition();

        Material sampleMaterial = GetSampleRuntimeMaterial();
        if (sampleMaterial == null)
        {
            RestoreOriginalMaterialsImmediate();
            return;
        }

        Material restoreBaseline = activeRestoreBaselineMaterial != null
            ? activeRestoreBaselineMaterial
            : (memoryPainterlyTemplate != null ? memoryPainterlyTemplate : sampleMaterial);
        ShaderState fromState = ApplyGrowthTargets(ReadStateFromMaterial(sampleMaterial), activeGrowthOrigin, activeGrowthMaxRadius, 1f);
        ShaderState toState = ApplyGrowthTargets(ReadStateFromMaterial(restoreBaseline), activeGrowthOrigin, 0f, 0f);
        fromState.memoryBlend = 1f;
        toState.memoryBlend = 0f;
        shaderTransitionCoroutine = StartCoroutine(AnimateShaderState(fromState, toState, true));
    }

    private void CollectStylizableRenderers(MemoryObject focusedObject, Material sourceMaterial)
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

            Material[] originalMaterials = renderer.sharedMaterials;
            if (originalMaterials == null || originalMaterials.Length == 0)
            {
                continue;
            }

            Material[] runtimeMaterials = new Material[originalMaterials.Length];
            Material sourceToClone = GetPrewarmedSourceMaterial(sourceMaterial);
            for (int i = 0; i < runtimeMaterials.Length; i++)
            {
                runtimeMaterials[i] = new Material(sourceToClone)
                {
                    name = $"{sourceMaterial.name} Runtime ({renderer.name} #{i})"
                };
                runtimeMaterials[i].hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            }

            activeRendererStates.Add(new RendererState
            {
                renderer = renderer,
                originalSharedMaterials = originalMaterials,
                runtimeMaterials = runtimeMaterials,
                usingRuntimeMaterials = false
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

    private ShaderState ApplyTransitionTargets(ShaderState state)
    {
        state.flattenAmount = targetFlattenAmount;
        state.viewProjectionBlend = targetViewProjectionBlend;
        state.viewBrushStrength = targetViewBrushStrength;
        state.shadowEdgeBreakStrength = targetShadowEdgeBreakStrength;
        state.memoryBlend = 1f;
        return state;
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

    private IEnumerator AnimateShaderState(ShaderState fromState, ShaderState toState, bool restoreAtEnd)
    {
        if (shaderTransitionDuration <= 0f)
        {
            ApplyStateToAllMaterials(toState);
            if (restoreAtEnd)
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
            ApplyStateToAllMaterials(blendedState);
            yield return null;
        }

        ApplyStateToAllMaterials(toState);

        if (restoreAtEnd)
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

    private void ApplyStateToAllMaterials(ShaderState state)
    {
        currentShaderState = state;
        hasCurrentShaderState = true;

        for (int i = 0; i < activeRendererStates.Count; i++)
        {
            Material[] runtimeMaterials = activeRendererStates[i].runtimeMaterials;
            if (runtimeMaterials == null)
            {
                continue;
            }

            for (int j = 0; j < runtimeMaterials.Length; j++)
            {
                Material runtimeMaterial = runtimeMaterials[j];
                if (runtimeMaterial == null)
                {
                    continue;
                }
                ApplyShaderStateOverrides(runtimeMaterial, state);
            }
        }
    }

    private void RestoreOriginalMaterialsImmediate()
    {
        for (int i = 0; i < activeRendererStates.Count; i++)
        {
            RendererState rendererState = activeRendererStates[i];
            SetRendererMaterialAssignment(rendererState, false);

            DestroyRuntimeMaterials(rendererState.runtimeMaterials);
        }

        activeRendererStates.Clear();
        activeEmotionMaterial = null;
        activeRestoreBaselineMaterial = null;
        hasCurrentShaderState = false;
    }

    private void DestroyRuntimeMaterials(Material[] runtimeMaterials)
    {
        if (runtimeMaterials == null)
        {
            return;
        }

        for (int i = 0; i < runtimeMaterials.Length; i++)
        {
            Material runtimeMaterial = runtimeMaterials[i];
            if (runtimeMaterial == null)
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Destroy(runtimeMaterial);
            }
            else
            {
                DestroyImmediate(runtimeMaterial);
            }
        }
    }

    private Material GetSampleRuntimeMaterial()
    {
        for (int i = 0; i < activeRendererStates.Count; i++)
        {
            Material[] runtimeMaterials = activeRendererStates[i].runtimeMaterials;
            if (runtimeMaterials == null)
            {
                continue;
            }

            for (int j = 0; j < runtimeMaterials.Length; j++)
            {
                if (runtimeMaterials[j] != null)
                {
                    return runtimeMaterials[j];
                }
            }
        }

        return null;
    }

    private void SetRendererMaterialAssignment(RendererState rendererState, bool useRuntimeMaterials)
    {
        if (rendererState == null || rendererState.renderer == null)
        {
            return;
        }

        rendererState.renderer.sharedMaterials = useRuntimeMaterials ? rendererState.runtimeMaterials : rendererState.originalSharedMaterials;
        rendererState.usingRuntimeMaterials = useRuntimeMaterials;
    }

    private void SetAllRendererAssignments(bool useRuntimeMaterials)
    {
        for (int i = 0; i < activeRendererStates.Count; i++)
        {
            SetRendererMaterialAssignment(activeRendererStates[i], useRuntimeMaterials);
        }
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
            if (rendererState.renderer == null)
            {
                continue;
            }

            Bounds rendererBounds = rendererState.renderer.bounds;
            float distance = Vector3.Distance(rendererBounds.center, growthOrigin) + rendererBounds.extents.magnitude;
            requiredRadius = Mathf.Max(requiredRadius, distance);
        }

        return Mathf.Max(requiredRadius, 0.1f);
    }

    private void PrewarmRuntimeMaterialSources()
    {
        DestroyPrewarmedMaterialSources();

        TryAddPrewarmedMaterial(memoryPainterlyTemplate);

        if (emotionMaterialLog == null)
        {
            return;
        }

        TryAddPrewarmedMaterial(emotionMaterialLog.FallbackMaterial);
        IReadOnlyList<EmotionMaterialLog.EmotionMaterialEntry> entries = emotionMaterialLog.Entries;
        for (int i = 0; i < entries.Count; i++)
        {
            EmotionMaterialLog.EmotionMaterialEntry entry = entries[i];
            if (entry == null)
            {
                continue;
            }

            TryAddPrewarmedMaterial(entry.memoryMaterial);
        }
    }

    private void TryAddPrewarmedMaterial(Material sourceMaterial)
    {
        if (sourceMaterial == null || prewarmedMaterialLookup.ContainsKey(sourceMaterial))
        {
            return;
        }

        Material prewarmedMaterial = new Material(sourceMaterial)
        {
            name = $"{sourceMaterial.name} Prewarmed"
        };
        prewarmedMaterial.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;

        prewarmedMaterialLookup[sourceMaterial] = prewarmedMaterial;
        prewarmedMaterials.Add(prewarmedMaterial);
    }

    private Material GetPrewarmedSourceMaterial(Material sourceMaterial)
    {
        if (sourceMaterial != null && prewarmedMaterialLookup.TryGetValue(sourceMaterial, out Material prewarmedMaterial) && prewarmedMaterial != null)
        {
            return prewarmedMaterial;
        }

        return sourceMaterial;
    }

    private void DestroyPrewarmedMaterialSources()
    {
        for (int i = 0; i < prewarmedMaterials.Count; i++)
        {
            Material prewarmedMaterial = prewarmedMaterials[i];
            if (prewarmedMaterial == null)
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Destroy(prewarmedMaterial);
            }
            else
            {
                DestroyImmediate(prewarmedMaterial);
            }
        }

        prewarmedMaterials.Clear();
        prewarmedMaterialLookup.Clear();
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

    private void CopyMaterialPropertiesToAllRuntimeMaterials(Material sourceMaterial)
    {
        if (sourceMaterial == null)
        {
            return;
        }

        for (int i = 0; i < activeRendererStates.Count; i++)
        {
            Material[] runtimeMaterials = activeRendererStates[i].runtimeMaterials;
            if (runtimeMaterials == null)
            {
                continue;
            }

            for (int j = 0; j < runtimeMaterials.Length; j++)
            {
                Material runtimeMaterial = runtimeMaterials[j];
                if (runtimeMaterial == null)
                {
                    continue;
                }

                runtimeMaterial.CopyPropertiesFromMaterial(sourceMaterial);
                Material originalMaterial = GetOriginalMaterialForSlot(activeRendererStates[i], j);
                CopyPreservedItemProperties(originalMaterial, runtimeMaterial);
            }
        }
    }

    private void ApplyShaderStateOverrides(ShaderState state)
    {
        for (int i = 0; i < activeRendererStates.Count; i++)
        {
            Material[] runtimeMaterials = activeRendererStates[i].runtimeMaterials;
            if (runtimeMaterials == null)
            {
                continue;
            }

            for (int j = 0; j < runtimeMaterials.Length; j++)
            {
                ApplyShaderStateOverrides(runtimeMaterials[j], state);
            }
        }
    }

    private static void ApplyShaderStateOverrides(Material runtimeMaterial, ShaderState state)
    {
        if (runtimeMaterial == null)
        {
            return;
        }

        SetColorIfPresent(runtimeMaterial, ShadowColorId, state.shadowColor);
        SetColorIfPresent(runtimeMaterial, LightTintColorId, state.lightTintColor);
        SetColorIfPresent(runtimeMaterial, AccentColorId, state.accentColor);
        SetColorIfPresent(runtimeMaterial, EmotionTintColorId, state.emotionTintColor);
        SetColorIfPresent(runtimeMaterial, RimColorId, state.rimColor);
        SetFloatIfPresent(runtimeMaterial, EmotionTintStrengthId, state.emotionTintStrength);
        SetFloatIfPresent(runtimeMaterial, RimStrengthId, state.rimStrength);
        SetFloatIfPresent(runtimeMaterial, FlattenAmountId, state.flattenAmount);
        SetFloatIfPresent(runtimeMaterial, ViewProjectionBlendId, state.viewProjectionBlend);
        SetFloatIfPresent(runtimeMaterial, ViewBrushStrengthId, state.viewBrushStrength);
        SetFloatIfPresent(runtimeMaterial, ShadowEdgeBreakStrengthId, state.shadowEdgeBreakStrength);
        SetFloatIfPresent(runtimeMaterial, MemoryBlendId, state.memoryBlend);
        SetVectorIfPresent(runtimeMaterial, GrowthOriginId, state.growthOrigin);
        SetFloatIfPresent(runtimeMaterial, GrowthRadiusId, state.growthRadius);
        SetFloatIfPresent(runtimeMaterial, GrowthMaxRadiusId, state.growthMaxRadius);
        SetFloatIfPresent(runtimeMaterial, GrowthSoftnessId, state.growthSoftness);
        SetFloatIfPresent(runtimeMaterial, GrowthNoiseStrengthId, state.growthNoiseStrength);
        SetFloatIfPresent(runtimeMaterial, GrowthBlendId, state.growthBlend);
        SetFloatIfPresent(runtimeMaterial, RuntimeTransitionActiveId, 1f);

        if (!runtimeMaterial.HasProperty(MemoryBlendId))
        {
            SetColorIfPresent(runtimeMaterial, BaseColorId, state.baseColor);
        }
    }

    private Material ResolveRuntimeTemplateMaterial(Material targetEmotionMaterial)
    {
        if (targetEmotionMaterial != null)
        {
            return targetEmotionMaterial;
        }

        return memoryPainterlyTemplate;
    }

    private static Material GetOriginalMaterialForSlot(RendererState rendererState, int slotIndex)
    {
        if (rendererState == null || rendererState.originalSharedMaterials == null || rendererState.originalSharedMaterials.Length == 0)
        {
            return null;
        }

        if (slotIndex >= 0 && slotIndex < rendererState.originalSharedMaterials.Length)
        {
            return rendererState.originalSharedMaterials[slotIndex];
        }

        return rendererState.originalSharedMaterials[0];
    }

    private static void CopyPreservedItemProperties(Material originalMaterial, Material runtimeMaterial)
    {
        if (originalMaterial == null || runtimeMaterial == null)
        {
            return;
        }

        CopyBaseMapWithTransform(originalMaterial, runtimeMaterial);
        CopyBaseColor(originalMaterial, runtimeMaterial);
        CopyTextureIfPresent(originalMaterial, runtimeMaterial, BumpMapId);
        CopyFloatIfPresent(originalMaterial, runtimeMaterial, BumpScaleId);
        CopyTextureIfPresent(originalMaterial, runtimeMaterial, MetallicGlossMapId);
        CopyFloatIfPresent(originalMaterial, runtimeMaterial, MetallicId);
        CopyFloatIfPresent(originalMaterial, runtimeMaterial, SmoothnessId);
        CopyTextureIfPresent(originalMaterial, runtimeMaterial, OcclusionMapId);
        CopyFloatIfPresent(originalMaterial, runtimeMaterial, OcclusionStrengthId);
        CopyTextureIfPresent(originalMaterial, runtimeMaterial, EmissionMapId);
        CopyColorIfPresent(originalMaterial, runtimeMaterial, EmissionColorId);
        CopyFloatIfPresent(originalMaterial, runtimeMaterial, PainterlyScaleId);
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

    private static void SetColorIfPresent(Material material, int propertyId, Color value)
    {
        if (material != null && material.HasProperty(propertyId))
        {
            material.SetColor(propertyId, value);
        }
    }

    private static void SetFloatIfPresent(Material material, int propertyId, float value)
    {
        if (material != null && material.HasProperty(propertyId))
        {
            material.SetFloat(propertyId, value);
        }
    }

    private static void SetVectorIfPresent(Material material, int propertyId, Vector3 value)
    {
        if (material != null && material.HasProperty(propertyId))
        {
            material.SetVector(propertyId, value);
        }
    }

    private static void CopyTextureIfPresent(Material source, Material target, int propertyId)
    {
        if (source == null || target == null || !source.HasProperty(propertyId) || !target.HasProperty(propertyId))
        {
            return;
        }

        target.SetTexture(propertyId, source.GetTexture(propertyId));
    }

    private static void CopyFloatIfPresent(Material source, Material target, int propertyId)
    {
        if (source == null || target == null || !source.HasProperty(propertyId) || !target.HasProperty(propertyId))
        {
            return;
        }

        target.SetFloat(propertyId, source.GetFloat(propertyId));
    }

    private static void CopyColorIfPresent(Material source, Material target, int propertyId)
    {
        if (source == null || target == null || !source.HasProperty(propertyId) || !target.HasProperty(propertyId))
        {
            return;
        }

        target.SetColor(propertyId, source.GetColor(propertyId));
    }

    private static void CopyBaseMapWithTransform(Material source, Material target)
    {
        if (source == null || target == null || !target.HasProperty(BaseMapPropertyName))
        {
            return;
        }

        string sourceTextureProperty = source.HasProperty(BaseMapPropertyName)
            ? BaseMapPropertyName
            : (source.HasProperty(MainTexPropertyName) ? MainTexPropertyName : null);

        if (string.IsNullOrEmpty(sourceTextureProperty))
        {
            return;
        }

        target.SetTexture(BaseMapPropertyName, source.GetTexture(sourceTextureProperty));
        target.SetTextureScale(BaseMapPropertyName, source.GetTextureScale(sourceTextureProperty));
        target.SetTextureOffset(BaseMapPropertyName, source.GetTextureOffset(sourceTextureProperty));
    }

    private static void CopyBaseColor(Material source, Material target)
    {
        if (source == null || target == null || !target.HasProperty(BaseColorId))
        {
            return;
        }

        if (source.HasProperty(BaseColorPropertyName))
        {
            target.SetColor(BaseColorId, source.GetColor(BaseColorId));
            return;
        }

        if (source.HasProperty(ColorPropertyName))
        {
            target.SetColor(BaseColorId, source.GetColor(ColorPropertyName));
        }
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
        }
        else
        {
            Debug.Log($"[MemoryModeShaderManager] {message}", this);
        }
    }
}

[DisallowMultipleComponent]
public sealed class MemoryShaderExclude : MonoBehaviour
{
}
