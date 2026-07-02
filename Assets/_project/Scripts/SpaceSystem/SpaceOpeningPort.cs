using UnityEngine;

[ExecuteAlways]
public class SpaceOpeningPort : MonoBehaviour
{
    public string openingId;
    public SpaceOpeningType openingType = SpaceOpeningType.Doorway;
    public SpaceConnectionKind connectionKind = SpaceConnectionKind.Passage;
    public int widthUnits = 1;
    public float height = 2.5f;
    public WallSide wallSide;
    public Vector2Int gridPosition;
    public Transform connectorAnchor;
    public bool isOccupied;
    public SpaceOpeningPort connectedPort;

    public MemorySpaceBlock OwningBlock
    {
        get { return GetComponentInParent<MemorySpaceBlock>(); }
    }

    public Transform EffectiveAnchor
    {
        get { return connectorAnchor != null ? connectorAnchor : transform; }
    }

    private void OnEnable()
    {
        SpaceConnectionManager manager = SpaceConnectionManager.FindExistingManager();
        if (manager != null)
        {
            manager.RegisterPort(this);
        }
    }

    private void OnDisable()
    {
        SpaceConnectionManager manager = SpaceConnectionManager.FindExistingManager();
        if (manager != null)
        {
            manager.UnregisterPort(this);
        }
    }

    public bool CanConnectTo(SpaceOpeningPort other, bool requireFacing, out string reason)
    {
        reason = string.Empty;
        if (other == null)
        {
            reason = "Port B is missing.";
            return false;
        }

        if (other == this)
        {
            reason = "Select two different doorway ports.";
            return false;
        }

        if (isOccupied || connectedPort != null)
        {
            reason = $"{name} is already occupied.";
            return false;
        }

        if (other.isOccupied || other.connectedPort != null)
        {
            reason = $"{other.name} is already occupied.";
            return false;
        }

        if (openingType != other.openingType)
        {
            reason = "Opening types do not match.";
            return false;
        }

        if (connectionKind != other.connectionKind)
        {
            reason = "Connection kinds do not match.";
            return false;
        }

        if (widthUnits != other.widthUnits)
        {
            reason = "Doorway widths do not match.";
            return false;
        }

        if (Mathf.Abs(height - other.height) > 0.01f)
        {
            reason = "Doorway heights do not match.";
            return false;
        }

        MemorySpaceBlock ownerA = OwningBlock;
        MemorySpaceBlock ownerB = other.OwningBlock;
        if (ownerA == null || ownerB == null)
        {
            reason = "Both ports must belong to baked SpaceBlocks.";
            return false;
        }

        if (ownerA == ownerB)
        {
            reason = "Ports cannot belong to the same SpaceBlock.";
            return false;
        }

        if (requireFacing && !FacesPort(other))
        {
            reason = "Doorways must face each other.";
            return false;
        }

        return true;
    }

    public bool FacesPort(SpaceOpeningPort other)
    {
        if (other == null)
        {
            return false;
        }

        Vector3 forwardA = EffectiveAnchor.forward.normalized;
        Vector3 forwardB = other.EffectiveAnchor.forward.normalized;
        return Vector3.Dot(forwardA, forwardB) <= -0.95f;
    }

    public void SetOccupied(SpaceOpeningPort other)
    {
        isOccupied = other != null;
        connectedPort = other;
    }

    public void ClearOccupied()
    {
        isOccupied = false;
        connectedPort = null;
    }
}
