using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class MemoryDisplaySlot : MonoBehaviour
{
    private const float GizmoCubeSize = 0.08f;
    private const float GizmoAxisLength = 0.12f;

    [Header("Slot Identity")]
    [SerializeField] private string slotId = "Slot";
    [SerializeField] private SlotType slotType = SlotType.MediumTabletop;

    [Header("Slot Rules")]
    [SerializeField] private List<ItemSize> acceptedItemSizes = new List<ItemSize>
    {
        ItemSize.Small,
        ItemSize.Medium
    };
    [SerializeField] private float snapRadius = 0.8f;
    [SerializeField] private bool useSmoothSnap = true;
    [SerializeField] private float snapDuration = 0.2f;

    [Header("Occupancy")]
    [SerializeField] private bool isOccupied;
    [SerializeField] private GameObject occupiedItem;

    public string SlotId => slotId;
    public SlotType Type => slotType;
    public IReadOnlyList<ItemSize> AcceptedItemSizes => acceptedItemSizes;
    public float SnapRadius => snapRadius;
    public bool UseSmoothSnap => useSmoothSnap;
    public float SnapDuration => snapDuration;
    public bool IsOccupied => isOccupied;
    public GameObject OccupiedItem => occupiedItem;

    public bool CanAccept(MemoryObject memoryObject)
    {
        if (isOccupied)
        {
            return false;
        }

        if (memoryObject == null || !memoryObject.EnablePlacement || !memoryObject.IsPlacementAllowed)
        {
            return false;
        }

        if (!memoryObject.CanUseSlotType(slotType))
        {
            return false;
        }

        if (acceptedItemSizes == null || acceptedItemSizes.Count == 0)
        {
            return true;
        }

        ItemSize requestedSize = memoryObject.GetPlacementItemSize();
        for (int i = 0; i < acceptedItemSizes.Count; i++)
        {
            if (acceptedItemSizes[i] == requestedSize)
            {
                return true;
            }
        }

        return false;
    }

    public void MarkOccupied(GameObject item)
    {
        occupiedItem = item;
        isOccupied = item != null;
    }

    public void ClearOccupied()
    {
        occupiedItem = null;
        isOccupied = false;
    }

    public Pose GetSnapPose()
    {
        return new Pose(transform.position, transform.rotation);
    }

    private void OnValidate()
    {
        if (occupiedItem == null)
        {
            isOccupied = false;
        }
        else
        {
            isOccupied = true;
        }

        if (acceptedItemSizes == null)
        {
            acceptedItemSizes = new List<ItemSize>();
        }

        snapRadius = Mathf.Max(0f, snapRadius);
        snapDuration = Mathf.Max(0f, snapDuration);
    }

    private void OnDrawGizmos()
    {
        DrawSlotGizmo(Color.cyan);
    }

    private void OnDrawGizmosSelected()
    {
        DrawSlotGizmo(new Color(0.3f, 1f, 1f, 1f));
    }

    private void DrawSlotGizmo(Color color)
    {
        Gizmos.color = color;
        Matrix4x4 previousMatrix = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;

        Gizmos.DrawWireCube(Vector3.zero, Vector3.one * GizmoCubeSize);
        Gizmos.DrawLine(Vector3.zero, Vector3.up * GizmoAxisLength);
        Gizmos.DrawLine(Vector3.zero, Vector3.forward * GizmoAxisLength);
        Gizmos.DrawLine(Vector3.zero, Vector3.right * GizmoAxisLength);

        Gizmos.matrix = previousMatrix;
    }
}
