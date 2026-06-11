#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(WallSegmentSlot))]
public class WallSegmentSlotEditor : Editor
{
    private struct SegmentMenuEntry
    {
        public string Label;
        public SpaceSegmentDefinition Definition;
    }

    private List<SegmentMenuEntry> cachedEntries = new List<SegmentMenuEntry>();
    private int selectedSegmentMenuIndex;

    public override void OnInspectorGUI()
    {
        WallSegmentSlot slot = (WallSegmentSlot)target;
        MemorySpaceBlock block = slot != null ? slot.GetComponentInParent<MemorySpaceBlock>() : null;
        SpaceSegmentKit kit = block != null ? block.segmentKit : null;
        SpaceSegmentDefinition currentDefinition = kit != null ? kit.GetSegment(slot.segmentId) : null;

        DrawSlotSummary(slot, currentDefinition, kit);
        if (kit == null)
        {
            EditorGUILayout.HelpBox("This slot is missing a SegmentKit source.", MessageType.Info);
            return;
        }

        RebuildMenuEntries(slot, kit, currentDefinition);
        if (cachedEntries.Count == 0)
        {
            EditorGUILayout.HelpBox("No compatible segment definitions were found for this slot.", MessageType.Warning);
            return;
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Change Segment", EditorStyles.boldLabel);
        selectedSegmentMenuIndex = Mathf.Clamp(selectedSegmentMenuIndex, 0, Mathf.Max(0, cachedEntries.Count - 1));

        if (GUILayout.Button(cachedEntries[selectedSegmentMenuIndex].Label, EditorStyles.popup))
        {
            ShowSegmentMenu(slot);
        }
    }

    private void DrawSlotSummary(
        WallSegmentSlot slot,
        SpaceSegmentDefinition currentDefinition,
        SpaceSegmentKit kit)
    {
        EditorGUILayout.LabelField("Wall Segment Slot", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.EnumPopup("Side", slot.side);
            EditorGUILayout.IntField("Index", slot.segmentIndex);
            EditorGUILayout.Toggle("Allow Connection", slot.allowConnection);
            EditorGUILayout.TextField(
                "Current Segment",
                currentDefinition != null ? BuildMenuPath(currentDefinition) : (slot.segmentId ?? string.Empty));
        }
    }

    private void RebuildMenuEntries(
        WallSegmentSlot slot,
        SpaceSegmentKit kit,
        SpaceSegmentDefinition currentDefinition)
    {
        cachedEntries.Clear();
        if (kit == null || kit.segments == null)
        {
            return;
        }

        SegmentCategory targetCategory = currentDefinition != null ? currentDefinition.category : SegmentCategory.Wall;
        string currentId = currentDefinition != null ? currentDefinition.segmentId : slot.segmentId;
        Vector2 targetSize = currentDefinition != null ? currentDefinition.sizeXZ : GetDefaultSizeForSlot(slot);
        float targetHeight = currentDefinition != null ? currentDefinition.height : GetDefaultHeightForSlot(currentDefinition);

        int currentIndex = 0;
        for (int i = 0; i < kit.segments.Count; i++)
        {
            SpaceSegmentDefinition definition = kit.segments[i];
            if (!IsCompatibleForSlot(slot, definition, targetCategory, targetSize, targetHeight))
            {
                continue;
            }

            cachedEntries.Add(new SegmentMenuEntry
            {
                Label = BuildMenuPath(definition),
                Definition = definition
            });

            if (definition != null && string.Equals(definition.segmentId, currentId, StringComparison.OrdinalIgnoreCase))
            {
                currentIndex = cachedEntries.Count - 1;
            }
        }

        cachedEntries.Sort((a, b) => string.Compare(a.Label, b.Label, StringComparison.OrdinalIgnoreCase));
        for (int i = 0; i < cachedEntries.Count; i++)
        {
            if (cachedEntries[i].Definition != null
                && string.Equals(cachedEntries[i].Definition.segmentId, currentId, StringComparison.OrdinalIgnoreCase))
            {
                currentIndex = i;
                break;
            }
        }

        selectedSegmentMenuIndex = currentIndex;
    }

    private void ShowSegmentMenu(WallSegmentSlot slot)
    {
        GenericMenu menu = new GenericMenu();
        for (int i = 0; i < cachedEntries.Count; i++)
        {
            int entryIndex = i;
            SegmentMenuEntry entry = cachedEntries[i];
            bool isSelected = entryIndex == selectedSegmentMenuIndex;
            menu.AddItem(
                new GUIContent(entry.Label),
                isSelected,
                () =>
                {
                    selectedSegmentMenuIndex = entryIndex;
                    ApplyDefinition(slot, entry.Definition);
                });
        }

        menu.ShowAsContext();
    }

    private static bool IsCompatibleForSlot(
        WallSegmentSlot slot,
        SpaceSegmentDefinition definition,
        SegmentCategory targetCategory,
        Vector2 targetSize,
        float targetHeight)
    {
        if (slot == null || definition == null)
        {
            return false;
        }

        if (definition.category != targetCategory)
        {
            return false;
        }

        if (targetCategory == SegmentCategory.Wall && !definition.canBeWallSegment)
        {
            return false;
        }

        if (targetCategory == SegmentCategory.OpeningOverlay && !definition.canBeOpeningOverlay)
        {
            return false;
        }

        if (slot.cornerPlacement != CornerPlacement.None)
        {
            return definition.variant == SegmentVariant.Corner;
        }

        if (definition.variant == SegmentVariant.Corner)
        {
            return false;
        }

        if (!Approximately(definition.sizeXZ.x, targetSize.x) || !Approximately(definition.sizeXZ.y, targetSize.y))
        {
            return false;
        }

        if (targetHeight > 0f && definition.height > 0f && !Approximately(definition.height, targetHeight))
        {
            return false;
        }

        return true;
    }

    private static Vector2 GetDefaultSizeForSlot(WallSegmentSlot slot)
    {
        return Vector2.one;
    }

    private static float GetDefaultHeightForSlot(SpaceSegmentDefinition definition)
    {
        return definition != null && definition.height > 0f ? definition.height : 2.5f;
    }

    private static string BuildMenuPath(SpaceSegmentDefinition definition)
    {
        string category = definition.category.ToString().ToLowerInvariant();
        string style = string.IsNullOrWhiteSpace(definition.styleId)
            ? "default"
            : $"{category}_{definition.styleId}".ToLowerInvariant();
        string size = GetDefinitionSizeFolder(definition);
        string leaf = GetDefinitionLeafName(definition);
        return $"{category}/{style}/{size}/{leaf}";
    }

    private static string GetDefinitionLeafName(SpaceSegmentDefinition definition)
    {
        if (definition == null)
        {
            return "none";
        }

        if (definition.variant == SegmentVariant.Default || definition.variant == SegmentVariant.Solid)
        {
            if (definition.category == SegmentCategory.OpeningOverlay
                && definition.segmentId != null
                && definition.segmentId.IndexOf("doorway", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "doorway";
            }

            return "solid";
        }

        return definition.variant.ToString().ToLowerInvariant();
    }

    private static string GetDefinitionSizeFolder(SpaceSegmentDefinition definition)
    {
        if (definition == null)
        {
            return "1x1";
        }

        float width = Mathf.Max(1f, definition.sizeXZ.x);
        float height = GetDefinitionDisplayHeight(definition);
        return $"{FormatNumber(width)}x{FormatNumber(height)}";
    }

    private static float GetDefinitionDisplayHeight(SpaceSegmentDefinition definition)
    {
        if (definition == null)
        {
            return 1f;
        }

        if (definition.height > 0f)
        {
            return definition.height;
        }

        if (definition.category == SegmentCategory.OpeningOverlay && definition.sizeXZ.y > 1f)
        {
            return definition.sizeXZ.y;
        }

        return definition.category == SegmentCategory.Wall ? 1f : Mathf.Max(1f, definition.sizeXZ.y);
    }

    private static string FormatNumber(float value)
    {
        return value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static void ApplyDefinition(WallSegmentSlot slot, SpaceSegmentDefinition definition)
    {
        if (slot == null || definition == null)
        {
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(slot.gameObject, "Change Wall Segment");
        slot.SetSegment(definition);
        EditorUtility.SetDirty(slot);

        MemorySpaceBlock block = slot.GetComponentInParent<MemorySpaceBlock>();
        if (block != null)
        {
            EditorUtility.SetDirty(block);
        }
    }

    private static bool Approximately(float a, float b)
    {
        return Mathf.Abs(a - b) <= 0.001f;
    }
}
#endif
