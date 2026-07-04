using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

public class MemoryModeLightingController : MonoBehaviour
{
    private const string SpotlightObjectName = "MemoryModeSpotlight";
    private const string FillLightObjectName = "MemoryModeFillLight";

    [Header("Transition")]
    [FormerlySerializedAs("transitionDuration")]
    [SerializeField] private float memoryTransitionDuration = 0.3f;

    [Header("Memory Mode Visuals")]
    [FormerlySerializedAs("mainDirectionalLight")]
    [SerializeField] private Light targetDirectionalLight;
    [FormerlySerializedAs("dimmedMainLightIntensity")]
    [SerializeField] private float memoryLightIntensity = 0.15f;
    [FormerlySerializedAs("globalVolume")]
    [SerializeField] private Volume targetGlobalVolume;
    [SerializeField] private float memoryVolumeWeight = 1f;

    [Header("Environment Dimming")]
    [SerializeField] private float dimmedAmbientIntensity = 0.2f;
    [SerializeField] private Material skyboxMaterial;
    [SerializeField] private float dimmedSkyboxExposure = 0.2f;
    [SerializeField] private float dimmedPostExposure = -2.0f;

    [Header("Memory Spotlight")]
    [SerializeField] private Light memorySpotlight;
    [SerializeField] private float spotlightIntensity = 8f;
    [SerializeField] private float spotlightRange = 4f;
    [SerializeField] private float spotlightAngle = 35f;
    [SerializeField] private float spotlightHeight = 1.2f;
    [SerializeField] private float spotlightForwardOffset = 0.2f;

    [Header("Memory Fill Light")]
    [SerializeField] private Light memoryFillLight;
    [SerializeField] private float fillLightIntensity = 1.6f;
    [SerializeField] private float fillLightRange = 4.5f;
    [SerializeField] private float fillLightAngle = 55f;
    [SerializeField] private float fillLightHeight = 0.2f;
    [SerializeField] private float fillLightForwardOffset = 0.75f;

    [Header("Spotlight Layer Mask")]
    [SerializeField] private string spotlitLayerName = "MemorySpotlit";
    [SerializeField] private bool useSpotlightLayerMask = true;

    private const string SkyboxExposureProperty = "_Exposure";

    private readonly Dictionary<Transform, int> originalLayersByTransform = new Dictionary<Transform, int>();
    private readonly HashSet<string> warningKeys = new HashSet<string>();

    private MemoryObject currentTarget;
    private Material activeSkyboxMaterial;
    private Coroutine lightingTransitionCoroutine;
    private bool isLightingModeActive;
    private bool hasCachedEnvironmentState;
    private bool hasCachedVolumeState;
    private bool hasCachedSpotlightState;
    private bool hasCachedFillLightState;
    private bool hasCachedPostExposureState;
    private float originalMemoryLightIntensity;
    private float originalVolumeWeight;
    private float originalAmbientIntensity;
    private float originalSkyboxExposure;
    private float originalPostExposure;
    private bool originalVolumeEnabled;
    private SpotlightState originalSpotlightState;
    private SpotlightState originalFillLightState;
    private VolumeComponent cachedColorAdjustmentsComponent;
    private FieldInfo cachedPostExposureField;
    private FieldInfo cachedParameterValueField;

    private struct SpotlightState
    {
        public bool enabled;
        public LightType type;
        public float intensity;
        public float range;
        public float spotAngle;
        public int cullingMask;
        public LightShadows shadows;
        public Vector3 position;
        public Quaternion rotation;
    }

    public void EnterLightingMode(MemoryObject target)
    {
        if (target == null)
        {
            Debug.LogWarning("[MemoryModeLightingController] EnterLightingMode called with a null MemoryObject.", this);
            return;
        }

        ResolveOptionalReferences();
        EnsureSpotlightExists();
        EnsureFillLightExists();

        if (!hasCachedEnvironmentState)
        {
            CacheEnvironmentState();
        }

        if (!hasCachedSpotlightState)
        {
            CacheSpotlightState();
        }

        if (!hasCachedFillLightState)
        {
            CacheFillLightState();
        }

        StopLightingTransition();

        if (currentTarget != null && currentTarget != target)
        {
            RestoreOriginalLayers();
        }

        currentTarget = target;
        isLightingModeActive = true;

        ConfigureSpotlightForMemoryMode();
        ConfigureFillLightForMemoryMode();
        ApplyTargetSpotlightLayer(target);
        UpdateActiveLightTransforms();
        lightingTransitionCoroutine = StartCoroutine(TransitionLightingState(true));
    }

