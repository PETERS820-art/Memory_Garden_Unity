using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(XRGrabInteractable))]
public class MemoryObject : MonoBehaviour
{
    private const float ObservationGizmoSphereRadius = 0.03f;
    private const float ObservationGizmoCrossHalfSize = 0.08f;

    [Header("Memory Data")]
    [SerializeField] private MemoryItemData memoryItemData;

    [Header("Observe Settings")]
    public float observeRequiredTime = 2f;
    public float maxObserveAngle = 25f;
    public float observeLogInterval = 0.5f;
    public Transform observeAnchor;
    public bool useBoundsCenterForObservation = true;
    public bool preferColliderBounds = true;

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

        Vector3 observationCenter = GetObservationCenter();
        Vector3 directionToObject = observationCenter - mainCamera.transform.position;
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

    public Vector3 GetObservationCenter()
    {
        if (observeAnchor != null)
        {
            return observeAnchor.position;
        }

        if (useBoundsCenterForObservation)
        {
            if (preferColliderBounds)
            {
                if (TryGetCombinedColliderBounds(out Bounds colliderBounds))
                {
                    return colliderBounds.center;
                }

                if (TryGetCombinedRendererBounds(out Bounds rendererBounds))
                {
                    return rendererBounds.center;
                }
            }
            else
            {
                if (TryGetCombinedRendererBounds(out Bounds rendererBounds))
                {
                    return rendererBounds.center;
                }

                if (TryGetCombinedColliderBounds(out Bounds colliderBounds))
                {
                    return colliderBounds.center;
                }
            }
        }

        return transform.position;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 center = GetObservationCenter();
        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(center, ObservationGizmoSphereRadius);
        Gizmos.DrawLine(center + (Vector3.right * ObservationGizmoCrossHalfSize), center - (Vector3.right * ObservationGizmoCrossHalfSize));
        Gizmos.DrawLine(center + (Vector3.up * ObservationGizmoCrossHalfSize), center - (Vector3.up * ObservationGizmoCrossHalfSize));
        Gizmos.DrawLine(center + (Vector3.forward * ObservationGizmoCrossHalfSize), center - (Vector3.forward * ObservationGizmoCrossHalfSize));
    }

    private bool TryGetCombinedColliderBounds(out Bounds combinedBounds)
    {
        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        bool hasBounds = false;
        combinedBounds = default;

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                combinedBounds = collider.bounds;
                hasBounds = true;
                continue;
            }

            combinedBounds.Encapsulate(collider.bounds);
        }

        return hasBounds;
    }

    private bool TryGetCombinedRendererBounds(out Bounds combinedBounds)
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        combinedBounds = default;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                combinedBounds = renderer.bounds;
                hasBounds = true;
                continue;
            }

            combinedBounds.Encapsulate(renderer.bounds);
        }

        return hasBounds;
    }
}
