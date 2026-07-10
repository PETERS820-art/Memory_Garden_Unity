using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class AppPreviewCatalogScanSummary
{
    public int blockDefinitionCount;
    public int spaceBlockPrefabCount;
    public int furniturePrefabCount;
    public int furniturePlacementCount;
    public int memoryItemPrefabCount;
    public int memoryItemDataCount;
    public int warningCount;
    public int blockCount;
    public int furnitureCount;
    public int slotCount;
    public int itemCount;
    public int portCount;
    public int layoutBlockInstanceCount;
    public int layoutConnectionCount;
    public int layoutFurniturePlacementCount;
    public int layoutSlotPlacementCount;
    public int layoutItemPlacementCount;
}

[Serializable]
public class AppPreviewCatalogWarning
{
    public string code;
    public string sourcePath;
    public string message;
}

[Serializable]
public class PreviewBoundsData
{
    public Vector3 center;
    public Vector3 size;
}

[Serializable]
public class StructuralPlacementPreviewRecord
{
    public string placementId;
    public string segmentId;
    public string category;
    public int gridX;
    public int gridZ;
    public string side;
    public int rotationY;
    public Vector2Int footprint;
    public string overlaySegmentId;
    public bool isConnectorCandidate;
}

[Serializable]
public class WallEdgePreviewRecord
{
    public string side;
    public string edgeKey;
    public int gridX;
    public int gridZ;
    public int spanUnits;
}

[Serializable]
public class DoorwayPortPreviewRecord
{
    public string portId;
    public string openingType;
    public string connectionKind;
    public int widthUnits;
    public float height;
    public string wallSide;
    public Vector2Int gridPosition;
    public Vector3 localPosition;
    public Vector3 localForward;
    public bool isOccupied;
}

[Serializable]
public class BlockPreviewRecord
{
    public string blockTypeId;
    public string sourceAssetPath;
    public string sourcePrefabPath;
    public string displayName;
    public int gridWidth;
    public int gridDepth;
    public float gridSize;
    public string previewMode;
    public string previewAssetKey;
    public PreviewBoundsData previewBounds;
    public List<StructuralPlacementPreviewRecord> structuralPlacements = new List<StructuralPlacementPreviewRecord>();
    public List<WallEdgePreviewRecord> wallEdges = new List<WallEdgePreviewRecord>();
    public List<DoorwayPortPreviewRecord> doorwayPorts = new List<DoorwayPortPreviewRecord>();
    public List<string> warnings = new List<string>();
}

[Serializable]
public class FurnitureFloorAnchorRecord
{
    public int gridX;
    public int gridZ;
    public int widthUnits;
    public int depthUnits;
    public int floorGridXHalf;
    public int floorGridZHalf;
    public int floorWidthHalf;
    public int floorDepthHalf;
    public float rotationY;
    public Vector3 localPosition;
    public Vector3 localEulerAngles;
}

[Serializable]
public class FurnitureWallAnchorRecord
{
    public string wallSide;
    public int wallGridPosition;
    public int widthUnits;
    public float rotationY;
    public float heightOffset;
    public int wallLayerIndex;
    public int wallLayerCount;
    public float wallSurfaceHeight;
    public Vector3 localPosition;
    public Vector3 localEulerAngles;
}

[Serializable]
public class FurnitureSlotPreviewRecord
{
    public string slotId;
    public string slotType;
    public List<string> acceptedItemSizes = new List<string>();
    public Vector3 localPosition;
    public Vector3 localRotation;
    public string occupiedItemId;
}

[Serializable]
public class FurniturePreviewRecord
{
    public string furnitureId;
    public string sourcePrefabPath;
    public string previewAssetKey;
    public string previewMode;
    public string silhouetteType;
    public Vector3 dimensions;
    public string blockTypeId;
    public string blockInstanceId;
    public string surfaceType;
    public FurnitureFloorAnchorRecord floorAnchor;
    public FurnitureWallAnchorRecord wallAnchor;
    public List<string> slotIds = new List<string>();
    public List<FurnitureSlotPreviewRecord> slots = new List<FurnitureSlotPreviewRecord>();
    public List<string> warnings = new List<string>();
}

[Serializable]
public class ItemPreviewRecord
{
    public string itemId;
    public string displayName;
    public string sourcePrefabPath;
    public string sourceDataAssetPath;
    public string previewAssetKey;
    public string previewMode;
    public string silhouetteType;
    public Vector3 dimensions;
    public string itemSize;
    public List<string> allowedSlotTypes = new List<string>();
    public bool enablePlacement;
    public string emotionType;
    public string thumbnailHint;
    public List<string> warnings = new List<string>();
}

[Serializable]
public class PreviewAssetManifestRecord
{
    public string previewAssetKey;
    public string sourceType;
    public string sourceAssetPath;
    public string previewMode;
    public string recommendedAppRenderer;
    public string fallbackShape;
    public string colorHint;
    public Vector3 dimensions;
    public string thumbnailPath;
    public List<string> notes = new List<string>();
}