    public void ExitLightingMode()
    {
        RestoreOriginalLayers();
        currentTarget = null;
        isLightingModeActive = false;

        if (!hasCachedEnvironmentState && !hasCachedSpotlightState && !hasCachedFillLightState)
        {
            return;
        }

        StopLightingTransition();
        lightingTransitionCoroutine = StartCoroutine(TransitionLightingState(false));
    }

    private void LateUpdate()
    {
        if (!isLightingModeActive || currentTarget == null)
        {
            return;
        }

        UpdateActiveLightTransforms();
    }

    private void OnDisable()
    {
        StopLightingTransition();
        RestoreOriginalLayers();
        RestoreEnvironmentState();
        RestoreSpotlightState();
        RestoreFillLightState();
        ClearCachedState();
        currentTarget = null;
        isLightingModeActive = false;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ResolveOptionalReferences();
        TryResolveManagedLights();
    }
#endif

    private void ResolveOptionalReferences()
    {
        if (targetDirectionalLight == null && RenderSettings.sun != null)
        {
            targetDirectionalLight = RenderSettings.sun;
        }

        if (skyboxMaterial == null && RenderSettings.skybox != null)
        {
            skyboxMaterial = RenderSettings.skybox;
        }

        TryResolveManagedLights();
    }

    private void CacheEnvironmentState()
    {
        originalAmbientIntensity = RenderSettings.ambientIntensity;

        if (targetDirectionalLight != null)
        {
            originalMemoryLightIntensity = targetDirectionalLight.intensity;
        }
        else
        {
            WarnOnce("MissingMainDirectionalLight",
                "[MemoryModeLightingController] Target directional light is not assigned. Main light dimming will be skipped.");
        }

        if (targetGlobalVolume != null)
        {
            originalVolumeEnabled = targetGlobalVolume.enabled;
            originalVolumeWeight = targetGlobalVolume.weight;
            hasCachedVolumeState = true;
        }
        else
        {
            hasCachedVolumeState = false;
            WarnOnce("MissingTargetGlobalVolume",
                "[MemoryModeLightingController] Target Global Volume is not assigned. Volume weight blending will be skipped.");
        }

        activeSkyboxMaterial = skyboxMaterial;
        if (activeSkyboxMaterial != null)
        {
            if (activeSkyboxMaterial.HasProperty(SkyboxExposureProperty))
            {
                originalSkyboxExposure = activeSkyboxMaterial.GetFloat(SkyboxExposureProperty);
            }
            else
            {
                WarnOnce("MissingSkyboxExposureProperty",
                    "[MemoryModeLightingController] Skybox material does not expose _Exposure. Skybox dimming will be skipped.");
                activeSkyboxMaterial = null;
            }
        }
        else
        {
            WarnOnce("MissingSkyboxMaterial",
                "[MemoryModeLightingController] Skybox material is not assigned. Skybox exposure dimming will be skipped.");
        }

        if (TryReadPostExposure(out float currentPostExposure))
        {
            originalPostExposure = currentPostExposure;
            hasCachedPostExposureState = true;
        }
        else
        {
            hasCachedPostExposureState = false;
        }

        hasCachedEnvironmentState = true;
    }

    private void CacheSpotlightState()
    {
        if (memorySpotlight == null)
        {
            return;
        }

        Transform spotlightTransform = memorySpotlight.transform;
        originalSpotlightState = new SpotlightState
        {
            enabled = memorySpotlight.enabled,
            type = memorySpotlight.type,
            intensity = memorySpotlight.intensity,
            range = memorySpotlight.range,
            spotAngle = memorySpotlight.spotAngle,
            cullingMask = memorySpotlight.cullingMask,
            shadows = memorySpotlight.shadows,
            position = spotlightTransform.position,
            rotation = spotlightTransform.rotation
        };

        hasCachedSpotlightState = true;
    }

