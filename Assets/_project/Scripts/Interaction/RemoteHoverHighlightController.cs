using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

[DisallowMultipleComponent]
[RequireComponent(typeof(XRRayInteractor))]
public class RemoteHoverHighlightController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private XRRayInteractor rayInteractor;

    [Header("Behavior")]
    [SerializeField] private bool autoAddHighlightComponent = true;
    [SerializeField] private bool useRaycastPolling = true;

    private MemoryObject currentMemoryObject;
    private MemoryItemInteractionState currentState;
    private MemoryItemHighlight currentHighlight;

    private void Reset()
    {
        rayInteractor = GetComponent<XRRayInteractor>();
    }

    private void Awake()
    {
        if (rayInteractor == null)
        {
            rayInteractor = GetComponent<XRRayInteractor>();
        }
    }

    private void OnEnable()
    {
        if (rayInteractor == null)
        {
            rayInteractor = GetComponent<XRRayInteractor>();
        }

        if (rayInteractor == null)
        {
            Debug.LogError($"[{nameof(RemoteHoverHighlightController)}] Missing {nameof(XRRayInteractor)} on {name}.", this);
            enabled = false;
            return;
        }

        rayInteractor.hoverEntered.AddListener(OnHoverEntered);
        rayInteractor.hoverExited.AddListener(OnHoverExited);
    }

    private void OnDisable()
    {
        if (rayInteractor != null)
        {
            rayInteractor.hoverEntered.RemoveListener(OnHoverEntered);
            rayInteractor.hoverExited.RemoveListener(OnHoverExited);
        }

        ClearTrackedHover(resetState: true);
    }

    private void Update()
    {
        if (useRaycastPolling)
        {
            RefreshHoverFromRaycast();
        }

        if (currentState == null || currentHighlight == null)
        {
            return;
        }

        if (IsHighlightAllowedState(currentState.CurrentState))
        {
            return;
        }

        currentHighlight.ClearHighlight();

        if (currentState.CurrentState == MemoryItemInteractionStateId.HoveredRemote)
        {
            currentState.SetState(MemoryItemInteractionStateId.Idle, "Remote hover invalidated");
        }

        currentMemoryObject = null;
        currentState = null;
        currentHighlight = null;
    }

    private void RefreshHoverFromRaycast()
    {
        if (rayInteractor == null || !rayInteractor.enabled)
        {
            ClearTrackedHover(resetState: true);
            return;
        }

        if (!rayInteractor.TryGetCurrent3DRaycastHit(out RaycastHit hit) || hit.collider == null)
        {
            ClearTrackedHover(resetState: true);
            return;
        }

        if (!TryResolveMemoryItem(hit.collider.transform, out MemoryObject memoryObject, out MemoryItemInteractionState state, out MemoryItemHighlight highlight))
        {
            ClearTrackedHover(resetState: true);
            return;
        }

        if (!IsHighlightAllowedState(state.CurrentState))
        {
            ClearTrackedHover(resetState: false);
            return;
        }

        if (currentMemoryObject == memoryObject)
        {
            currentHighlight = highlight;
            if (!highlight.IsHighlighted)
            {
                highlight.SetHoverHighlight(true);
            }

            return;
        }

        ClearTrackedHover(resetState: true);

        currentMemoryObject = memoryObject;
        currentState = state;
        currentHighlight = highlight;

        if (state.CurrentState == MemoryItemInteractionStateId.Idle
            || state.CurrentState == MemoryItemInteractionStateId.Dropped)
        {
            state.SetState(MemoryItemInteractionStateId.HoveredRemote, "Remote hover raycast");
        }

        highlight.SetHoverHighlight(true);
    }

    private void OnHoverEntered(HoverEnterEventArgs args)
    {
        if (!TryResolveMemoryItem(args, out MemoryObject memoryObject, out MemoryItemInteractionState state, out MemoryItemHighlight highlight))
        {
            return;
        }

        if (!IsHighlightAllowedState(state.CurrentState))
        {
            return;
        }

        if (currentMemoryObject != null && currentMemoryObject != memoryObject)
        {
            ClearTrackedHover(resetState: true);
        }

        currentMemoryObject = memoryObject;
        currentState = state;
        currentHighlight = highlight;

        if (state.CurrentState == MemoryItemInteractionStateId.Idle
            || state.CurrentState == MemoryItemInteractionStateId.Dropped)
        {
            state.SetState(MemoryItemInteractionStateId.HoveredRemote, "Remote hover enter");
        }

        highlight.SetHoverHighlight(true);
    }

    private void OnHoverExited(HoverExitEventArgs args)
    {
        if (!TryResolveMemoryItem(args, out MemoryObject memoryObject, out MemoryItemInteractionState state, out MemoryItemHighlight highlight))
        {
            return;
        }

        highlight.ClearHighlight();

        if (state.CurrentState == MemoryItemInteractionStateId.HoveredRemote)
        {
            state.SetState(MemoryItemInteractionStateId.Idle, "Remote hover exit");
        }

        if (currentMemoryObject == memoryObject)
        {
            currentMemoryObject = null;
            currentState = null;
            currentHighlight = null;
        }
    }

    private bool TryResolveMemoryItem(
        BaseInteractionEventArgs args,
        out MemoryObject memoryObject,
        out MemoryItemInteractionState state,
        out MemoryItemHighlight highlight)
    {
        memoryObject = null;
        state = null;
        highlight = null;

        if (args?.interactableObject == null)
        {
            return false;
        }

        return TryResolveMemoryItem(args.interactableObject.transform, out memoryObject, out state, out highlight);
    }

    private bool TryResolveMemoryItem(
        Transform interactableTransform,
        out MemoryObject memoryObject,
        out MemoryItemInteractionState state,
        out MemoryItemHighlight highlight)
    {
        memoryObject = null;
        state = null;
        highlight = null;

        if (interactableTransform == null)
        {
            return false;
        }

        memoryObject = interactableTransform.GetComponentInParent<MemoryObject>();
        if (memoryObject == null)
        {
            return false;
        }

        state = memoryObject.InteractionState != null
            ? memoryObject.InteractionState
            : memoryObject.GetComponent<MemoryItemInteractionState>();

        if (state == null)
        {
            return false;
        }

        highlight = memoryObject.GetComponent<MemoryItemHighlight>();
        if (highlight == null && autoAddHighlightComponent)
        {
            highlight = memoryObject.gameObject.AddComponent<MemoryItemHighlight>();
        }

        return highlight != null;
    }

    private void ClearTrackedHover(bool resetState)
    {
        if (currentHighlight != null)
        {
            currentHighlight.ClearHighlight();
        }

        if (resetState && currentState != null && currentState.CurrentState == MemoryItemInteractionStateId.HoveredRemote)
        {
            currentState.SetState(MemoryItemInteractionStateId.Idle, "Remote hover cleared");
        }

        currentMemoryObject = null;
        currentState = null;
        currentHighlight = null;
    }

    private static bool IsHighlightAllowedState(MemoryItemInteractionStateId state)
    {
        return state == MemoryItemInteractionStateId.Idle
            || state == MemoryItemInteractionStateId.Dropped
            || state == MemoryItemInteractionStateId.HoveredRemote;
    }
}
