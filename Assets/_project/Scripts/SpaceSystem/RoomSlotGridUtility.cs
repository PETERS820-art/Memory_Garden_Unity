using System;
using System.Collections.Generic;
using UnityEngine;

public static class RoomSlotGridUtility
{
    private const float DefaultGridSize = 1f;
    private const int FloorGridSubdivision = 2;

    public static int GetGridWidth(MemorySpaceBlock block)
    {
        if (block == null)
        {
            return 0;
        }

        if (block.blockDefinition != null && block.blockDefinition.gridWidth > 0)
        {
            return block.blockDefinition.gridWidth;
        }

        return Mathf.Max(0, block.widthUnits);
    }

    public static int GetGridDepth(MemorySpaceBlock block)
    {
        if (block == null)
        {
            return 0;
        }

        if (block.blockDefinition != null && block.blockDefinition.gridDepth > 0)
        {
            return block.blockDefinition.gridDepth;
        }

        return Mathf.Max(0, block.depthUnits);
    }

    public static float GetGridSize(MemorySpaceBlock block)
    {
        if (block != null && block.blockDefinition != null && block.blockDefinition.gridSize > 0f)
        {
            return block.blockDefinition.gridSize;
        }

        return DefaultGridSize;
    }

    public static int GetFloorGridSubdivision()
    {
        return FloorGridSubdivision;
    }

    public static string GetBlockTypeId(MemorySpaceBlock block)
    {
        if (block == null)
        {
            return string.Empty;
        }

        if (block.blockDefinition != null && !string.IsNullOrWhiteSpace(block.blockDefinition.blockId))
        {
            return block.blockDefinition.blockId.Trim();
        }

        return string.IsNullOrWhiteSpace(block.name) ? string.Empty : block.name.Trim();
    }

    public static string GetBlockInstanceId(MemorySpaceBlock block)
    {
        if (block == null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(block.spaceBlockId))
        {
            return block.spaceBlockId.Trim();
        }

        return string.IsNullOrWhiteSpace(block.name) ? string.Empty : block.name.Trim();
    }

    public static bool TryGetPlacementTransform(
        MemorySpaceBlock block,
        RoomSlotSurfaceType surfaceType,
        int gridX,
        int gridZ,
        int widthUnits,
        int depthUnits,
        int floorGridXHalf,
        int floorGridZHalf,
        int floorWidthHalf,
        int floorDepthHalf,
        float rotationY,
        WallSide wallSide,
        int wallGridPosition,
        float heightOffset,
        out Vector3 localPosition,
        out Quaternion localRotation,
        out string errorMessage)
    {
        localPosition = Vector3.zero;
        localRotation = Quaternion.identity;
        errorMessage = string.Empty;

        if (block == null)
        {
            errorMessage = "Target MemorySpaceBlock is missing.";
            return false;
        }

        if (!HasValidGrid(block))
        {
            errorMessage = "Target MemorySpaceBlock does not have a valid grid.";
            return false;
        }

        widthUnits = Mathf.Max(1, widthUnits);
        depthUnits = Mathf.Max(1, depthUnits);

        switch (surfaceType)
        {
            case RoomSlotSurfaceType.Floor:
                int resolvedFloorGridXHalf = floorWidthHalf > 0 ? floorGridXHalf : gridX * FloorGridSubdivision;
                int resolvedFloorGridZHalf = floorDepthHalf > 0 ? floorGridZHalf : gridZ * FloorGridSubdivision;
                int resolvedFloorWidthHalf = floorWidthHalf > 0 ? floorWidthHalf : Mathf.Max(1, widthUnits) * FloorGridSubdivision;
                int resolvedFloorDepthHalf = floorDepthHalf > 0 ? floorDepthHalf : Mathf.Max(1, depthUnits) * FloorGridSubdivision;
                if (!IsFloorPlacementInBoundsHalf(block, resolvedFloorGridXHalf, resolvedFloorGridZHalf, resolvedFloorWidthHalf, resolvedFloorDepthHalf))
                {
                    errorMessage = $"Floor placement is out of bounds: ({gridX}, {gridZ}) size {widthUnits}x{depthUnits}.";
                    return false;
                }

                localPosition = GetFloorLocalPositionHalf(block, resolvedFloorGridXHalf, resolvedFloorGridZHalf, resolvedFloorWidthHalf, resolvedFloorDepthHalf);
                localRotation = Quaternion.Euler(0f, NormalizeRotation(rotationY), 0f);
                return true;

            case RoomSlotSurfaceType.Wall:
                if (!TryGetWallAnchorGrid(block, wallSide, wallGridPosition, widthUnits, out int resolvedGridX, out int resolvedGridZ))
                {
                    errorMessage = $"Wall placement is out of bounds: {wallSide} [{wallGridPosition}] span {widthUnits}.";
                    return false;
                }

                localPosition = GetWallLocalPosition(block, wallSide, wallGridPosition, widthUnits, heightOffset);
                localRotation = GetWallLocalRotation(wallSide, rotationY);
                return true;

            default:
                errorMessage = $"Unsupported surface type: {surfaceType}.";
                return false;
        }
    }