    private void CacheFillLightState()
    {
        if (memoryFillLight == null)
        {
            return;
        }

        Transform fillLightTransform = memoryFillLight.transform;
        originalFillLightState = new SpotlightState
        {
            enabled = memoryFillLight.enabled,
            type = memoryFillLight.type,
            intensity = memoryFillLight.intensity,
            range = memoryFillLight.range,
            spotAngle = memoryFillLight.spotAngle,
            cullingMask = memoryFillLight.cullingMask,
            shadows = memoryFillLight.shadows,
            position = fillLightTransform.position,
            rotation = fillLightTransform.rotation
        };

        hasCachedFillLightState = true;
    }

    private void EnsureSpotlightExists()
    {
        if (memorySpotlight == null)
        {
            memorySpotlight = FindManagedLight(SpotlightObjectName);
        }

        if (memorySpotlight != null)
        {
            return;
        }

        GameObject spotlightObject = new GameObject(SpotlightObjectName);
        spotlightObject.transform.SetParent(transform, false);

        memorySpotlight = spotlightObject.AddComponent<Light>();
        memorySpotlight.type = LightType.Spot;
        memorySpotlight.shadows = LightShadows.Soft;
        memorySpotlight.enabled = false;
    }

    private void EnsureFillLightExists()
    {
        if (memoryFillLight == null)
        {
            memoryFillLight = FindManagedLight(FillLightObjectName);
        }

        if (memoryFillLight != null)
        {
            return;
        }

        GameObject fillLightObject = new GameObject(FillLightObjectName);
        fillLightObject.transform.SetParent(transform, false);

        memoryFillLight = fillLightObject.AddComponent<Light>();
        memoryFillLight.type = LightType.Spot;
        memoryFillLight.shadows = LightShadows.None;
        memoryFillLight.enabled = false;
    }

    private void ConfigureSpotlightForMemoryMode()
    {
        if (memorySpotlight == null)
        {
            WarnOnce("MissingSpotlight",
                "[MemoryModeLightingController] Memory spotlight is unavailable. Spotlight targeting will be skipped.");
            return;
        }

        memorySpotlight.type = LightType.Spot;
        memorySpotlight.range = spotlightRange;
        memorySpotlight.spotAngle = spotlightAngle;
        memorySpotlight.shadows = LightShadows.Soft;
        memorySpotlight.intensity = 0f;
        memorySpotlight.enabled = false;
    }

    private void ConfigureFillLightForMemoryMode()
    {
        if (memoryFillLight == null)
        {
            WarnOnce("MissingFillLight",
                "[MemoryModeLightingController] Memory fill light is unavailable. Fill light targeting will be skipped.");
            return;
        }

        memoryFillLight.type = LightType.Spot;
        memoryFillLight.range = fillLightRange;
        memoryFillLight.spotAngle = fillLightAngle;
        memoryFillLight.shadows = LightShadows.None;
        memoryFillLight.intensity = 0f;
        memoryFillLight.enabled = false;
    }

    private void TryResolveManagedLights()
    {
        if (memorySpotlight == null)
        {
            memorySpotlight = FindManagedLight(SpotlightObjectName);
        }

        if (memoryFillLight == null)
        {
            memoryFillLight = FindManagedLight(FillLightObjectName);
        }
    }

    private Light FindManagedLight(string objectName)
    {
        Transform child = transform.Find(objectName);
        if (child != null)
        {
            Light childLight = child.GetComponent<Light>();
            if (childLight != null)
            {
                return childLight;
            }
        }

        Light[] childLights = GetComponentsInChildren<Light>(true);
        for (int i = 0; i < childLights.Length; i++)
        {
            Light childLight = childLights[i];
            if (childLight != null && childLight.name == objectName)
            {
                return childLight;
            }
        }

        return null;
    }

