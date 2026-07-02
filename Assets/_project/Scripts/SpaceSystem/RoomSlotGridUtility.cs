using System;
using System.Collections.Generic;
using UnityEngine;

public static class RoomSlotGridUtility
{
    private const float DefaultGridSize = 1f;

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
                if (!IsFloorPlacementInBounds(block, gridX, gridZ, widthUnits, depthUnits))
                {
                    errorMessage = $"Floor placement is out of bounds: ({gridX}, {gridZ}) size {widthUnits}x{depthUnits}.";
                    return false;
                }

                localPosition = GetFloorLocalPosition(block, gridX, gridZ, widthUnits, depthUnits);
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
        int gridWidth = GetGridWidth(block);
        int gridDepth = GetGridDepth(block);
        float gridSize = GetGridSize(block);
        float halfWidth = gridWidth * gridSize * 0.5f;
        float halfDepth = gridDepth * gridSize * 0.5f;

        return new Vector3(
            -halfWidth + ((gridX + (Mathf.Max(1, widthUnits) * 0.5f)) * gridSize),
            0f,
            -halfDepth + ((gridZ + (Mathf.Max(1, depthUnits) * 0.5f)) * gridSize));
    }

    public static bool IsFloorPlacementInBounds(
        MemorySpaceBlock block,
        int gridX,
        int gridZ,
        int widthUnits,
        int depthUnits)
    {
        int gridWidth = GetGridWidth(block);
        int gridDepth = GetGridDepth(block);
        widthUnits = Mathf.Max(1, widthUnits);
        depthUnits = Mathf.Max(1, depthUnits);

        return gridX >= 0
            && gridZ >= 0
            && gridX + widthUnits <= gridWidth
            && gridZ + depthUnits <= gridDepth;
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
        List<Vector2Int> cells = new List<Vector2Int>();
        int width = Mathf.Max(1, widthUnits);
        int depth = Mathf.Max(1, depthUnits);
        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < depth; z++)
            {
                cells.Add(new Vector2Int(gridX + x, gridZ + z));
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
            return DoFloorAreasOverlap(a.gridX, a.gridZ, a.widthUnits, a.depthUnits, b.gridX, b.gridZ, b.widthUnits, b.depthUnits);
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
        int aMinX = aGridX;
        int aMaxX = aGridX + Mathf.Max(1, aWidthUnits);
        int aMinZ = aGridZ;
        int aMaxZ = aGridZ + Mathf.Max(1, aDepthUnits);

        int bMinX = bGridX;
        int bMaxX = bGridX + Mathf.Max(1, bWidthUnits);
        int bMinZ = bGridZ;
        int bMaxZ = bGridZ + Mathf.Max(1, bDepthUnits);

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
}