    public static Vector3 GetFloorLocalPosition(
        MemorySpaceBlock block,
        int gridX,
        int gridZ,
        int widthUnits,
        int depthUnits)
    {
        return GetFloorLocalPositionHalf(
            block,
            gridX * FloorGridSubdivision,
            gridZ * FloorGridSubdivision,
            Mathf.Max(1, widthUnits) * FloorGridSubdivision,
            Mathf.Max(1, depthUnits) * FloorGridSubdivision);
    }

    public static Vector3 GetFloorLocalPositionHalf(
        MemorySpaceBlock block,
        int gridXHalf,
        int gridZHalf,
        int widthHalf,
        int depthHalf)
    {
        int gridWidth = GetGridWidth(block);
        int gridDepth = GetGridDepth(block);
        float gridSize = GetGridSize(block);
        float halfGridSize = gridSize / FloorGridSubdivision;
        float halfWidth = gridWidth * gridSize * 0.5f;
        float halfDepth = gridDepth * gridSize * 0.5f;

        return new Vector3(
            -halfWidth + ((gridXHalf + (Mathf.Max(1, widthHalf) * 0.5f)) * halfGridSize),
            0f,
            -halfDepth + ((gridZHalf + (Mathf.Max(1, depthHalf) * 0.5f)) * halfGridSize));
    }

    public static bool IsFloorPlacementInBounds(
        MemorySpaceBlock block,
        int gridX,
        int gridZ,
        int widthUnits,
        int depthUnits)
    {
        return IsFloorPlacementInBoundsHalf(
            block,
            gridX * FloorGridSubdivision,
            gridZ * FloorGridSubdivision,
            Mathf.Max(1, widthUnits) * FloorGridSubdivision,
            Mathf.Max(1, depthUnits) * FloorGridSubdivision);
    }

    public static bool IsFloorPlacementInBoundsHalf(
        MemorySpaceBlock block,
        int gridXHalf,
        int gridZHalf,
        int widthHalf,
        int depthHalf)
    {
        int gridWidth = GetGridWidth(block);
        int gridDepth = GetGridDepth(block);
        int halfGridWidth = gridWidth * FloorGridSubdivision;
        int halfGridDepth = gridDepth * FloorGridSubdivision;
        widthHalf = Mathf.Max(1, widthHalf);
        depthHalf = Mathf.Max(1, depthHalf);

        return gridXHalf >= 0
            && gridZHalf >= 0
            && gridXHalf + widthHalf <= halfGridWidth
            && gridZHalf + depthHalf <= halfGridDepth;
    }