    private void UpdateActiveLightTransforms()
    {
        if (currentTarget == null)
        {
            return;
        }

        Vector3 targetCenter = GetTargetCenter(currentTarget);
        Camera activeCamera = Camera.main;

        if (activeCamera != null)
        {
            Vector3 horizontalCameraForward = Vector3.ProjectOnPlane(activeCamera.transform.forward, Vector3.up);
            if (horizontalCameraForward.sqrMagnitude <= Mathf.Epsilon)
            {
                horizontalCameraForward = Vector3.ProjectOnPlane(targetCenter - activeCamera.transform.position, Vector3.up);
            }

            if (horizontalCameraForward.sqrMagnitude <= Mathf.Epsilon)
            {
                horizontalCameraForward = Vector3.forward;
            }

            Vector3 horizontalForward = horizontalCameraForward.normalized;
            UpdateLightTransform(memorySpotlight, targetCenter, spotlightHeight, spotlightForwardOffset, horizontalForward);
            UpdateLightTransform(memoryFillLight, targetCenter, fillLightHeight, fillLightForwardOffset, horizontalForward);
        }
        else
        {
            WarnOnce("MissingMainCamera",
                "[MemoryModeLightingController] Camera.main was not found. Spotlight will use a top-down fallback.");
            UpdateTopDownLightTransform(memorySpotlight, targetCenter, spotlightHeight);
            UpdateTopDownLightTransform(memoryFillLight, targetCenter, fillLightHeight);
        }
    }

    private void UpdateLightTransform(
        Light targetLight,
        Vector3 targetCenter,
        float height,
        float forwardOffset,
        Vector3 horizontalCameraForward)
    {
        if (targetLight == null)
        {
            return;
        }

        Transform lightTransform = targetLight.transform;
        Vector3 lightPosition =
            targetCenter +
            (Vector3.up * height) -
            (horizontalCameraForward * forwardOffset);

        lightTransform.position = lightPosition;

        Vector3 directionToTarget = targetCenter - lightPosition;
        if (directionToTarget.sqrMagnitude > Mathf.Epsilon)
        {
            lightTransform.rotation = Quaternion.LookRotation(directionToTarget.normalized, Vector3.up);
        }
    }

    private void UpdateTopDownLightTransform(Light targetLight, Vector3 targetCenter, float height)
    {
        if (targetLight == null)
        {
            return;
        }

        targetLight.transform.position = targetCenter + (Vector3.up * height);
        targetLight.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
    }

    private IEnumerator TransitionLightingState(bool enteringLightingMode)
    {
        float duration = Mathf.Max(0f, memoryTransitionDuration);
        float startingMainLightIntensity = targetDirectionalLight != null ? targetDirectionalLight.intensity : 0f;
        float startingVolumeWeight = targetGlobalVolume != null ? targetGlobalVolume.weight : 0f;
        float startingAmbientIntensity = RenderSettings.ambientIntensity;
        float startingSkyboxExposure = activeSkyboxMaterial != null
            ? activeSkyboxMaterial.GetFloat(SkyboxExposureProperty)
            : 0f;
        float startingSpotlightIntensity = enteringLightingMode
            ? 0f
            : (memorySpotlight != null && memorySpotlight.enabled ? memorySpotlight.intensity : 0f);
        float startingFillLightIntensity = enteringLightingMode
            ? 0f
            : (memoryFillLight != null && memoryFillLight.enabled ? memoryFillLight.intensity : 0f);

        bool canBlendPostExposure = TryReadPostExposure(out float startingPostExposure);

        float targetMainLightIntensity = enteringLightingMode ? memoryLightIntensity : originalMemoryLightIntensity;
        float targetVolumeWeight = enteringLightingMode ? memoryVolumeWeight : originalVolumeWeight;
        float targetAmbientIntensity = enteringLightingMode ? dimmedAmbientIntensity : originalAmbientIntensity;
        float targetSkyboxExposure = enteringLightingMode ? dimmedSkyboxExposure : originalSkyboxExposure;
        float targetPostExposure = enteringLightingMode ? dimmedPostExposure : originalPostExposure;
        float targetSpotlightIntensity = enteringLightingMode
            ? spotlightIntensity
            : (hasCachedSpotlightState && originalSpotlightState.enabled ? originalSpotlightState.intensity : 0f);
        float targetFillLightIntensity = enteringLightingMode
            ? fillLightIntensity
            : (hasCachedFillLightState && originalFillLightState.enabled ? originalFillLightState.intensity : 0f);

        if (targetGlobalVolume != null)
        {
            targetGlobalVolume.enabled = true;
        }

        if (memorySpotlight != null)
        {
            memorySpotlight.intensity = startingSpotlightIntensity;
            memorySpotlight.enabled = true;
        }

        if (memoryFillLight != null)
        {
            memoryFillLight.intensity = startingFillLightIntensity;
            memoryFillLight.enabled = true;
        }

        if (duration <= Mathf.Epsilon)
        {
            ApplyLightingState(
                targetMainLightIntensity,
                targetVolumeWeight,
                targetAmbientIntensity,
                targetSkyboxExposure,
                canBlendPostExposure,
                targetPostExposure,
                targetSpotlightIntensity,
                targetFillLightIntensity);
            FinalizeTransition(enteringLightingMode);
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            ApplyLightingState(
                Mathf.Lerp(startingMainLightIntensity, targetMainLightIntensity, t),
                Mathf.Lerp(startingVolumeWeight, targetVolumeWeight, t),
                Mathf.Lerp(startingAmbientIntensity, targetAmbientIntensity, t),
                Mathf.Lerp(startingSkyboxExposure, targetSkyboxExposure, t),
                canBlendPostExposure,
                Mathf.Lerp(startingPostExposure, targetPostExposure, t),
                Mathf.Lerp(startingSpotlightIntensity, targetSpotlightIntensity, t),
                Mathf.Lerp(startingFillLightIntensity, targetFillLightIntensity, t));

            yield return null;
        }

        ApplyLightingState(
            targetMainLightIntensity,
            targetVolumeWeight,
            targetAmbientIntensity,
            targetSkyboxExposure,
            canBlendPostExposure,
            targetPostExposure,
            targetSpotlightIntensity,
            targetFillLightIntensity);

        FinalizeTransition(enteringLightingMode);
    }

