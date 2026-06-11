using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SBD_NewSpaceBlock", menuName = "Memory Garden/Space Block Definition")]
public class SpaceBlockDefinition : ScriptableObject
{
    public string blockId;
    public int gridWidth = 10;
    public int gridDepth = 10;
    public float gridSize = 1f;
    public List<SpaceSegmentPlacementRecord> placements = new List<SpaceSegmentPlacementRecord>();
}

[Serializable]
public class SpaceSegmentPlacementRecord
{
    public string placementId;
    public string segmentId;
    public SegmentCategory category;
    public int gridX;
    public int gridZ;
    public WallSide side;
    public int rotationY;
    public Vector2Int footprint = Vector2Int.one;
    public string overlaySegmentId;
    public bool isConnectorCandidate;
}
