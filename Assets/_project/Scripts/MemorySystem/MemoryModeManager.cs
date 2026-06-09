using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public class MemoryModeManager : MonoBehaviour
{
    public static MemoryModeManager Instance { get; private set; }

    [Header("Memory Mode Visuals")]
    public Light targetDirectionalLight;
    public float memoryLightIntensity = 0.3f;
    public Volume targetGlobalVolume;
    public float memoryVolumeWeight = 1f;
    public float memoryTransitionDuration = 0.3f;

    public bool IsInMemoryMode { get; private set; }
    public MemoryObject CurrentMemoryObject => currentMemoryObject;

    private MemoryObject currentMemoryObject;
    private Coroutine visualTransitionCoroutine;
    private float originalLightIntensity;
    private float originalVolumeWeight;
    private bool originalVolumeEnabled;
    private bool hasCachedLightState;
    private bool hasCachedVolumeState;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[MemoryModeManager] Duplicate instance found. Destroying the new one.", this);
            Destroy(gameObject);
            return;
        }

        Instance = this;
        CacheOriginalVisualState();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void EnterMemoryMode(MemoryObject memoryObject)
    {
        if (memoryObject == null)
        {
            Debug.LogWarning("[MemoryModeManager] EnterMemoryMode called with a null MemoryObject.", this);
            return;
        }

        CacheOriginalVisualState();

        if (IsInMemoryMode && currentMemoryObject == memoryObject)
        {
            Debug.Log("[MemoryModeManager] EnterMemoryMode ignored because this object is already active.", this);
            return;
        }

        if (currentMemoryObject != null && currentMemoryObject != memoryObject)
        {
            currentMemoryObject.SetHighlight(false);
        }

        currentMemoryObject = memoryObject;
        IsInMemoryMode = true;
        currentMemoryObject.SetHighlight(true);

        StartVisualTransition(
            hasCachedLightState ? memoryLightIntensity : 0f,
            hasCachedVolumeState ? memoryVolumeWeight : 0f,
            true,
            "[MemoryModeManager] Memory mode visual feedback applied.");

        string safeItemName = string.IsNullOrWhiteSpace(memoryObject.itemName) ? "(Unnamed Memory)" : memoryObject.itemName;
        string safeDescription = string.IsNullOrWhiteSpace(memoryObject.shortDescription) ? "(No Description)" : memoryObject.shortDescription;
        string safeEmotion = string.IsNullOrWhiteSpace(memoryObject.emotionType) ? "(No Emotion Type)" : memoryObject.emotionType;

        Debug.Log(
            $"[MemoryModeManager] EnterMemoryMode -> Name: {safeItemName}, Description: {safeDescription}, Emotion: {safeEmotion}",
            this);
    }

    public void ExitMemoryMode()
    {
        if (!IsInMemoryMode && currentMemoryObject == null)
        {
            return;
        }

        MemoryObject exitingMemoryObject = currentMemoryObject;

        IsInMemoryMode = false;
        currentMemoryObject = null;

        if (exitingMemoryObject != null)
        {
            exitingMemoryObject.SetHighlight(false);
        }

        CacheOriginalVisualState();
        StartVisualTransition(
            hasCachedLightState ? originalLightIntensity : 0f,
            hasCachedVolumeState ? originalVolumeWeight : 0f,
            hasCachedVolumeState && originalVolumeEnabled,
            "[MemoryModeManager] Memory mode visual feedback restored.");

        Debug.Log("[MemoryModeManager] Exiting memory mode.", this);
    }

    private void CacheOriginalVisualState()
    {
        if (targetDirectionalLight != null && !hasCachedLightState)
        {
            originalLightIntensity = targetDirectionalLight.intensity;
            hasCachedLightState = true;
        }

        if (targetGlobalVolume != null && !hasCachedVolumeState)
        {
            originalVolumeEnabled = targetGlobalVolume.enabled;
            originalVolumeWeight = targetGlobalVolume.weight;
            hasCachedVolumeState = true;
        }
    }

    private void StartVisualTransition(
        float targetLightIntensity,
        float targetVolumeWeight,
        bool finalVolumeEnabled,
        string completionLog)
    {
        if (visualTransitionCoroutine != null)
        {
            StopCoroutine(visualTransitionCoroutine);
            visualTransitionCoroutine = null;
        }

        visualTransitionCoroutine = StartCoroutine(
            TransitionVisualState(targetLightIntensity, targetVolumeWeight, finalVolumeEnabled, completionLog));
    }

    private IEnumerator TransitionVisualState(
        float targetLightIntensity,
        float targetVolumeWeight,
        bool finalVolumeEnabled,
        string completionLog)
    {
        float duration = Mathf.Max(0f, memoryTransitionDuration);

        float startingLightIntensity = targetDirectionalLight != null ? targetDirectionalLight.intensity : 0f;
        float startingVolumeWeight = targetGlobalVolume != null ? targetGlobalVolume.weight : 0f;

        if (targetGlobalVolume != null)
        {
            targetGlobalVolume.enabled = true;
        }

        if (duration <= Mathf.Epsilon)
        {
            ApplyVisualState(targetLightIntensity, targetVolumeWeight, finalVolumeEnabled);
            visualTransitionCoroutine = null;

            if (!string.IsNullOrWhiteSpace(completionLog))
            {
                Debug.Log(completionLog, this);
            }

            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            if (targetDirectionalLight != null)
            {
                targetDirectionalLight.intensity = Mathf.Lerp(startingLightIntensity, targetLightIntensity, t);
            }

            if (targetGlobalVolume != null)
            {
                targetGlobalVolume.weight = Mathf.Lerp(startingVolumeWeight, targetVolumeWeight, t);
            }

            yield return null;
        }

        ApplyVisualState(targetLightIntensity, targetVolumeWeight, finalVolumeEnabled);
        visualTransitionCoroutine = null;

        if (!string.IsNullOrWhiteSpace(completionLog))
        {
            Debug.Log(completionLog, this);
        }
    }

    private void ApplyVisualState(float lightIntensity, float volumeWeight, bool finalVolumeEnabled)
    {
        if (targetDirectionalLight != null)
        {
            targetDirectionalLight.intensity = lightIntensity;
        }

        if (targetGlobalVolume != null)
        {
            targetGlobalVolume.weight = volumeWeight;
            targetGlobalVolume.enabled = finalVolumeEnabled;
        }
    }
}