    private void ApplyLightingState(
        float mainLightIntensity,
        float volumeWeight,
        float ambientIntensity,
        float skyboxExposure,
        bool applyPostExposure,
        float postExposure,
        float spotlightTargetIntensity,
        float fillLightTargetIntensity)
    {
        if (targetDirectionalLight != null)
        {
            targetDirectionalLight.intensity = mainLightIntensity;
        }

        if (targetGlobalVolume != null)
        {
            targetGlobalVolume.weight = volumeWeight;
        }

        RenderSettings.ambientIntensity = ambientIntensity;

        if (activeSkyboxMaterial != null)
        {
            activeSkyboxMaterial.SetFloat(SkyboxExposureProperty, skyboxExposure);
        }

        if (applyPostExposure)
        {
            TryWritePostExposure(postExposure);
        }

        if (memorySpotlight != null)
        {
            memorySpotlight.type = LightType.Spot;
            memorySpotlight.range = spotlightRange;
            memorySpotlight.spotAngle = spotlightAngle;
            memorySpotlight.intensity = spotlightTargetIntensity;
        }

        if (memoryFillLight != null)
        {
            memoryFillLight.type = LightType.Spot;
            memoryFillLight.range = fillLightRange;
            memoryFillLight.spotAngle = fillLightAngle;
            memoryFillLight.shadows = LightShadows.None;
            memoryFillLight.intensity = fillLightTargetIntensity;
        }
    }

    private void FinalizeTransition(bool enteringLightingMode)
    {
        lightingTransitionCoroutine = null;

        if (enteringLightingMode)
        {
            if (memorySpotlight != null)
            {
                memorySpotlight.enabled = true;
                memorySpotlight.intensity = spotlightIntensity;
            }

            if (memoryFillLight != null)
            {
                memoryFillLight.enabled = true;
                memoryFillLight.intensity = fillLightIntensity;
            }

            return;
        }

        RestoreEnvironmentState();
        RestoreSpotlightState();
        RestoreFillLightState();
        ClearCachedState();
    }