    public static bool TryGetWallAnchorGrid(
        MemorySpaceBlock block,
        WallSide wallSide,
        int wallGridPosition,
        int widthUnits,
        out int gridX,
        out int gridZ)
    {
        gridX = 0;
        gridZ = 0;

        int gridWidth = GetGridWidth(block);
        int gridDepth = GetGridDepth(block);
        int wallSpan = GetWallSlotCount(block, wallSide);
        int width = Mathf.Max(1, widthUnits);

        if (gridWidth <= 0 || gridDepth <= 0 || wallGridPosition < 0 || width > wallSpan || wallGridPosition + width > wallSpan)
        {
            return false;
        }

        switch (wallSide)
        {
            case WallSide.North:
                gridX = wallGridPosition;
                gridZ = gridDepth - 1;
                return true;

            case WallSide.South:
                gridX = wallGridPosition;
                gridZ = 0;
                return true;

            case WallSide.East:
                gridX = gridWidth - 1;
                gridZ = wallGridPosition;
                return true;

            case WallSide.West:
                gridX = 0;
                gridZ = wallGridPosition;
                return true;

            default:
                return false;
        }
    }

    public static bool IsWallPlacementInBounds(
        MemorySpaceBlock block,
        WallSide wallSide,
        int wallGridPosition,
        int widthUnits)
    {
        return TryGetWallAnchorGrid(block, wallSide, wallGridPosition, widthUnits, out _, out _);
    }

    public static Vector3 GetWallLocalPosition(
        MemorySpaceBlock block,
        WallSide wallSide,
        int wallGridPosition,
        int widthUnits,
        float heightOffset)
    {
        TryGetWallAnchorGrid(block, wallSide, wallGridPosition, widthUnits, out int gridX, out int gridZ);

        int gridWidth = GetGridWidth(block);
        int gridDepth = GetGridDepth(block);
        float gridSize = GetGridSize(block);
        float halfWidth = gridWidth * gridSize * 0.5f;
        float halfDepth = gridDepth * gridSize * 0.5f;
        float cellMinX = -halfWidth + (gridX * gridSize);
        float cellMinZ = -halfDepth + (gridZ * gridSize);
        float wallLength = Mathf.Max(1, widthUnits) * gridSize;

        switch (wallSide)
        {
            case WallSide.North:
                return new Vector3(cellMinX + (wallLength * 0.5f), heightOffset, cellMinZ + gridSize);
            case WallSide.South:
                return new Vector3(cellMinX + (wallLength * 0.5f), heightOffset, cellMinZ);
            case WallSide.East:
                return new Vector3(cellMinX + gridSize, heightOffset, cellMinZ + (wallLength * 0.5f));
            case WallSide.West:
                return new Vector3(cellMinX, heightOffset, cellMinZ + (wallLength * 0.5f));
            default:
                return Vector3.zero;
        }
    }

    public static Quaternion GetWallLocalRotation(WallSide wallSide, float extraRotationY)
    {
        float baseRotationY;
        switch (wallSide)
        {
            case WallSide.North:
                baseRotationY = 180f;
                break;
            case WallSide.South:
                baseRotationY = 0f;
                break;
            case WallSide.East:
                baseRotationY = 270f;
                break;
            case WallSide.West:
                baseRotationY = 90f;
                break;
            default:
                baseRotationY = 0f;
                break;
        }

        return Quaternion.Euler(0f, NormalizeRotation(baseRotationY + extraRotationY), 0f);
    }

    public static List<Vector2Int> GetFloorFootprintCells(int gridX, int gridZ, int widthUnits, int depthUnits)
    {
        return GetFloorFootprintHalfCells(
            gridX * FloorGridSubdivision,
            gridZ * FloorGridSubdivision,
            Mathf.Max(1, widthUnits) * FloorGridSubdivision,
            Mathf.Max(1, depthUnits) * FloorGridSubdivision);
    }

