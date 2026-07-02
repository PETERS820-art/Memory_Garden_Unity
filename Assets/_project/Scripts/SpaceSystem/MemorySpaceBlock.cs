using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class MemorySpaceBlock : MonoBehaviour
{
    private const string BlockBoundsName = "_BlockBounds";
    private const float DefaultBlockHeight = 2.5f;

    public string spaceBlockId;
    public SpaceBlockType spaceBlockType = SpaceBlockType.Room;
    public int widthUnits = 5;
    public int depthUnits = 5;
    public SpaceSegmentKit segmentKit;
    public SpaceBlockDefinition blockDefinition;
    public List<WallSegmentSlot> wallSegments = new List<WallSegmentSlot>();

    [ContextMenu("Auto Collect Wall Segments")]
    public void AutoCollectWallSegments()
    {
        WallSegmentSlot[] slots = GetComponentsInChildren<WallSegmentSlot>(true);
        Array.Sort(slots, CompareSlots);

        wallSegments.Clear();
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null)
            {
                wallSegments.Add(slots[i]);
            }
        }
    }

    public WallSegmentSlot GetWallSegment(WallSide side, int index)
    {
        for (int i = 0; i < wallSegments.Count; i++)
        {
            WallSegmentSlot slot = wallSegments[i];
            if (slot == null)
            {
                continue;
            }

            if (slot.side == side && slot.segmentIndex == index)
            {
                return slot;
            }
        }

        return null;
    }

    [ContextMenu("Validate Block")]
    public void ValidateBlock()
    {
        AutoCollectWallSegments();
        List<string> messages = GetValidationMessages();
        if (messages.Count == 0)
        {
            Debug.Log($"[MemorySpaceBlock] {name} validation passed.");
            return;
        }

        for (int i = 0; i < messages.Count; i++)
        {
            Debug.LogWarning($"[MemorySpaceBlock] {name}: {messages[i]}", this);
        }
    }

    [ContextMenu("Normalize Wall Slot Roots")]
    public void NormalizeWallSlotRoots()
    {
        AutoCollectWallSegments();
        for (int i = 0; i < wallSegments.Count; i++)
        {
            WallSegmentSlot slot = wallSegments[i];
            if (slot == null)
            {
                continue;
            }

            slot.NormalizeSlotTransformState();
        }
    }

    [ContextMenu("Ensure Block Bounds")]
    public void EnsureBlockBounds()
    {
        GetOrCreateBlockBoundsCollider();
    }

    public BoxCollider GetOrCreateBlockBoundsCollider()
    {
        Transform boundsTransform = transform.Find(BlockBoundsName);
        if (boundsTransform == null)
        {
            GameObject boundsObject = new GameObject(BlockBoundsName);
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                Undo.RegisterCreatedObjectUndo(boundsObject, "Create Block Bounds");
                Undo.SetTransformParent(boundsObject.transform, transform, "Create Block Bounds");
            }
            else
#endif
            {
                boundsObject.transform.SetParent(transform, false);
            }

            boundsTransform = boundsObject.transform;
        }

        boundsTransform.localPosition = Vector3.zero;
        boundsTransform.localRotation = Quaternion.identity;
        boundsTransform.localScale = Vector3.one;

        BoxCollider boundsCollider = boundsTransform.GetComponent<BoxCollider>();
        if (boundsCollider == null)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                boundsCollider = Undo.AddComponent<BoxCollider>(boundsTransform.gameObject);
            }
            else