    private void ApplyTargetSpotlightLayer(MemoryObject target)
    {
        if (memorySpotlight == null && memoryFillLight == null)
        {
            return;
        }

        if (!useSpotlightLayerMask)
        {
            if (memorySpotlight != null)
            {
                memorySpotlight.cullingMask = ~0;
            }

            if (memoryFillLight != null)
            {
                memoryFillLight.cullingMask = ~0;
            }
            return;
        }

        int spotlitLayer = LayerMask.NameToLayer(spotlitLayerName);
        if (spotlitLayer < 0)
        {
            WarnOnce("MissingSpotlitLayer",
                $"[MemoryModeLightingController] Layer '{spotlitLayerName}' does not exist. Create it in Tags and Layers to isolate the spotlight.");
            if (memorySpotlight != null)
            {
                memorySpotlight.cullingMask = ~0;
            }

            if (memoryFillLight != null)
            {
                memoryFillLight.cullingMask = ~0;
            }
            return;
        }

        originalLayersByTransform.Clear();
        Transform[] targetTransforms = target.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < targetTransforms.Length; i++)
        {
            Transform targetTransform = targetTransforms[i];
            originalLayersByTransform[targetTransform] = targetTransform.gameObject.layer;
            targetTransform.gameObject.layer = spotlitLayer;
        }

        if (memorySpotlight != null)
        {
            memorySpotlight.cullingMask = 1 << spotlitLayer;
        }

