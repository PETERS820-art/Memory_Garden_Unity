using UnityEngine;

public enum SegmentCategory
{
    Floor,
    Wall,
    Ceiling,
    Beam,
    OpeningOverlay,
    Threshold,
    Corner,
    Custom
}

public enum SegmentVariant
{
    Solid,
    Empty,
    Corner,
    DoorCenter,
    DoorLeft,
    DoorRight,
    LowWall,
    Lattice,
    Default,
    Custom
}

[CreateAssetMenu(fileName = "SD_NewSpaceSegment", menuName = "Memory Garden/Space Segment Definition")]
public class SpaceSegmentDefinition : ScriptableObject
{
    [Header("Identity")]
    public string segmentId;
    public SegmentCategory category;
    public string styleId;

    [Header("Dimensions")]
    public Vector2 sizeXZ = Vector2.one;
    public float height;
    public SegmentVariant variant = SegmentVariant.Default;

    [Header("Prefab")]
    public GameObject prefab;
    public bool hasCollider;
    public bool canBeWallSegment;
    public bool canBeOpeningOverlay;

    [Header("Placement Authoring Override")]
    public bool hasPlacementAuthoringOverride;
    public Vector3 placementAuthoringEulerAngles = Vector3.zero;
    public Vector3 placementAuthoringScale = Vector3.one;

    private void OnValidate()
    {
        placementAuthoringScale = new Vector3(
            Mathf.Approximately(placementAuthoringScale.x, 0f) ? 1f : placementAuthoringScale.x,
            Mathf.Approximately(placementAuthoringScale.y, 0f) ? 1f : placementAuthoringScale.y,
            Mathf.Approximately(placementAuthoringScale.z, 0f) ? 1f : placementAuthoringScale.z);
    }
}
