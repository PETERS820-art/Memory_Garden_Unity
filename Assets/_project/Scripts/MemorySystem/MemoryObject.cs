using System;
using System.Collections;
using System.Collections.Generic;
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

    [Header("Placement Settings")]
    [SerializeField] private bool enablePlacement = true;
    [SerializeField] private ItemSizeMode itemSizeMode = ItemSizeMode.AutoFromBounds;
    [SerializeField] private ItemSize manualItemSize = ItemSize.Medium;
    [SerializeField] private List<SlotType> allowedSlotTypes = new List<SlotType>();
    [SerializeField] private float preferredHeightOffset = 0f;
    [SerializeField] private bool alignToSlotRotation = true;
    [SerializeField] private MemoryDisplaySlot currentSlot;
    [SerializeField] private bool lockRigidbodyDuringSnap = true;

    public bool IsHeld { get; private set; }
    public bool IsBeingObserved { get; private set; }
    public float ObserveProgress { get; private set; }
    public MemoryItemData MemoryItemData => memoryItemData;
    public string ItemId => memoryItemData != null ? memoryItemData.ItemId : string.Empty;
    public string ItemName => memoryItemData != null ? memoryItemData.ItemName : gameObject.name;
    public string ShortDescription => memoryItemData != null ? memoryItemData.ShortDescription : string.Empty;
    public string EmotionType => memoryItemData != null ? memoryItemData.EmotionType : string.Empty;
    public bool EnablePlacement => enablePlacement;
    public ItemSizeMode PlacementItemSizeMode => itemSizeMode;
    public ItemSize ManualItemSize => manualItemSize;
    public IReadOnlyList<SlotType> AllowedSlotTypes => allowedSlotTypes;
    public float PreferredHeightOffset => preferredHeightOffset;
    public bool AlignToSlotRotation => alignToSlotRotation;
    public MemoryDisplaySlot CurrentSlot => currentSlot;

    private XRGrabInteractable grabInteractable;
    private Rigidbody attachedRigidbody;
    private bool hasTriggeredWhileHeld;
    private float nextObserveLogTime;
    private Coroutine snapRoutine;
    private bool restoreIsKinematicAfterSnap;

    private void Awake()
    {
        EnsurePlacementDefaults();
        grabInteractable = GetComponent<XRGrabInteractable>();
        attachedRigidbody = GetComponent<Rigidbody>();

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
        CancelActiveSnap();
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
        CancelActiveSnap();

        if (currentSlot != null)
        {
            currentSlot.ClearOccupied();
            currentSlot = null;
        }

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
        StartSnapAfterRelease();

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

    public bool CanUseSlotType(SlotType slotType)
    {
        if (!enablePlacement)
        {
            return false;
        }

        if (allowedSlotTypes == null || allowedSlotTypes.Count == 0)
        {
            return true;
        }

        for (int i = 0; i < allowedSlotTypes.Count; i++)
        {
            if (allowedSlotTypes[i] == slotType)
            {
                return true;
            }
        }

        return false;
    }

    public ItemSize GetPlacementItemSize()
    {
        if (itemSizeMode == ItemSizeMode.Manual)
        {
            return manualItemSize;
        }

        if (!TryGetPlacementBounds(out Bounds placementBounds))
        {
            return manualItemSize;
        }

        Vector3 size = placementBounds.size;
        float maxDimension = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
        float maxHorizontalDimension = Mathf.Max(size.x, size.z);
        bool isClearlyTall = size.y >= maxHorizontalDimension * 1.35f && size.y >= 0.9f;

        if (isClearlyTall || maxDimension > 1.8f)
        {
            return ItemSize.Tall;
        }

        if (maxDimension < 0.4f)
        {
            return ItemSize.Small;
        }

        if (maxDimension <= 1.0f)
        {
            return ItemSize.Medium;
        }

        return ItemSize.Large;
    }

    public bool CanUseSlot(MemoryDisplaySlot slot)
    {
        if (slot == null || !enablePlacement)
        {
            return false;
        }

        return slot.CanAccept(this);
    }

    public void ClearCurrentSlotAssignment()
    {
        CancelActiveSnap();

        if (currentSlot != null && currentSlot.OccupiedItem == gameObject)
        {
            currentSlot.ClearOccupied();
        }

        currentSlot = null;
    }

    public bool TryPlaceOnSlot(MemoryDisplaySlot targetSlot)
    {
        if (targetSlot == null || !enablePlacement)
        {
            return false;
        }

        ClearCurrentSlotAssignment();

        if (!targetSlot.CanAccept(this))
        {
            return false;
        }

        Pose snapPose = targetSlot.GetSnapPose();
        Quaternion targetRotation = alignToSlotRotation ? snapPose.rotation : transform.rotation;
        Vector3 targetPosition = CalculatePlacementPosition(
            snapPose.position,
            targetRotation,
            targetSlot.transform.up);

        PrepareRigidbodyForSnap();
        transform.SetPositionAndRotation(targetPosition, targetRotation);
        Physics.SyncTransforms();
        RestoreRigidbodyAfterSnap();

        targetSlot.MarkOccupied(gameObject);
        currentSlot = targetSlot;
        return true;
    }

    private void StartSnapAfterRelease()
    {
        if (!enablePlacement)
        {
            return;
        }

        CancelActiveSnap();
        snapRoutine = StartCoroutine(SnapAfterReleaseRoutine());
    }

    private void Reset()
    {
        EnsurePlacementDefaults();
    }

    private void OnValidate()
    {
        EnsurePlacementDefaults();
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

    private bool TryGetPlacementBounds(out Bounds placementBounds)
    {
        if (preferColliderBounds)
        {
            if (TryGetCombinedColliderBounds(out placementBounds))
            {
                return true;
            }

            if (TryGetCombinedRendererBounds(out placementBounds))
            {
                return true;
            }
        }
        else
        {
            if (TryGetCombinedRendererBounds(out placementBounds))
            {
                return true;
            }

            if (TryGetCombinedColliderBounds(out placementBounds))
            {
                return true;
            }
        }

        placementBounds = default;
        return false;
    }

    private IEnumerator SnapAfterReleaseRoutine()
    {
        yield return new WaitForEndOfFrame();

        if (!enablePlacement)
        {
            snapRoutine = null;
            yield break;
        }

        MemoryDisplaySlot targetSlot = FindNearestValidSlot();
        if (targetSlot == null)
        {
            snapRoutine = null;
            yield break;
        }

        Pose snapPose = targetSlot.GetSnapPose();
        Quaternion targetRotation = alignToSlotRotation ? snapPose.rotation : transform.rotation;
        Vector3 targetPosition = CalculatePlacementPosition(
            snapPose.position,
            targetRotation,
            targetSlot.transform.up);

        PrepareRigidbodyForSnap();

        if (targetSlot.UseSmoothSnap && targetSlot.SnapDuration > 0.001f)
        {
            Vector3 startPosition = transform.position;
            Quaternion startRotation = transform.rotation;
            float elapsed = 0f;

            while (elapsed < targetSlot.SnapDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / targetSlot.SnapDuration);
                transform.SetPositionAndRotation(
                    Vector3.Lerp(startPosition, targetPosition, t),
                    Quaternion.Slerp(startRotation, targetRotation, t));
                yield return null;
            }
        }

        transform.SetPositionAndRotation(targetPosition, targetRotation);
        Physics.SyncTransforms();
        RestoreRigidbodyAfterSnap();

        targetSlot.MarkOccupied(gameObject);
        currentSlot = targetSlot;
        snapRoutine = null;
    }

    private MemoryDisplaySlot FindNearestValidSlot()
    {
        MemoryDisplaySlot[] allSlots =
            FindObjectsByType<MemoryDisplaySlot>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        MemoryDisplaySlot nearestSlot = null;
        float nearestDistance = float.PositiveInfinity;

        for (int i = 0; i < allSlots.Length; i++)
        {
            MemoryDisplaySlot slot = allSlots[i];
            if (slot == null || !slot.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (!slot.CanAccept(this))
            {
                continue;
            }

            float distance = Vector3.Distance(transform.position, slot.transform.position);
            if (distance > slot.SnapRadius || distance > nearestDistance)
            {
                continue;
            }

            nearestDistance = distance;
            nearestSlot = slot;
        }

        return nearestSlot;
    }

    private Vector3 CalculatePlacementPosition(Vector3 slotPosition, Quaternion targetRotation, Vector3 slotUp)
    {
        Vector3 normalizedUp = slotUp.sqrMagnitude > Mathf.Epsilon ? slotUp.normalized : Vector3.up;
        Vector3 targetPosition = slotPosition + (normalizedUp * preferredHeightOffset);

        if (!TryGetPlacementBoundsCornerPoints(out Vector3[] localCorners))
        {
            return targetPosition;
        }

        Vector3 lossyScale = transform.lossyScale;
        Matrix4x4 targetMatrix = Matrix4x4.TRS(targetPosition, targetRotation, lossyScale);
        float minProjection = float.PositiveInfinity;

        for (int i = 0; i < localCorners.Length; i++)
        {
            Vector3 worldCorner = targetMatrix.MultiplyPoint3x4(localCorners[i]);
            float projection = Vector3.Dot(worldCorner - slotPosition, normalizedUp);
            if (projection < minProjection)
            {
                minProjection = projection;
            }
        }

        if (float.IsPositiveInfinity(minProjection))
        {
            return targetPosition;
        }

        if (minProjection < 0f)
        {
            targetPosition += normalizedUp * -minProjection;
        }

        return targetPosition;
    }

    private bool TryGetPlacementBoundsCornerPoints(out Vector3[] localCorners)
    {
        if (!TryGetPlacementBounds(out Bounds placementBounds))
        {
            localCorners = Array.Empty<Vector3>();
            return false;
        }

        Vector3 center = placementBounds.center;
        Vector3 extents = placementBounds.extents;
        localCorners = new Vector3[8];
        int index = 0;

        for (int x = -1; x <= 1; x += 2)
        {
            for (int y = -1; y <= 1; y += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 worldCorner = center + new Vector3(
                        extents.x * x,
                        extents.y * y,
                        extents.z * z);
                    localCorners[index] = transform.InverseTransformPoint(worldCorner);
                    index++;
                }
            }
        }

        return true;
    }

    private void PrepareRigidbodyForSnap()
    {
        if (attachedRigidbody == null)
        {
            return;
        }

        attachedRigidbody.velocity = Vector3.zero;
        attachedRigidbody.angularVelocity = Vector3.zero;

        if (!lockRigidbodyDuringSnap)
        {
            restoreIsKinematicAfterSnap = false;
            return;
        }

        restoreIsKinematicAfterSnap = !attachedRigidbody.isKinematic;
        attachedRigidbody.isKinematic = true;
    }

    private void RestoreRigidbodyAfterSnap()
    {
        if (attachedRigidbody == null)
        {
            return;
        }

        if (lockRigidbodyDuringSnap && restoreIsKinematicAfterSnap)
        {
            attachedRigidbody.isKinematic = false;
            restoreIsKinematicAfterSnap = false;
        }

        if (!attachedRigidbody.isKinematic)
        {
            attachedRigidbody.velocity = Vector3.zero;
            attachedRigidbody.angularVelocity = Vector3.zero;
        }
    }

    private void CancelActiveSnap()
    {
        if (snapRoutine != null)
        {
            StopCoroutine(snapRoutine);
            snapRoutine = null;
        }

        RestoreRigidbodyAfterSnap();
    }

    private void EnsurePlacementDefaults()
    {
        if (allowedSlotTypes == null)
        {
            allowedSlotTypes = new List<SlotType>();
        }
    }
}