        if (memoryFillLight != null)
        {
            memoryFillLight.cullingMask = 1 << spotlitLayer;
        }
    }

    private void RestoreOriginalLayers()
    {
        if (originalLayersByTransform.Count == 0)
        {
            return;
        }

        foreach (KeyValuePair<Transform, int> entry in originalLayersByTransform)
        {
            if (entry.Key == null)
            {
                continue;
            }

            entry.Key.gameObject.layer = entry.Value;
        }

        originalLayersByTransform.Clear();
    }

    private void RestoreEnvironmentState()
    {
        if (!hasCachedEnvironmentState)
        {
            return;
        }

        if (targetDirectionalLight != null)
        {
            targetDirectionalLight.intensity = originalMemoryLightIntensity;
        }

        if (targetGlobalVolume != null && hasCachedVolumeState)
        {
            targetGlobalVolume.weight = originalVolumeWeight;
            targetGlobalVolume.enabled = originalVolumeEnabled;
        }

        RenderSettings.ambientIntensity = originalAmbientIntensity;

        if (activeSkyboxMaterial != null)
        {
            activeSkyboxMaterial.SetFloat(SkyboxExposureProperty, originalSkyboxExposure);
        }

        if (hasCachedPostExposureState)
        {
            TryWritePostExposure(originalPostExposure);
        }
    }

    private void RestoreSpotlightState()
    {
        if (memorySpotlight == null)
        {
            return;
        }

        if (!hasCachedSpotlightState)
        {
            memorySpotlight.enabled = false;
            return;
        }

        memorySpotlight.enabled = originalSpotlightState.enabled;
        memorySpotlight.type = originalSpotlightState.type;
        memorySpotlight.intensity = originalSpotlightState.intensity;
        memorySpotlight.range = originalSpotlightState.range;
        memorySpotlight.spotAngle = originalSpotlightState.spotAngle;
        memorySpotlight.cullingMask = originalSpotlightState.cullingMask;
        memorySpotlight.shadows = originalSpotlightState.shadows;
        memorySpotlight.transform.position = originalSpotlightState.position;
        memorySpotlight.transform.rotation = originalSpotlightState.rotation;
    }

    private void RestoreFillLightState()
    {
        if (memoryFillLight == null)
        {
            return;
        }

        if (!hasCachedFillLightState)
        {
            memoryFillLight.enabled = false;
            return;
        }

        memoryFillLight.enabled = originalFillLightState.enabled;
        memoryFillLight.type = originalFillLightState.type;
        memoryFillLight.intensity = originalFillLightState.intensity;
        memoryFillLight.range = originalFillLightState.range;
        memoryFillLight.spotAngle = originalFillLightState.spotAngle;
        memoryFillLight.cullingMask = originalFillLightState.cullingMask;
        memoryFillLight.shadows = originalFillLightState.shadows;
        memoryFillLight.transform.position = originalFillLightState.position;
        memoryFillLight.transform.rotation = originalFillLightState.rotation;
    }

    private void ClearCachedState()
    {
        hasCachedEnvironmentState = false;
        hasCachedVolumeState = false;
        hasCachedSpotlightState = false;
        hasCachedFillLightState = false;
        hasCachedPostExposureState = false;
        activeSkyboxMaterial = null;
        cachedColorAdjustmentsComponent = null;
        cachedPostExposureField = null;
        cachedParameterValueField = null;
    }

    private void StopLightingTransition()
    {
        if (lightingTransitionCoroutine == null)
        {
            return;
        }

        StopCoroutine(lightingTransitionCoroutine);
        lightingTransitionCoroutine = null;
    }

    private Vector3 GetTargetCenter(MemoryObject target)
    {
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        Bounds combinedBounds = new Bounds(target.transform.position, Vector3.zero);
        bool hasBounds = false;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer currentRenderer = renderers[i];
            if (currentRenderer == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                combinedBounds = currentRenderer.bounds;
                hasBounds = true;
                continue;
            }

            combinedBounds.Encapsulate(currentRenderer.bounds);
        }

        if (hasBounds)
        {
            return combinedBounds.center;
        }

        WarnOnce("MissingRendererBounds",
            $"[MemoryModeLightingController] No Renderer bounds found on {target.name}. Spotlight will target the transform position.");
        return target.transform.position;
    }

    private bool TryReadPostExposure(out float postExposureValue)
    {
        postExposureValue = 0f;

        if (!TryResolvePostExposureParameter(out object postExposureParameter))
        {
            return false;
        }

        postExposureValue = (float)cachedParameterValueField.GetValue(postExposureParameter);
        return true;
    }

    private bool TryWritePostExposure(float value)
    {
        if (!TryResolvePostExposureParameter(out object postExposureParameter))
        {
            return false;
        }

        cachedParameterValueField.SetValue(postExposureParameter, value);
        return true;
    }

    private bool TryResolvePostExposureParameter(out object postExposureParameter)
    {
        postExposureParameter = null;

        if (targetGlobalVolume == null)
        {
            WarnOnce("MissingGlobalVolume",
                "[MemoryModeLightingController] Target Global Volume is not assigned. Post Exposure dimming will be skipped.");
            return false;
        }

        if (targetGlobalVolume.profile == null)
        {
            WarnOnce("MissingVolumeProfile",
                "[MemoryModeLightingController] The assigned Target Global Volume has no profile. Post Exposure dimming will be skipped.");
            return false;
        }

        if (cachedColorAdjustmentsComponent == null || cachedPostExposureField == null || cachedParameterValueField == null)
        {
            IList<VolumeComponent> components = targetGlobalVolume.profile.components;
            for (int i = 0; i < components.Count; i++)
            {
                VolumeComponent component = components[i];
                if (component == null || component.GetType().Name != "ColorAdjustments")
                {
                    continue;
                }

                FieldInfo postExposureField = component.GetType().GetField("postExposure", BindingFlags.Instance | BindingFlags.Public);
                if (postExposureField == null)
                {
                    continue;
                }

                object parameter = postExposureField.GetValue(component);
                if (parameter == null)
                {
                    continue;
                }

                FieldInfo valueField = parameter.GetType().GetField("value", BindingFlags.Instance | BindingFlags.Public);
                if (valueField == null || valueField.FieldType != typeof(float))
                {
                    continue;
                }

                cachedColorAdjustmentsComponent = component;
                cachedPostExposureField = postExposureField;
                cachedParameterValueField = valueField;
                break;
            }
        }

        if (cachedColorAdjustmentsComponent == null || cachedPostExposureField == null || cachedParameterValueField == null)
        {
            WarnOnce("MissingColorAdjustments",
                "[MemoryModeLightingController] No ColorAdjustments override with a postExposure value was found on the assigned Global Volume.");
            return false;
        }

        postExposureParameter = cachedPostExposureField.GetValue(cachedColorAdjustmentsComponent);
        return postExposureParameter != null;
    }

    private void WarnOnce(string key, string message)
    {
        if (!warningKeys.Add(key))
        {
            return;
        }

        Debug.LogWarning(message, this);
    }
}
