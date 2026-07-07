using System;
using UnityEngine;

public class MemoryModeManager : MonoBehaviour
{
    public static MemoryModeManager Instance { get; private set; }

    public event Action<MemoryObject> OnMemoryModeEntered;
    public event Action<MemoryObject> OnMemoryModeExited;

    [Header("Memory Mode Lighting")]
    [SerializeField] private MemoryModeLightingController lightingController;

    [Header("Memory Mode UI")]
    [SerializeField] private bool preferHiFiMemoryUI = true;
    [SerializeField] private MemoryModeUIController memoryModeUIController;
    [SerializeField] private MemoryUIRootController memoryUIRootHiFiController;

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
        TryAutoAssignHiFiUIController();
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

        OnMemoryModeEntered?.Invoke(currentMemoryObject);
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

        OnMemoryModeExited?.Invoke(exitingMemoryObject);
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

    private void TryAutoAssignHiFiUIController()
    {
        if (memoryUIRootHiFiController != null)
        {
            return;
        }

        memoryUIRootHiFiController = UnityEngine.Object.FindObjectOfType<MemoryUIRootController>(true);
    }

    private void ShowMemoryModeUI(MemoryItemData data)
    {
        TryAutoAssignUIController();
        TryAutoAssignHiFiUIController();

        if (preferHiFiMemoryUI && memoryUIRootHiFiController != null)
        {
            memoryModeUIController?.Hide();
            memoryUIRootHiFiController.Bind(data);
            memoryUIRootHiFiController.Show();
            return;
        }

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
        TryAutoAssignHiFiUIController();

        if (memoryUIRootHiFiController != null)
        {
            memoryUIRootHiFiController.Hide();
        }

        if (memoryModeUIController == null)
        {
            return;
        }

        memoryModeUIController.Hide();
    }
}
