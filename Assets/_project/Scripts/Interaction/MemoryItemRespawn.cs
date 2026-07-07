using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

[DisallowMultipleComponent]
[RequireComponent(typeof(MemoryItemInteractionState))]
public class MemoryItemRespawn : MonoBehaviour
{
    [SerializeField] private float maxDistanceFromStart = 100f;
    [SerializeField] private float minY = -10f;
    [SerializeField] private float checkInterval = 0.5f;
    [SerializeField] private float respawnHeightOffset = 0.3f;
    [SerializeField] private bool resetWhenSelected = false;
    [SerializeField] private bool resetOnLowYEvenWhenSelected = true;

    private XRGrabInteractable grabInteractable;
    private Rigidbody attachedRigidbody;
    private MemoryItemInteractionState interactionState;
    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private float nextCheckTime;
    private Coroutine resetRoutine;
    private bool pendingForceDeselect;

    private void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
        attachedRigidbody = GetComponent<Rigidbody>();
        interactionState = EnsureInteractionStateComponent();
        SetInitialPoseToCurrent();
    }

    private void OnEnable()
    {
        if (interactionState == null)
        {
            interactionState = EnsureInteractionStateComponent();
        }

        nextCheckTime = Time.time + Mathf.Max(0.01f, checkInterval);
    }

    private void OnDisable()
    {
        if (resetRoutine != null)
        {
            StopCoroutine(resetRoutine);
            resetRoutine = null;
        }

        pendingForceDeselect = false;
    }

    private void Update()
    {
        if (Time.time < nextCheckTime)
        {
            return;
        }

        nextCheckTime = Time.time + Mathf.Max(0.01f, checkInterval);
        EvaluateRespawnConditions();
    }

    public void ResetToInitialPose()
    {
        QueueReset(forceDeselectIfSelected: true);
    }

    public void SetInitialPoseToCurrent()
    {
        initialPosition = transform.position;
        initialRotation = transform.rotation;
    }

    public void SetRespawnPose(Vector3 position, Quaternion rotation)
    {
        initialPosition = position;
        initialRotation = rotation;
    }

    private Vector3 GetRespawnPosition()
    {
        return initialPosition + Vector3.up * Mathf.Max(0f, respawnHeightOffset);
    }

    private void EvaluateRespawnConditions()
    {
        if (!IsRespawnAllowed())
        {
            return;
        }

        bool isSelected = grabInteractable != null && grabInteractable.isSelected;
        Vector3 currentPosition = transform.position;

        bool isBelowMinimumY = currentPosition.y < minY;
        if (isBelowMinimumY)
        {
            if (!isSelected || resetOnLowYEvenWhenSelected || resetWhenSelected)
            {
                QueueReset(forceDeselectIfSelected: isSelected);
            }

            return;
        }

        if (isSelected && !resetWhenSelected)
        {
            return;
        }

        float distanceFromStart = Vector3.Distance(currentPosition, initialPosition);
        if (distanceFromStart > maxDistanceFromStart)
        {
            QueueReset(forceDeselectIfSelected: isSelected);
        }
    }

    private void QueueReset(bool forceDeselectIfSelected)
    {
        if (!IsRespawnAllowed())
        {
            return;
        }

        pendingForceDeselect |= forceDeselectIfSelected;

        if (resetRoutine == null && isActiveAndEnabled)
        {
            resetRoutine = StartCoroutine(ResetRoutine());
        }
    }

    private IEnumerator ResetRoutine()
    {
        SetInteractionState(MemoryItemInteractionStateId.Respawning, "Respawn queued");
        bool shouldForceDeselect = pendingForceDeselect;
        pendingForceDeselect = false;
        XRInteractionManager interactionManager = null;

        if (shouldForceDeselect && grabInteractable != null && grabInteractable.isSelected)
        {
            interactionManager = ResolveInteractionManager();
            if (interactionManager != null)
            {
                interactionManager.CancelInteractableSelection((IXRSelectInteractable)grabInteractable);
            }
            else
            {
                Debug.LogWarning($"{nameof(MemoryItemRespawn)} could not find an XRInteractionManager to release {name} before respawn.", this);
            }
        }

        bool restoreInteractable = grabInteractable != null && grabInteractable.enabled;
        if (restoreInteractable)
        {
            grabInteractable.enabled = false;
        }

        yield return new WaitForEndOfFrame();
        yield return new WaitForFixedUpdate();

        if (interactionManager != null && grabInteractable != null && grabInteractable.isSelected)
        {
            interactionManager.CancelInteractableSelection((IXRSelectInteractable)grabInteractable);
            yield return new WaitForEndOfFrame();
            yield return new WaitForFixedUpdate();
        }

        yield return ApplyResetPoseRoutine();

        if (restoreInteractable && grabInteractable != null)
        {
            grabInteractable.enabled = true;
        }

        SetInteractionState(MemoryItemInteractionStateId.Idle, "Respawn complete");
        resetRoutine = null;

        if (pendingForceDeselect)
        {
            resetRoutine = StartCoroutine(ResetRoutine());
        }
    }

    private IEnumerator ApplyResetPoseRoutine()
    {
        Vector3 respawnPosition = GetRespawnPosition();
        bool shouldDropAfterRespawn = respawnHeightOffset > 0.001f;

        if (attachedRigidbody != null)
        {
            bool originalIsKinematic = attachedRigidbody.isKinematic;
            bool originalDetectCollisions = attachedRigidbody.detectCollisions;

            attachedRigidbody.velocity = Vector3.zero;
            attachedRigidbody.angularVelocity = Vector3.zero;
            attachedRigidbody.isKinematic = true;
            attachedRigidbody.detectCollisions = false;

            transform.SetPositionAndRotation(respawnPosition, initialRotation);
            attachedRigidbody.position = respawnPosition;
            attachedRigidbody.rotation = initialRotation;
            Physics.SyncTransforms();

            yield return new WaitForFixedUpdate();

            attachedRigidbody.detectCollisions = originalDetectCollisions;
            attachedRigidbody.isKinematic = originalIsKinematic;
            attachedRigidbody.velocity = Vector3.zero;
            attachedRigidbody.angularVelocity = Vector3.zero;
            if (shouldDropAfterRespawn && !originalIsKinematic)
            {
                attachedRigidbody.WakeUp();
            }
            else
            {
                attachedRigidbody.Sleep();
            }
            yield break;
        }

        transform.SetPositionAndRotation(respawnPosition, initialRotation);
        yield return null;
    }

    private XRInteractionManager ResolveInteractionManager()
    {
        if (grabInteractable != null && grabInteractable.interactionManager != null)
        {
            return grabInteractable.interactionManager as XRInteractionManager;
        }

        return FindFirstObjectByType<XRInteractionManager>(FindObjectsInactive.Include);
    }

    private bool IsRespawnAllowed()
    {
        return interactionState == null || interactionState.IsRespawnAllowed;
    }

    private MemoryItemInteractionState EnsureInteractionStateComponent()
    {
        MemoryItemInteractionState state = GetComponent<MemoryItemInteractionState>();
        if (state != null)
        {
            return state;
        }

        return gameObject.AddComponent<MemoryItemInteractionState>();
    }

    private void SetInteractionState(MemoryItemInteractionStateId newState, string reason)
    {
        if (interactionState == null)
        {
            interactionState = EnsureInteractionStateComponent();
        }

        if (interactionState != null)
        {
            interactionState.SetState(newState, reason);
        }
    }
}
