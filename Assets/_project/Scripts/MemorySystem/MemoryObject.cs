using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(XRGrabInteractable))]
public class MemoryObject : MonoBehaviour
{
    [Header("Memory Data")]
    [SerializeField] private MemoryItemData memoryItemData;

    [Header("Observe Settings")]
    public float observeRequiredTime = 2f;
    public float maxObserveAngle = 25f;
    public float observeLogInterval = 0.5f;

    public bool IsHeld { get; private set; }
    public bool IsBeingObserved { get; private set; }
    public float ObserveProgress { get; private set; }
    public MemoryItemData MemoryItemData => memoryItemData;
    public string ItemId => memoryItemData != null ? memoryItemData.ItemId : string.Empty;
    public string ItemName => memoryItemData != null ? memoryItemData.ItemName : gameObject.name;
    public string ShortDescription => memoryItemData != null ? memoryItemData.ShortDescription : string.Empty;
    public string EmotionType => memoryItemData != null ? memoryItemData.EmotionType : string.Empty;

    private XRGrabInteractable grabInteractable;
    private bool hasTriggeredWhileHeld;
    private float nextObserveLogTime;

    private void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();

        if (grabInteractable == null)
        {
            Debug.LogError($"[{nameof(MemoryObject)}] Missing {nameof(XRGrabInteractable)} on {name}.", this);
        }
    }

    private void OnEnable()
    {
        if (grabInteractable == null)
        {
            grabInteractable = GetComponent<XRGrabInteractable>();
        }

        if (grabInteractable == null)
        {
            return;
        }

        grabInteractable.selectEntered.AddListener(OnSelectEntered);
        grabInteractable.selectExited.AddListener(OnSelectExited);
    }

    private void OnDisable()
    {
        if (grabInteractable == null)
        {
            return;
        }

        grabInteractable.selectEntered.RemoveListener(OnSelectEntered);
        grabInteractable.selectExited.RemoveListener(OnSelectExited);
    }

    private void Update()
    {
        if (!IsHeld)
        {
            return;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            IsBeingObserved = false;
            return;
        }

        Vector3 directionToObject = transform.position - mainCamera.transform.position;
        if (directionToObject.sqrMagnitude <= Mathf.Epsilon)
        {
            return;
        }

        float angle = Vector3.Angle(mainCamera.transform.forward, directionToObject.normalized);
        bool isWithinObserveAngle = angle <= maxObserveAngle;

        if (isWithinObserveAngle)
        {
            HandleObserveWithinAngle();
            return;
        }

        HandleObserveOutsideAngle();
    }

    private void HandleObserveWithinAngle()
    {
        if (!IsBeingObserved)
        {
            Debug.Log($"[MemoryObject] Started observing {ItemName}.", this);
            nextObserveLogTime = Time.time + Mathf.Max(0.1f, observeLogInterval);
        }

        IsBeingObserved = true;

        if (hasTriggeredWhileHeld)
        {
            return;
        }

        ObserveProgress = Mathf.Min(ObserveProgress + Time.deltaTime, observeRequiredTime);

        if (Time.time >= nextObserveLogTime)
        {
            Debug.Log(
                $"[MemoryObject] Observing {ItemName} ({ObserveProgress:F2}/{observeRequiredTime:F2}s).",
                this);
            nextObserveLogTime = Time.time + Mathf.Max(0.1f, observeLogInterval);
        }

        if (ObserveProgress < observeRequiredTime)
        {
            return;
        }

        hasTriggeredWhileHeld = true;
        ObserveProgress = observeRequiredTime;

        Debug.Log($"[MemoryObject] Memory triggered for {ItemName}.", this);

        if (MemoryModeManager.Instance != null)
        {
            MemoryModeManager.Instance.EnterMemoryMode(this);
        }
        else
        {
            Debug.LogWarning("[MemoryObject] MemoryModeManager.Instance is null.", this);
        }
    }

    private void HandleObserveOutsideAngle()
    {
        bool wasActiveMemoryObject = MemoryModeManager.Instance != null &&
            MemoryModeManager.Instance.CurrentMemoryObject == this;

        if (wasActiveMemoryObject)
        {
            Debug.Log($"[MemoryObject] Lost observation on active memory {ItemName}. Exiting memory mode.", this);
            MemoryModeManager.Instance.ExitMemoryMode();
            ResetObservationState(true);
            return;
        }

        if (IsBeingObserved || ObserveProgress > 0f)
        {
            Debug.Log($"[MemoryObject] Lost observation on {ItemName}. Progress reset.", this);
        }

        ResetObservationState(hasTriggeredWhileHeld);
    }

    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        IsHeld = true;
        ResetObservationState(true);

        Debug.Log($"[MemoryObject] Grabbed {ItemName}.", this);
    }

    private void OnSelectExited(SelectExitEventArgs args)
    {
        if (MemoryModeManager.Instance != null && MemoryModeManager.Instance.CurrentMemoryObject == this)
        {
            MemoryModeManager.Instance.ExitMemoryMode();
        }

        IsHeld = false;
        ResetObservationState(true);

        Debug.Log($"[MemoryObject] Released {ItemName}. Observation reset.", this);
    }

    private void ResetObservationState(bool allowRetrigger)
    {
        IsBeingObserved = false;
        ObserveProgress = 0f;
        nextObserveLogTime = 0f;

        if (allowRetrigger)
        {
            hasTriggeredWhileHeld = false;
        }
    }
}
