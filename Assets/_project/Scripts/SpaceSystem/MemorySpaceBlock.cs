using System;
using System.Collections.Generic;
using UnityEngine;

public class MemorySpaceBlock : MonoBehaviour
{
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
