using UnityEngine;

public class MemoryModeManager : MonoBehaviour
{
    public static MemoryModeManager Instance { get; private set; }

    [Header("Memory Mode Lighting")]
    [SerializeField] private MemoryModeLightingController lightingController;

    [Header("Memory Mode UI")]
    [SerializeField] private MemoryModeUIController memoryModeUIController;

    public bool IsInMemoryMode { get; private set; }
    public MemoryObject CurrentMemoryObject => currentMemoryObject;

    private MemoryObject currentMemoryObject;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[MemoryModeManager] Duplicate instance found. Destroying the new one.", this);
            Destroy(gameObject);
            return;
        }

        Instance = this;
        TryAutoAssignLightingController();
        TryAutoAssignUIController();
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

        TryAutoAssignLightingController();

        currentMemoryObject = memoryObject;
        IsInMemoryMode = true;

        if (lightingController != null)
        {
            lightingController.EnterLightingMode(currentMemoryObject);
        }
        else
        {
            Debug.LogWarning("[MemoryModeManager] MemoryModeLightingController was not found. Memory mode will continue without lighting changes.", this);
        }

        MemoryItemData data = memoryObject.MemoryItemData;
        ShowMemoryModeUI(data);

        string safeItemId = data != null && !string.IsNullOrWhiteSpace(data.ItemId) ? data.ItemId : "(No Item Id)";
        string safeItemName = string.IsNullOrWhiteSpace(memoryObject.ItemName) ? "(Unnamed Memory)" : memoryObject.ItemName;
        string safeDescription = string.IsNullOrWhiteSpace(memoryObject.ShortDescription) ? "(No Description)" : memoryObject.ShortDescription;
        string safeEmotion = string.IsNullOrWhiteSpace(memoryObject.EmotionType) ? "(No Emotion Type)" : memoryObject.EmotionType;

        Debug.Log(
            $"[MemoryModeManager] EnterMemoryMode -> Id: {safeItemId}, Name: {safeItemName}, Description: {safeDescription}, Emotion: {safeEmotion}",
            this);
    }

    public void ExitMemoryMode()
    {
        if (!IsInMemoryMode && currentMemoryObject == null)
        {
            return;
        }

        IsInMemoryMode = false;
        currentMemoryObject = null;

        if (lightingController != null)
        {
            lightingController.ExitLightingMode();
        }
        else
        {
            Debug.LogWarning("[MemoryModeManager] MemoryModeLightingController was not found. Memory mode lighting restore was skipped.", this);
        }

        HideMemoryModeUI();

        Debug.Log("[MemoryModeManager] Exiting memory mode.", this);
    }

    private void TryAutoAssignLightingController()
    {
        if (lightingController != null)
        {
            return;
        }

        lightingController = UnityEngine.Object.FindObjectOfType<MemoryModeLightingController>(true);
    }

    private void TryAutoAssignUIController()
    {
        if (memoryModeUIController != null)
        {
            return;
        }

        memoryModeUIController = UnityEngine.Object.FindObjectOfType<MemoryModeUIController>(true);
    }

    private void ShowMemoryModeUI(MemoryItemData data)
    {
        TryAutoAssignUIController();

        if (memoryModeUIController == null)
        {
            Debug.LogWarning("[MemoryModeManager] MemoryModeUIController is not assigned.", this);
            return;
        }

        memoryModeUIController.Show(data);
    }

    private void HideMemoryModeUI()
    {
        TryAutoAssignUIController();

        if (memoryModeUIController == null)
        {
            Debug.LogWarning("[MemoryModeManager] MemoryModeUIController is not assigned.", this);
            return;
        }

        memoryModeUIController.Hide();
    }
}