[Serializable]
public class BlockPreviewCatalogDocument
{
    public string schemaVersion;
    public string generatedAtUtc;
    public List<BlockPreviewRecord> records = new List<BlockPreviewRecord>();
}

[Serializable]
public class FurniturePreviewCatalogDocument
{
    public string schemaVersion;
    public string generatedAtUtc;
    public List<FurniturePreviewRecord> records = new List<FurniturePreviewRecord>();
}

[Serializable]
public class ItemPreviewCatalogDocument
{
    public string schemaVersion;
    public string generatedAtUtc;
    public List<ItemPreviewRecord> records = new List<ItemPreviewRecord>();
}

[Serializable]
public class PreviewAssetManifestDocument
{
    public string schemaVersion;
    public string generatedAtUtc;
    public List<PreviewAssetManifestRecord> records = new List<PreviewAssetManifestRecord>();
}

[Serializable]
public class GardenLayoutPreviewDocument
{
    public string layoutId = "unity-current-scene-preview";
    public string sourceScene;
    public string exportedAt;
    public string units = "unity-world";
    public List<GardenLayoutBlockInstanceRecord> blockInstances = new List<GardenLayoutBlockInstanceRecord>();
    public List<GardenLayoutConnectionRecord> connections = new List<GardenLayoutConnectionRecord>();
    public List<GardenLayoutFurniturePlacementRecord> furniturePlacements = new List<GardenLayoutFurniturePlacementRecord>();
    public List<GardenLayoutSlotPlacementRecord> slotPlacements = new List<GardenLayoutSlotPlacementRecord>();
    public List<GardenLayoutItemPlacementRecord> itemPlacements = new List<GardenLayoutItemPlacementRecord>();
    public List<AppPreviewCatalogWarning> warnings = new List<AppPreviewCatalogWarning>();
}

[Serializable]
public class GardenLayoutBlockInstanceRecord
{
    public string blockInstanceId;
    public string blockTypeId;
    public string sourcePrefabPath;
    public string scenePath;
    public Vector3 position;
    public Vector3 rotationEuler;
    public Vector3 scale;
    public int gridWidth;
    public int gridDepth;
    public float gridSize;
    public Vector3 boundsCenter;
    public Vector3 boundsSize;
}

[Serializable]
public class GardenLayoutConnectionRecord
{
    public string connectionId;
    public string portAId;
    public string portBId;
    public string blockAInstanceId;
    public string blockBInstanceId;
    public string connectionKind;
    public int widthUnits;
    public float height;
    public float connectorLength;
    public bool isPreview;
}

[Serializable]
public class GardenLayoutFurniturePlacementRecord
{
    public string furnitureId;
    public string sourcePrefabPath;
    public string blockInstanceId;
    public string blockTypeId;
    public string surfaceType;
    public Vector3 position;
    public Vector3 rotationEuler;
    public Vector3 scale;
    public FurnitureFloorAnchorRecord floorAnchor;
    public FurnitureWallAnchorRecord wallAnchor;
    public List<string> slotIds = new List<string>();
}

[Serializable]
public class GardenLayoutSlotPlacementRecord
{
    public string slotId;
    public string furnitureId;
    public string blockInstanceId;
    public string slotType;
    public List<string> acceptedItemSizes = new List<string>();
    public Vector3 worldPosition;
    public Vector3 localPosition;
    public Vector3 localRotationEuler;
    public string occupiedItemId;
}

[Serializable]
public class GardenLayoutItemPlacementRecord
{
    public string itemId;
    public string itemName;
    public string sourcePrefabPath;
    public string furnitureId;
    public string slotId;
    public string blockInstanceId;
    public Vector3 worldPosition;
    public Vector3 rotationEuler;
    public string itemSize;
    public List<string> allowedSlotTypes = new List<string>();
    public bool enablePlacement;
}

[Serializable]
public class AppPreviewCatalogScanResult
{
    public string schemaVersion = "app-preview-catalog-v0";
    public string generatedAtUtc;
    public string exportDirectory;
    public List<string> scanRoots = new List<string>();
    public AppPreviewCatalogScanSummary summary = new AppPreviewCatalogScanSummary();
    public List<BlockPreviewRecord> blocks = new List<BlockPreviewRecord>();
    public List<FurniturePreviewRecord> furniture = new List<FurniturePreviewRecord>();
    public List<ItemPreviewRecord> items = new List<ItemPreviewRecord>();
    public List<PreviewAssetManifestRecord> manifest = new List<PreviewAssetManifestRecord>();
    public GardenLayoutPreviewDocument gardenLayout = new GardenLayoutPreviewDocument();
    public List<AppPreviewCatalogWarning> warnings = new List<AppPreviewCatalogWarning>();
}

[Serializable]
public class AppPreviewCatalogWriteResult
{
    public string exportDirectory;
    public string blockCatalogPath;
    public string furnitureCatalogPath;
    public string itemCatalogPath;
    public string manifestPath;
    public string gardenLayoutPath;
    public string reportPath;
}
