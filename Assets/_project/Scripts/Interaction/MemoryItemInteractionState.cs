using UnityEngine;

public enum MemoryItemInteractionStateId
{
    Idle,
    HoveredRemote,
    ArmedRemote,
    PullingToHand,
    Catchable,
    Held,
    Inspecting,
    Dropped,
    SnappingToSlot,
    Respawning
}

[DisallowMultipleComponent]
public class MemoryItemInteractionState : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField] private bool logStateChanges = false;

    [Header("Runtime State")]
    [SerializeField] private MemoryItemInteractionStateId currentState = MemoryItemInteractionStateId.Idle;
    [SerializeField] private MemoryItemInteractionStateId previousState = MemoryItemInteractionStateId.Idle;

    public MemoryItemInteractionStateId CurrentState => currentState;
    public MemoryItemInteractionStateId PreviousState => previousState;
    public bool LogStateChanges
    {
        get => logStateChanges;
        set => logStateChanges = value;
    }

    public bool IsGrabAllowed =>
        currentState == MemoryItemInteractionStateId.Idle
        || currentState == MemoryItemInteractionStateId.HoveredRemote
        || currentState == MemoryItemInteractionStateId.ArmedRemote
        || currentState == MemoryItemInteractionStateId.Catchable
        || currentState == MemoryItemInteractionStateId.Dropped;

    public bool IsRemotePullAllowed =>
        currentState == MemoryItemInteractionStateId.Idle
        || currentState == MemoryItemInteractionStateId.HoveredRemote
        || currentState == MemoryItemInteractionStateId.ArmedRemote
        || currentState == MemoryItemInteractionStateId.Dropped;

    public bool IsPlacementAllowed =>
        currentState == MemoryItemInteractionStateId.Idle
        || currentState == MemoryItemInteractionStateId.Dropped;

    public bool IsRespawnAllowed =>
        currentState == MemoryItemInteractionStateId.Idle
        || currentState == MemoryItemInteractionStateId.Dropped;

    public bool IsGazeInspectAllowed => currentState == MemoryItemInteractionStateId.Held;

    public bool IsPhysicsControlLocked =>
        currentState == MemoryItemInteractionStateId.PullingToHand
        || currentState == MemoryItemInteractionStateId.Inspecting
        || currentState == MemoryItemInteractionStateId.SnappingToSlot
        || currentState == MemoryItemInteractionStateId.Respawning;

    public bool SetState(MemoryItemInteractionStateId newState, string reason = null)
    {
        if (currentState == newState)
        {
            return false;
        }

        previousState = currentState;
        currentState = newState;

        if (logStateChanges)
        {
            string suffix = string.IsNullOrWhiteSpace(reason) ? string.Empty : $" ({reason})";
            Debug.Log(
                $"[{nameof(MemoryItemInteractionState)}] {name}: {previousState} -> {currentState}{suffix}",
                this);
        }

        return true;
    }
}