    public static List<Vector2Int> GetFloorFootprintHalfCells(int gridXHalf, int gridZHalf, int widthHalf, int depthHalf)
    {
        List<Vector2Int> cells = new List<Vector2Int>();
        int width = Mathf.Max(1, widthHalf);
        int depth = Mathf.Max(1, depthHalf);
        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < depth; z++)
            {
                cells.Add(new Vector2Int(gridXHalf + x, gridZHalf + z));
            }
        }

        return cells;
    }

    public static List<string> GetWallEdgeKeys(int gridX, int gridZ, WallSide wallSide, int widthUnits)
    {
        List<string> keys = new List<string>();
        int width = Mathf.Max(1, widthUnits);
        for (int step = 0; step < width; step++)
        {
            switch (wallSide)
            {
                case WallSide.North:
                    keys.Add($"H:{gridX + step}:{gridZ + 1}");
                    break;
                case WallSide.South:
                    keys.Add($"H:{gridX + step}:{gridZ}");
                    break;
                case WallSide.East:
                    keys.Add($"V:{gridX + 1}:{gridZ + step}");
                    break;
                case WallSide.West:
                    keys.Add($"V:{gridX}:{gridZ + step}");
                    break;
            }
        }

        return keys;
    }

    public static bool DoPlacementsOverlap(RoomSlotPlacementMetadata a, RoomSlotPlacementMetadata b)
    {
        if (a == null || b == null || a.surfaceType != b.surfaceType)
        {
            return false;
        }

        if (a.surfaceType == RoomSlotSurfaceType.Floor)
        {
            return DoFloorAreasOverlapHalf(
                GetFloorGridXHalf(a),
                GetFloorGridZHalf(a),
                GetFloorWidthHalf(a),
                GetFloorDepthHalf(a),
                GetFloorGridXHalf(b),
                GetFloorGridZHalf(b),
                GetFloorWidthHalf(b),
                GetFloorDepthHalf(b));
        }

        if (GetWallLayerIndex(a) != GetWallLayerIndex(b))
        {
            return false;
        }

        List<string> aKeys = GetWallEdgeKeys(a.gridX, a.gridZ, a.wallSide, a.widthUnits);
        List<string> bKeys = GetWallEdgeKeys(b.gridX, b.gridZ, b.wallSide, b.widthUnits);
        for (int i = 0; i < aKeys.Count; i++)
        {
            for (int j = 0; j < bKeys.Count; j++)
            {
                if (string.Equals(aKeys[i], bKeys[j], StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public static bool DoFloorAreasOverlap(
        int aGridX,
        int aGridZ,
        int aWidthUnits,
        int aDepthUnits,
        int bGridX,
        int bGridZ,
        int bWidthUnits,
        int bDepthUnits)
    {
        return DoFloorAreasOverlapHalf(
            aGridX * FloorGridSubdivision,
            aGridZ * FloorGridSubdivision,
            Mathf.Max(1, aWidthUnits) * FloorGridSubdivision,
            Mathf.Max(1, aDepthUnits) * FloorGridSubdivision,
            bGridX * FloorGridSubdivision,
            bGridZ * FloorGridSubdivision,
            Mathf.Max(1, bWidthUnits) * FloorGridSubdivision,
            Mathf.Max(1, bDepthUnits) * FloorGridSubdivision);
    }

    public static bool DoFloorAreasOverlapHalf(
        int aGridXHalf,
        int aGridZHalf,
        int aWidthHalf,
        int aDepthHalf,
        int bGridXHalf,
        int bGridZHalf,
        int bWidthHalf,
        int bDepthHalf)
    {
        int aMinX = aGridXHalf;
        int aMaxX = aGridXHalf + Mathf.Max(1, aWidthHalf);
        int aMinZ = aGridZHalf;
        int aMaxZ = aGridZHalf + Mathf.Max(1, aDepthHalf);

        int bMinX = bGridXHalf;
        int bMaxX = bGridXHalf + Mathf.Max(1, bWidthHalf);
        int bMinZ = bGridZHalf;
        int bMaxZ = bGridZHalf + Mathf.Max(1, bDepthHalf);

        return aMinX < bMaxX && aMaxX > bMinX && aMinZ < bMaxZ && aMaxZ > bMinZ;
    }

    public static List<RoomSlotPlacementMetadata> FindOverlaps(MemorySpaceBlock block, RoomSlotPlacementMetadata candidate, RoomSlotPlacementMetadata ignore = null)
    {
        List<RoomSlotPlacementMetadata> overlaps = new List<RoomSlotPlacementMetadata>();
        if (block == null || candidate == null)
        {
            return overlaps;
        }

        RoomSlotPlacementMetadata[] existing = block.GetComponentsInChildren<RoomSlotPlacementMetadata>(true);
        for (int i = 0; i < existing.Length; i++)
        {
            RoomSlotPlacementMetadata current = existing[i];
            if (current == null || current == candidate || current == ignore)
            {
                continue;
            }

            if (DoPlacementsOverlap(candidate, current))
            {
                overlaps.Add(current);
            }
        }

        return overlaps;
    }

    public static int GetWallSlotCount(MemorySpaceBlock block, WallSide wallSide)
    {
        int gridWidth = GetGridWidth(block);
        int gridDepth = GetGridDepth(block);

        switch (wallSide)
        {
            case WallSide.North:
            case WallSide.South:
                return Mathf.Max(0, gridWidth);
            case WallSide.East:
            case WallSide.West:
                return Mathf.Max(0, gridDepth);
            default:
                return 0;
        }
    }

    public static float NormalizeRotation(float rotationY)
    {
        float normalized = rotationY % 360f;
        if (normalized < 0f)
        {
            normalized += 360f;
        }

        return normalized;
    }

    private static bool HasValidGrid(MemorySpaceBlock block)
    {
        return GetGridWidth(block) > 0
            && GetGridDepth(block) > 0
            && GetGridSize(block) > 0f;
    }

    public static int GetFloorGridXHalf(RoomSlotPlacementMetadata metadata)
    {
        if (metadata == null)
        {
            return 0;
        }

        return HasHalfFloorPlacementData(metadata) && !(metadata.floorGridXHalf == 0 && metadata.gridX != 0)
            ? metadata.floorGridXHalf
            : metadata.gridX * FloorGridSubdivision;
    }

    public static int GetFloorGridZHalf(RoomSlotPlacementMetadata metadata)
    {
        if (metadata == null)
        {
            return 0;
        }

        return HasHalfFloorPlacementData(metadata) && !(metadata.floorGridZHalf == 0 && metadata.gridZ != 0)
            ? metadata.floorGridZHalf
            : metadata.gridZ * FloorGridSubdivision;
    }

    public static int GetFloorWidthHalf(RoomSlotPlacementMetadata metadata)
    {
        if (metadata == null)
        {
            return FloorGridSubdivision;
        }

        return HasHalfFloorPlacementData(metadata)
            ? Mathf.Max(1, metadata.floorWidthHalf)
            : Mathf.Max(1, metadata.widthUnits) * FloorGridSubdivision;
    }

    public static int GetFloorDepthHalf(RoomSlotPlacementMetadata metadata)
    {
        if (metadata == null)
        {
            return FloorGridSubdivision;
        }

        return HasHalfFloorPlacementData(metadata)
            ? Mathf.Max(1, metadata.floorDepthHalf)
            : Mathf.Max(1, metadata.depthUnits) * FloorGridSubdivision;
    }

    public static int GetWallLayerIndex(RoomSlotPlacementMetadata metadata)
    {
        if (metadata == null)
        {
            return 0;
        }

        int layerCount = Mathf.Max(1, metadata.wallLayerCount);
        return Mathf.Clamp(metadata.wallLayerIndex, 0, layerCount - 1);
    }

    private static bool HasHalfFloorPlacementData(RoomSlotPlacementMetadata metadata)
    {
        return metadata != null
            && metadata.floorWidthHalf > 0
            && metadata.floorDepthHalf > 0;
    }
}
