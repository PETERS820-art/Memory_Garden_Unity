using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class MemoryDisplayFurniture : MonoBehaviour
{
    private const float FurnitureGizmoSphereRadius = 0.035f;
    private const string PlacementBoundsObjectName = "_PlacementBounds";

    [Header("Furniture Identity")]
    [SerializeField] private string furnitureId;
    [SerializeField] private FurnitureType furnitureType = FurnitureType.Custom;

    [Header("Furniture Rules")]
    [SerializeField] private bool editableInArrangementMode = true;
    [SerializeField] private bool lockedInExploreMode = true;

    [Header("Placement Bounds")]
    [SerializeField] private BoxCollider placementBoundsCollider;
    [SerializeField] private bool usePlacementBoundsForSlots = true;
    [SerializeField] private bool createBlockingCollider = true;

    [Header("Slot References")]
    [SerializeField] private List<MemoryDisplaySlot> slots = new List<MemoryDisplaySlot>();

    public string FurnitureId => furnitureId;
    public FurnitureType Type => furnitureType;
    public bool EditableInArrangementMode => editableInArrangementMode;
    public bool LockedInExploreMode => lockedInExploreMode;
    public BoxCollider PlacementBoundsCollider => placementBoundsCollider;
    public bool UsePlacementBoundsForSlots => usePlacementBoundsForSlots;
    public bool CreateBlockingCollider => createBlockingCollider;
    public IReadOnlyList<MemoryDisplaySlot> Slots => slots;

    public void AutoCollectSlots()
    {
        MemoryDisplaySlot[] collectedSlots = GetComponentsInChildren<MemoryDisplaySlot>(true);

        if (slots == null)
        {
            slots = new List<MemoryDisplaySlot>(collectedSlots.Length);
        }
        else
        {
            slots.Clear();
        }

        for (int i = 0; i < collectedSlots.Length; i++)
        {
            MemoryDisplaySlot slot = collectedSlots[i];
            if (slot != null)
            {
                slots.Add(slot);
            }
        }
    }

    public List<MemoryDisplaySlot> GetAvailableSlots()
    {
        if (slots == null || slots.Count == 0)
        {
            AutoCollectSlots();
        }

        List<MemoryDisplaySlot> availableSlots = new List<MemoryDisplaySlot>();
        for (int i = 0; i < slots.Count; i++)
        {
            MemoryDisplaySlot slot = slots[i];
            if (slot == null)
            {
                continue;
            }

            if (!slot.IsOccupied)
            {
                availableSlots.Add(slot);
            }
        }

        return availableSlots;
    }

    public MemoryDisplaySlot GetSlotById(string slotId)
    {
        if (string.IsNullOrWhiteSpace(slotId))
        {
            return null;
        }

        if (slots == null || slots.Count == 0)
        {
            AutoCollectSlots();
        }

        for (int i = 0; i < slots.Count; i++)
        {
            MemoryDisplaySlot slot = slots[i];
            if (slot == null)
            {
                continue;
            }

            if (string.Equals(slot.SlotId, slotId, StringComparison.OrdinalIgnoreCase))
            {
                return slot;
            }
        }

        return null;
    }

    public Bounds GetPlacementBounds()
    {
        if (usePlacementBoundsForSlots)
        {
            AutoAssignPlacementBounds();
            if (placementBoundsCollider != null)
            {
                return placementBoundsCollider.bounds;
            }
        }

        if (TryGetCombinedRendererBounds(out Bounds rendererBounds))
        {
            return rendererBounds;
        }

        return new Bounds(transform.position, Vector3.zero);
    }

    public void AutoAssignPlacementBounds()
    {
        if (placementBoundsCollider != null)
        {
            return;
        }

        Transform placementBoundsTransform = transform.Find(PlacementBoundsObjectName);
        if (placementBoundsTransform != null)
        {
            placementBoundsCollider = placementBoundsTransform.GetComponent<BoxCollider>();
            if (placementBoundsCollider != null)
            {
                return;
            }
        }

        BoxCollider[] colliders = GetComponentsInChildren<BoxCollider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            BoxCollider collider = colliders[i];
            if (collider != null && string.Equals(collider.gameObject.name, PlacementBoundsObjectName, StringComparison.Ordinal))
            {
                placementBoundsCollider = collider;
                return;
            }
        }
    }

    public void AutoFitPlacementBoundsFromRenderers()
    {
        AutoAssignPlacementBounds();
        if (placementBoundsCollider == null)
        {
            return;
        }

        if (!TryGetCombinedLocalRendererBounds(out Bounds localBounds))
        {
            return;
        }

        if (!TrySetColliderToFurnitureLocalBounds(placementBoundsCollider, localBounds))
        {
            return;
        }
    }

    private void Reset()
    {
        if (string.IsNullOrWhiteSpace(furnitureId))
        {
            furnitureId = gameObject.name;
        }

        AutoAssignPlacementBounds();
        AutoCollectSlots();
    }

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(furnitureId))
        {
            furnitureId = gameObject.name;
        }

        AutoAssignPlacementBounds();

        if (slots == null)
        {
            slots = new List<MemoryDisplaySlot>();
            return;
        }

        for (int i = slots.Count - 1; i >= 0; i--)
        {
            if (slots[i] == null)
            {
                slots.RemoveAt(i);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 1f);
        Gizmos.DrawSphere(transform.position, FurnitureGizmoSphereRadius);

        if (placementBoundsCollider != null)
        {
            Matrix4x4 previousMatrix = Gizmos.matrix;
            Gizmos.matrix = placementBoundsCollider.transform.localToWorldMatrix;
            Gizmos.color = new Color(0.2f, 1f, 0.45f, 1f);
            Gizmos.DrawWireCube(placementBoundsCollider.center, placementBoundsCollider.size);
            Gizmos.matrix = previousMatrix;
        }
        else if (TryGetCombinedRendererBounds(out Bounds bounds))
        {
            Gizmos.color = new Color(0.2f, 1f, 0.45f, 1f);
            Gizmos.DrawWireCube(bounds.center, bounds.size);
        }

        if (slots == null || slots.Count == 0)
        {
            AutoCollectSlots();
        }

        for (int i = 0; i < slots.Count; i++)
        {
            MemoryDisplaySlot slot = slots[i];
            if (slot == null)
            {
                continue;
            }

            Gizmos.DrawLine(transform.position, slot.transform.position);
        }
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

    private bool TryGetCombinedLocalRendererBounds(out Bounds localBounds)
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        Vector3 min = Vector3.zero;
        Vector3 max = Vector3.zero;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            Bounds rendererBounds = renderer.bounds;
            Vector3 center = rendererBounds.center;
            Vector3 extents = rendererBounds.extents;

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
                        Vector3 localCorner = transform.InverseTransformPoint(worldCorner);

                        if (!hasBounds)
                        {
                            min = localCorner;
                            max = localCorner;
                            hasBounds = true;
                            continue;
                        }

                        min = Vector3.Min(min, localCorner);
                        max = Vector3.Max(max, localCorner);
                    }
                }
            }
        }

        if (!hasBounds)
        {
            localBounds = default;
            return false;
        }

        localBounds = new Bounds((min + max) * 0.5f, max - min);
        return true;
    }

    private bool TrySetColliderToFurnitureLocalBounds(BoxCollider collider, Bounds furnitureLocalBounds)
    {
        if (collider == null)
        {
            return false;
        }

        Vector3[] worldCorners = new Vector3[8];
        Vector3 center = furnitureLocalBounds.center;
        Vector3 extents = furnitureLocalBounds.extents;
        int index = 0;

        for (int x = -1; x <= 1; x += 2)
        {
            for (int y = -1; y <= 1; y += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 localCorner = center + new Vector3(
                        extents.x * x,
                        extents.y * y,
                        extents.z * z);
                    worldCorners[index] = transform.TransformPoint(localCorner);
                    index++;
                }
            }
        }

        Vector3 min = Vector3.zero;
        Vector3 max = Vector3.zero;
        bool hasCorner = false;

        for (int i = 0; i < worldCorners.Length; i++)
        {
            Vector3 colliderLocalCorner = collider.transform.InverseTransformPoint(worldCorners[i]);
            if (!hasCorner)
            {
                min = colliderLocalCorner;
                max = colliderLocalCorner;
                hasCorner = true;
                continue;
            }

            min = Vector3.Min(min, colliderLocalCorner);
            max = Vector3.Max(max, colliderLocalCorner);
        }

        if (!hasCorner)
        {
            return false;
        }

        collider.center = (min + max) * 0.5f;
        collider.size = Vector3.Max(max - min, Vector3.one * 0.01f);
        return true;
    }
}
