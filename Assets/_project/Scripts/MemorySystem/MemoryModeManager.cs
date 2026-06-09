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

    public bool IsInMemoryMode { get; private set; }

    private MemoryObject currentMemoryObject;
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

        if (IsInMemoryMode && currentMemoryObject == memoryObject)
        {
            Debug.Log("[MemoryModeManager] EnterMemoryMode ignored because this object is already active.", this);
            return;
        }

        if (currentMemoryObject != null && currentMemoryObject != memoryObject)
        {
            currentMemoryObject.SetHighlight(false);
        }

        IsInMemoryMode = true;
        currentMemoryObject = memoryObject;

        string safeItemName = string.IsNullOrWhiteSpace(memoryObject.itemName) ? "(Unnamed Memory)" : memoryObject.itemName;
        string safeDescription = string.IsNullOrWhiteSpace(memoryObject.shortDescription) ? "(No Description)" : memoryObject.shortDescription;
        string safeEmotion = string.IsNullOrWhiteSpace(memoryObject.emotionType) ? "(No Emotion Type)" : memoryObject.emotionType;

        ApplyVisualFeedback(memoryObject);

        Debug.Log(
            $"[MemoryModeManager] EnterMemoryMode -> Name: {safeItemName}, Description: {safeDescription}, Emotion: {safeEmotion}",
            this);
    }

    public void ExitMemoryMode()
    {
        if (!IsInMemoryMode)
        {
            Debug.Log("[MemoryModeManager] ExitMemoryMode called, but memory mode was already inactive.", this);
            return;
        }

        RestoreVisualFeedback();
        IsInMemoryMode = false;
        currentMemoryObject = null;
        Debug.Log("[MemoryModeManager] Exited memory mode.", this);
    }

    private void ApplyVisualFeedback(MemoryObject memoryObject)
    {
        if (targetDirectionalLight != null)
        {
            if (!hasCachedLightState)
            {
                originalLightIntensity = targetDirectionalLight.intensity;
                hasCachedLightState = true;
            }

            targetDirectionalLight.intensity = memoryLightIntensity;
        }
        else
        {
            Debug.LogWarning("[MemoryModeManager] No targetDirectionalLight assigned. Light feedback skipped.", this);
        }

        if (targetGlobalVolume != null)
        {
            if (!hasCachedVolumeState)
            {
                originalVolumeEnabled = targetGlobalVolume.enabled;
                originalVolumeWeight = targetGlobalVolume.weight;
                hasCachedVolumeState = true;
            }

            targetGlobalVolume.enabled = true;
            targetGlobalVolume.weight = memoryVolumeWeight;
        }
        else
        {
            Debug.LogWarning("[MemoryModeManager] No targetGlobalVolume assigned. Volume feedback skipped.", this);
        }

        memoryObject.SetHighlight(true);
        Debug.Log("[MemoryModeManager] Memory mode visual feedback applied.", this);
    }

    private void RestoreVisualFeedback()
    {
        if (targetDirectionalLight != null && hasCachedLightState)
        {
            targetDirectionalLight.intensity = originalLightIntensity;
        }

        if (targetGlobalVolume != null && hasCachedVolumeState)
        {
            targetGlobalVolume.enabled = originalVolumeEnabled;
            targetGlobalVolume.weight = originalVolumeWeight;
        }

        if (currentMemoryObject != null)
        {
            currentMemoryObject.SetHighlight(false);
        }

        Debug.Log("[MemoryModeManager] Memory mode visual feedback restored.", this);
    }
}