#endif
            {
                boundsCollider = boundsTransform.gameObject.AddComponent<BoxCollider>();
            }
        }

        boundsCollider.isTrigger = true;
        UpdateBlockBoundsCollider(boundsCollider);
        return boundsCollider;
    }

    public bool TryGetWorldBounds(out Bounds worldBounds)
    {
        BoxCollider boundsCollider = GetOrCreateBlockBoundsCollider();
        if (boundsCollider == null)
        {
            worldBounds = default;
            return false;
        }

        worldBounds = GetWorldBoundsFromLocalBox(
            boundsCollider.center,
            boundsCollider.size,
            transform.position,
            transform.rotation);
        return true;
    }

    public static Bounds GetWorldBoundsFromLocalBox(
        Vector3 localCenter,
        Vector3 localSize,
        Vector3 worldPosition,
        Quaternion worldRotation)
    {
        Vector3 worldCenter = worldPosition + (worldRotation * localCenter);
        Vector3 halfSize = localSize * 0.5f;

        Vector3 axisX = worldRotation * new Vector3(halfSize.x, 0f, 0f);
        Vector3 axisY = worldRotation * new Vector3(0f, halfSize.y, 0f);
        Vector3 axisZ = worldRotation * new Vector3(0f, 0f, halfSize.z);

        Vector3 worldExtents = new Vector3(
            Mathf.Abs(axisX.x) + Mathf.Abs(axisY.x) + Mathf.Abs(axisZ.x),
            Mathf.Abs(axisX.y) + Mathf.Abs(axisY.y) + Mathf.Abs(axisZ.y),
            Mathf.Abs(axisX.z) + Mathf.Abs(axisY.z) + Mathf.Abs(axisZ.z));

        return new Bounds(worldCenter, worldExtents * 2f);
    }

    public List<string> GetValidationMessages()
    {
        List<string> messages = new List<string>();

        if (segmentKit == null)
        {
            messages.Add("Missing SegmentKit.");
        }

        if (widthUnits <= 0)
        {
            messages.Add("Width Units must be greater than 0.");
        }

        if (depthUnits <= 0)
        {
            messages.Add("Depth Units must be greater than 0.");
        }

        if (segmentKit != null && !HasCategorySegment(segmentKit, SegmentCategory.Floor))
        {
            messages.Add("Missing floor segment in SegmentKit.");
        }

        if (segmentKit != null && !HasCategorySegment(segmentKit, SegmentCategory.Wall))
        {
            messages.Add("Missing wall segment in SegmentKit.");
        }

        HashSet<string> seenSlotKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < wallSegments.Count; i++)
        {
            WallSegmentSlot slot = wallSegments[i];
            if (slot == null)
            {
                continue;
            }

            string slotKey = $"{slot.side}:{slot.segmentIndex}";
            if (!seenSlotKeys.Add(slotKey))
            {
                messages.Add($"Duplicate slot side/index: {slot.side} [{slot.segmentIndex}]");
            }

            int expectedCount = GetExpectedSlotCount(slot.side);
            if (slot.segmentIndex < 0 || slot.segmentIndex >= expectedCount)
            {
                messages.Add($"Segment index out of range for {slot.side}: {slot.segmentIndex}");
            }

            if (slot.segmentRoot == null)
            {
                messages.Add($"Missing SegmentRoot on slot {slot.side} [{slot.segmentIndex}]");
            }

            if (segmentKit == null || string.IsNullOrWhiteSpace(slot.segmentId))
            {
                continue;
            }

            SpaceSegmentDefinition segmentDefinition = segmentKit.GetSegment(slot.segmentId);
            if (segmentDefinition == null)
            {
                messages.Add($"Missing wall segment definition: {slot.segmentId}");
                continue;
            }

            if (segmentDefinition.category != SegmentCategory.Wall)
            {
                messages.Add(
                    $"Unsupported segment category on slot {slot.side} [{slot.segmentIndex}]: {segmentDefinition.category}");
            }

            if (segmentDefinition.prefab == null)
            {
                messages.Add($"Missing segment prefab for definition: {slot.segmentId}");
            }
        }

        return messages;
    }

    private void UpdateBlockBoundsCollider(BoxCollider boundsCollider)
    {
        if (boundsCollider == null)
        {
            return;
        }

        if (TryGetCombinedRendererBounds(out Bounds worldBounds))
        {
            Vector3 localCenter = transform.InverseTransformPoint(worldBounds.center);
            Vector3 localSizeVector = transform.InverseTransformVector(worldBounds.size);
            boundsCollider.center = localCenter;
            boundsCollider.size = new Vector3(
                Mathf.Max(0.1f, Mathf.Abs(localSizeVector.x)),
                Mathf.Max(0.1f, Mathf.Abs(localSizeVector.y)),
                Mathf.Max(0.1f, Mathf.Abs(localSizeVector.z)));
            return;
        }

        float height = GetEstimatedBlockHeight();
        boundsCollider.center = new Vector3(0f, height * 0.5f, 0f);
        boundsCollider.size = new Vector3(
            Mathf.Max(1f, widthUnits),
            Mathf.Max(0.1f, height),
            Mathf.Max(1f, depthUnits));
    }

    private bool TryGetCombinedRendererBounds(out Bounds combinedBounds)
    {
        combinedBounds = default;
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        bool foundBounds = false;
        Transform boundsTransform = transform.Find(BlockBoundsName);

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            if (boundsTransform != null && renderer.transform.IsChildOf(boundsTransform))
            {
                continue;
            }

            if (!foundBounds)
            {
                combinedBounds = renderer.bounds;
                foundBounds = true;
            }
            else
            {
                combinedBounds.Encapsulate(renderer.bounds);
            }
        }

        return foundBounds;
    }

    private float GetEstimatedBlockHeight()
    {
        float bestHeight = 0f;
        if (segmentKit != null && segmentKit.segments != null)
        {
            for (int i = 0; i < segmentKit.segments.Count; i++)
            {
                SpaceSegmentDefinition definition = segmentKit.segments[i];
                if (definition != null && definition.category == SegmentCategory.Wall)
                {
                    bestHeight = Mathf.Max(bestHeight, definition.height);
                }
            }
        }

        return bestHeight > 0f ? bestHeight : DefaultBlockHeight;
    }

    private int GetExpectedSlotCount(WallSide side)
    {
        switch (side)
        {
            case WallSide.North:
            case WallSide.South:
                return Mathf.Max(0, widthUnits);
            case WallSide.East:
            case WallSide.West:
                return Mathf.Max(0, depthUnits - 2);
            default:
                return 0;
        }
    }

    private static bool HasCategorySegment(SpaceSegmentKit kit, SegmentCategory category)
    {
        if (kit == null || kit.segments == null)
        {
            return false;
        }

        for (int i = 0; i < kit.segments.Count; i++)
        {
            SpaceSegmentDefinition definition = kit.segments[i];
            if (definition != null && definition.category == category)
            {
                return true;
            }
        }

        return false;
    }

    private static int CompareSlots(WallSegmentSlot a, WallSegmentSlot b)
    {
        if (a == null && b == null)
        {
            return 0;
        }

        if (a == null)
        {
            return 1;
        }

        if (b == null)
        {
            return -1;
        }

        int sideCompare = a.side.CompareTo(b.side);
        if (sideCompare != 0)
        {
            return sideCompare;
        }

        return a.segmentIndex.CompareTo(b.segmentIndex);
    }
}
