using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SK_NewSegmentKit", menuName = "Memory Garden/Space Segment Kit")]
public class SpaceSegmentKit : ScriptableObject
{
    private const float SizeTolerance = 0.001f;

    public string kitId;
    public List<SpaceSegmentDefinition> segments = new List<SpaceSegmentDefinition>();

    public SpaceSegmentDefinition GetSegment(string segmentId)
    {
        if (string.IsNullOrWhiteSpace(segmentId) || segments == null)
        {
            return null;
        }

        for (int i = 0; i < segments.Count; i++)
        {
            SpaceSegmentDefinition segment = segments[i];
            if (segment == null || string.IsNullOrWhiteSpace(segment.segmentId))
            {
                continue;
            }

            if (string.Equals(segment.segmentId, segmentId, StringComparison.OrdinalIgnoreCase))
            {
                return segment;
            }
        }

        return null;
    }

    public SpaceSegmentDefinition FindSegment(
        SegmentCategory category,
        string styleId,
        Vector2 sizeXZ,
        float height,
        SegmentVariant variant)
    {
        if (segments == null)
        {
            return null;
        }

        for (int i = 0; i < segments.Count; i++)
        {
            SpaceSegmentDefinition segment = segments[i];
            if (segment == null)
            {
                continue;
            }

            if (segment.category != category)
            {
                continue;
            }

            if (!string.Equals(segment.styleId ?? string.Empty, styleId ?? string.Empty, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!Approximately(segment.sizeXZ.x, sizeXZ.x) || !Approximately(segment.sizeXZ.y, sizeXZ.y))
            {
                continue;
            }

            if (!Approximately(segment.height, height))
            {
                continue;
            }

            if (segment.variant != variant)
            {
                continue;
            }

            return segment;
        }

        return null;
    }

    private static bool Approximately(float a, float b)
    {
        return Mathf.Abs(a - b) <= SizeTolerance;
    }
}
