using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class AppPreviewCatalogExporter
{
    public const string DefaultExportDirectory = "Assets/_project/Exports/AppPreviewCatalog";

    private const string ScriptableObjectRoot = "Assets/_project/ScriptableObjects";
    private const string DataRoot = "Assets/_project/Data";
    private const string SpaceBlockPrefabRoot = "Assets/_project/Prefabs/Environment/SpaceBlocks";
    private const string DisplayFurniturePrefabRoot = "Assets/_project/Prefabs/DisplayFurniture";
    private const string MemoryItemPrefabRoot = "Assets/_project/Prefabs/MemoryItems";
    private const float DefaultBlockHeight = 2.5f;

    private sealed class FurniturePrefabDescriptor
    {
        public string assetPath;
        public string prefabName;
        public string furnitureId;
        public FurnitureType furnitureType;
        public Vector3 dimensions;
        public List<FurnitureSlotPreviewRecord> slots = new List<FurnitureSlotPreviewRecord>();
    }

    public static AppPreviewCatalogScanResult ScanProject()
    {
        AppPreviewCatalogScanResult result = new AppPreviewCatalogScanResult();
        result.generatedAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        result.exportDirectory = DefaultExportDirectory;
        result.scanRoots.Add(ScriptableObjectRoot);
        if (AssetDatabase.IsValidFolder(DataRoot))
        {
            result.scanRoots.Add(DataRoot);
        }

        result.scanRoots.Add(SpaceBlockPrefabRoot);
        result.scanRoots.Add(DisplayFurniturePrefabRoot);
        result.scanRoots.Add(MemoryItemPrefabRoot);

        Dictionary<string, BlockPreviewRecord> blocksByAssetPath =
            new Dictionary<string, BlockPreviewRecord>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, BlockPreviewRecord> blocksByTypeId =
            new Dictionary<string, BlockPreviewRecord>(StringComparer.OrdinalIgnoreCase);

        ScanBlockDefinitions(result, blocksByAssetPath, blocksByTypeId);
        ScanSpaceBlockPrefabs(result, blocksByAssetPath, blocksByTypeId);

        Dictionary<string, FurniturePrefabDescriptor> furnitureByName =
            new Dictionary<string, FurniturePrefabDescriptor>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, FurniturePrefabDescriptor> furnitureByPath =
            new Dictionary<string, FurniturePrefabDescriptor>(StringComparer.OrdinalIgnoreCase);

        ScanFurniturePrefabs(result, furnitureByName, furnitureByPath);
        ScanFurniturePlacements(result, furnitureByName);
        AddStandaloneFurnitureRecords(result, furnitureByPath);

        ScanItems(result);
        ResolveDuplicateBlockPreviewKeys(result);
        BuildManifest(result);
        FinalizeSummary(result);
        SortResult(result);
        return result;
    }

    public static AppPreviewCatalogWriteResult ExportPreviewCatalog(string exportDirectory)
    {
        AppPreviewCatalogScanResult result = ScanProject();
        result.exportDirectory = string.IsNullOrWhiteSpace(exportDirectory) ? DefaultExportDirectory : exportDirectory;
        return AppPreviewCatalogWriter.Write(result);
    }

    public static void BatchExportDefaultCatalog()
    {
        AppPreviewCatalogWriteResult writeResult = ExportPreviewCatalog(DefaultExportDirectory);
        Debug.Log("[AppPreviewCatalogExporter] Export completed.");
        Debug.Log("[AppPreviewCatalogExporter] Block catalog: " + writeResult.blockCatalogPath);
        Debug.Log("[AppPreviewCatalogExporter] Furniture catalog: " + writeResult.furnitureCatalogPath);
        Debug.Log("[AppPreviewCatalogExporter] Item catalog: " + writeResult.itemCatalogPath);
        Debug.Log("[AppPreviewCatalogExporter] Manifest: " + writeResult.manifestPath);
        Debug.Log("[AppPreviewCatalogExporter] Report: " + writeResult.reportPath);
    }

    private static void ScanBlockDefinitions(
        AppPreviewCatalogScanResult result,
        Dictionary<string, BlockPreviewRecord> blocksByAssetPath,
        Dictionary<string, BlockPreviewRecord> blocksByTypeId)
    {
        string[] definitionRoots = GetExistingRoots(ScriptableObjectRoot, DataRoot);
        string[] guids = AssetDatabase.FindAssets("t:SpaceBlockDefinition", definitionRoots);
        result.summary.blockDefinitionCount = guids != null ? guids.Length : 0;

        if (guids == null)
        {
            return;
        }

        for (int i = 0; i < guids.Length; i++)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
            SpaceBlockDefinition definition = AssetDatabase.LoadAssetAtPath<SpaceBlockDefinition>(assetPath);
            if (definition == null)
            {
                AddWarning(result, null, "missing_space_block_definition_asset", assetPath, "Failed to load SpaceBlockDefinition asset.");
                continue;
            }

            BlockPreviewRecord record = CreateBlockRecord(definition, assetPath, result);
            result.blocks.Add(record);

            if (!string.IsNullOrWhiteSpace(assetPath))
            {
                blocksByAssetPath[assetPath] = record;
            }

            if (!string.IsNullOrWhiteSpace(record.blockTypeId))
            {
                blocksByTypeId[record.blockTypeId] = record;
            }
        }
    }

    private static BlockPreviewRecord CreateBlockRecord(
        SpaceBlockDefinition definition,
        string assetPath,
        AppPreviewCatalogScanResult result)
    {
        List<string> warnings = new List<string>();
        string fallbackId = Path.GetFileNameWithoutExtension(assetPath);
        string blockTypeId = FirstNonEmpty(definition.blockId, fallbackId);
        if (string.IsNullOrWhiteSpace(definition.blockId))
        {
            AddWarning(result, warnings, "missing_blockTypeId", assetPath, $"SpaceBlockDefinition '{fallbackId}' is missing blockId. Falling back to asset name.");
        }

        BlockPreviewRecord record = new BlockPreviewRecord();
        record.blockTypeId = blockTypeId;
        record.sourceAssetPath = assetPath;
        record.displayName = blockTypeId;
        record.gridWidth = Mathf.Max(0, definition.gridWidth);
        record.gridDepth = Mathf.Max(0, definition.gridDepth);
        record.gridSize = definition.gridSize > 0f ? definition.gridSize : 1f;
        record.previewMode = "procedural";
        record.previewAssetKey = "preview-block-" + SanitizeKey(blockTypeId);
        record.previewBounds = CreatePreviewBounds(record.gridWidth, record.gridDepth, record.gridSize, DefaultBlockHeight);
        record.structuralPlacements = ConvertPlacements(definition.placements);
        record.wallEdges = CreateWallEdges(definition.placements);
        record.warnings = warnings;

        return record;
    }

    private static void ScanSpaceBlockPrefabs(
        AppPreviewCatalogScanResult result,
        Dictionary<string, BlockPreviewRecord> blocksByAssetPath,
        Dictionary<string, BlockPreviewRecord> blocksByTypeId)
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { SpaceBlockPrefabRoot });
        result.summary.spaceBlockPrefabCount = guids != null ? guids.Length : 0;

        if (guids == null)
        {
            return;
        }

        for (int i = 0; i < guids.Length; i++)
        {
            string prefabPath = AssetDatabase.GUIDToAssetPath(guids[i]);
            GameObject prefabRoot = null;

            try
            {
                prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
                if (prefabRoot == null)
                {
                    AddWarning(result, null, "missing_space_block_prefab_root", prefabPath, "Failed to load space block prefab contents.");
                    continue;
                }

                MemorySpaceBlock block = prefabRoot.GetComponent<MemorySpaceBlock>();
                if (block == null)
                {
                    block = prefabRoot.GetComponentInChildren<MemorySpaceBlock>(true);
                }

                if (block == null)
                {
                    AddWarning(result, null, "missing_memory_space_block", prefabPath, "Prefab under SpaceBlocks does not contain MemorySpaceBlock.");
                    continue;
                }

                BlockPreviewRecord record = ResolveOrCreateBlockRecord(result, block, prefabPath, blocksByAssetPath, blocksByTypeId);
                record.sourcePrefabPath = prefabPath;
                record.displayName = FirstNonEmpty(record.displayName, block.name);
                record.gridWidth = record.gridWidth > 0 ? record.gridWidth : RoomSlotGridUtility.GetGridWidth(block);
                record.gridDepth = record.gridDepth > 0 ? record.gridDepth : RoomSlotGridUtility.GetGridDepth(block);
                record.gridSize = record.gridSize > 0f ? record.gridSize : RoomSlotGridUtility.GetGridSize(block);
                record.previewBounds = CreatePreviewBounds(
                    record.gridWidth,
                    record.gridDepth,
                    record.gridSize,
                    ResolveBlockHeight(block));
                record.doorwayPorts = CreateDoorwayPorts(block);

                if (record.doorwayPorts.Count == 0 && HasDoorwayLikeContent(block, record))
                {
                    AddWarning(
                        result,
                        record.warnings,
                        "missing_space_opening_port",
                        prefabPath,
                        $"Block prefab '{block.name}' appears to contain doorway content but no SpaceOpeningPort components were exported.");
                }
            }
            catch (Exception exception)
            {
                AddWarning(result, null, "space_block_prefab_scan_failed", prefabPath, exception.Message);
            }
            finally
            {
                if (prefabRoot != null)
                {
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                }
            }
        }
    }

    private static BlockPreviewRecord ResolveOrCreateBlockRecord(
        AppPreviewCatalogScanResult result,
        MemorySpaceBlock block,
        string prefabPath,
        Dictionary<string, BlockPreviewRecord> blocksByAssetPath,
        Dictionary<string, BlockPreviewRecord> blocksByTypeId)
    {
        string definitionPath = block.blockDefinition != null ? AssetDatabase.GetAssetPath(block.blockDefinition) : string.Empty;
        if (!string.IsNullOrWhiteSpace(definitionPath) && blocksByAssetPath.TryGetValue(definitionPath, out BlockPreviewRecord byAsset))
        {
            return byAsset;
        }

        string blockTypeId = RoomSlotGridUtility.GetBlockTypeId(block);
        if (!string.IsNullOrWhiteSpace(blockTypeId) && blocksByTypeId.TryGetValue(blockTypeId, out BlockPreviewRecord byType))
        {
            return byType;
        }

        List<string> warnings = new List<string>();
        if (block.blockDefinition == null)
        {
            AddWarning(result, warnings, "missing_space_block_definition", prefabPath, $"Space block prefab '{block.name}' has no linked SpaceBlockDefinition.");
        }

        if (string.IsNullOrWhiteSpace(blockTypeId))
        {
            blockTypeId = Path.GetFileNameWithoutExtension(prefabPath);
            AddWarning(result, warnings, "missing_blockTypeId", prefabPath, $"Space block prefab '{block.name}' is missing blockTypeId. Falling back to prefab name.");
        }

        BlockPreviewRecord record = new BlockPreviewRecord();
        record.blockTypeId = blockTypeId;
        record.sourceAssetPath = definitionPath;
        record.sourcePrefabPath = prefabPath;
        record.displayName = block.name;
        record.gridWidth = RoomSlotGridUtility.GetGridWidth(block);
        record.gridDepth = RoomSlotGridUtility.GetGridDepth(block);
        record.gridSize = RoomSlotGridUtility.GetGridSize(block);
        record.previewMode = "procedural";
        record.previewAssetKey = "preview-block-" + SanitizeKey(blockTypeId);
        record.previewBounds = CreatePreviewBounds(record.gridWidth, record.gridDepth, record.gridSize, ResolveBlockHeight(block));
        record.warnings = warnings;

        if (block.blockDefinition != null)
        {
            record.structuralPlacements = ConvertPlacements(block.blockDefinition.placements);
            record.wallEdges = CreateWallEdges(block.blockDefinition.placements);
        }

        result.blocks.Add(record);
        if (!string.IsNullOrWhiteSpace(definitionPath))
        {
            blocksByAssetPath[definitionPath] = record;
        }

        if (!string.IsNullOrWhiteSpace(blockTypeId))
        {
            blocksByTypeId[blockTypeId] = record;
        }

        return record;
    }

    private static void ScanFurniturePrefabs(
        AppPreviewCatalogScanResult result,
        Dictionary<string, FurniturePrefabDescriptor> furnitureByName,
        Dictionary<string, FurniturePrefabDescriptor> furnitureByPath)
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { DisplayFurniturePrefabRoot });
        result.summary.furniturePrefabCount = guids != null ? guids.Length : 0;

        if (guids == null)
        {
            return;
        }

        for (int i = 0; i < guids.Length; i++)
        {
            string prefabPath = AssetDatabase.GUIDToAssetPath(guids[i]);
            GameObject prefabRoot = null;

            try
            {
                prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
                if (prefabRoot == null)
                {
                    AddWarning(result, null, "missing_furniture_prefab_root", prefabPath, "Failed to load display furniture prefab contents.");
                    continue;
                }

                MemoryDisplayFurniture furniture = prefabRoot.GetComponent<MemoryDisplayFurniture>();
                if (furniture == null)
                {
                    furniture = prefabRoot.GetComponentInChildren<MemoryDisplayFurniture>(true);
                }

                if (furniture == null)
                {
                    AddWarning(result, null, "missing_memory_display_furniture", prefabPath, "Prefab under DisplayFurniture does not contain MemoryDisplayFurniture.");
                    continue;
                }

                FurniturePrefabDescriptor descriptor = new FurniturePrefabDescriptor();
                descriptor.assetPath = prefabPath;
                descriptor.prefabName = prefabRoot.name;
                descriptor.furnitureId = FirstNonEmpty(furniture.FurnitureId, prefabRoot.name);
                descriptor.furnitureType = furniture.Type;
                descriptor.dimensions = ResolveFurnitureDimensions(furniture, furniture.transform);
                descriptor.slots = CreateFurnitureSlots(furniture);

                furnitureByName[descriptor.prefabName] = descriptor;
                furnitureByPath[prefabPath] = descriptor;
            }
            catch (Exception exception)
            {
                AddWarning(result, null, "furniture_prefab_scan_failed", prefabPath, exception.Message);
            }
            finally
            {
                if (prefabRoot != null)
                {
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                }
            }
        }
    }

    private static void ScanFurniturePlacements(
        AppPreviewCatalogScanResult result,
        Dictionary<string, FurniturePrefabDescriptor> furnitureByName)
    {
        string[] blockPrefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { SpaceBlockPrefabRoot });

        if (blockPrefabGuids == null)
        {
            return;
        }

        for (int i = 0; i < blockPrefabGuids.Length; i++)
        {
            string prefabPath = AssetDatabase.GUIDToAssetPath(blockPrefabGuids[i]);
            GameObject prefabRoot = null;

            try
            {
                prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
                if (prefabRoot == null)
                {
                    continue;
                }

                MemorySpaceBlock block = prefabRoot.GetComponent<MemorySpaceBlock>();
                if (block == null)
                {
                    block = prefabRoot.GetComponentInChildren<MemorySpaceBlock>(true);
                }

                if (block == null)
                {
                    continue;
                }

                RoomSlotPlacementMetadata[] placements = block.GetComponentsInChildren<RoomSlotPlacementMetadata>(true);
                MemoryDisplayFurniture[] furniture = block.GetComponentsInChildren<MemoryDisplayFurniture>(true);
                if ((placements == null || placements.Length == 0) && furniture != null && furniture.Length > 0)
                {
                    AddWarning(result, null, "missing_room_slot_placement_metadata", prefabPath, $"Block prefab '{block.name}' contains display furniture but no RoomSlotPlacementMetadata.");
                }

                if (placements == null)
                {
                    continue;
                }

                result.summary.furniturePlacementCount += placements.Length;
                for (int placementIndex = 0; placementIndex < placements.Length; placementIndex++)
                {
                    RoomSlotPlacementMetadata metadata = placements[placementIndex];
                    if (metadata == null)
                    {
                        continue;
                    }

                    FurniturePreviewRecord record = CreateFurniturePlacementRecord(result, block, metadata, prefabPath, furnitureByName);
                    result.furniture.Add(record);
                }
            }
            catch (Exception exception)
            {
                AddWarning(result, null, "furniture_placement_scan_failed", prefabPath, exception.Message);
            }
            finally
            {
                if (prefabRoot != null)
                {
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                }
            }
        }
    }

    private static FurniturePreviewRecord CreateFurniturePlacementRecord(
        AppPreviewCatalogScanResult result,
        MemorySpaceBlock block,
        RoomSlotPlacementMetadata metadata,
        string ownerPrefabPath,
        Dictionary<string, FurniturePrefabDescriptor> furnitureByName)
    {
        List<string> warnings = new List<string>();
        MemoryDisplayFurniture furniture = metadata.GetComponent<MemoryDisplayFurniture>();
        if (furniture == null)
        {
            furniture = metadata.GetComponentInChildren<MemoryDisplayFurniture>(true);
        }

        string sourcePrefabPath = ResolveFurnitureSourcePrefabPath(metadata, furnitureByName);
        FurniturePrefabDescriptor descriptor = null;
        if (!string.IsNullOrWhiteSpace(sourcePrefabPath))
        {
            string prefabName = Path.GetFileNameWithoutExtension(sourcePrefabPath);
            furnitureByName.TryGetValue(prefabName, out descriptor);
        }
        else if (!string.IsNullOrWhiteSpace(metadata.slotPrefabKey))
        {
            furnitureByName.TryGetValue(metadata.slotPrefabKey, out descriptor);
        }

        string furnitureId = FirstNonEmpty(metadata.furnitureId, furniture != null ? furniture.FurnitureId : string.Empty, metadata.slotPlacementId, metadata.slotPrefabKey);
        if (string.IsNullOrWhiteSpace(metadata.furnitureId))
        {
            AddWarning(result, warnings, "missing_furnitureId", ownerPrefabPath, $"RoomSlotPlacementMetadata '{metadata.name}' is missing furnitureId. Falling back to placement identity.");
        }

        FurniturePreviewRecord record = new FurniturePreviewRecord();
        record.furnitureId = furnitureId;
        record.sourcePrefabPath = sourcePrefabPath;
        record.previewAssetKey = "preview-furniture-" + SanitizeKey(furnitureId);
        record.previewMode = "primitive";
        record.dimensions = ResolveFurniturePlacementDimensions(block, metadata, furniture, descriptor);
        record.silhouetteType = ResolveFurnitureSilhouette(
            FirstNonEmpty(metadata.slotPrefabKey, descriptor != null ? descriptor.prefabName : string.Empty, metadata.name),
            furniture != null ? furniture.Type : descriptor != null ? descriptor.furnitureType : FurnitureType.Custom,
            record.dimensions);
        record.blockTypeId = FirstNonEmpty(metadata.blockTypeId, RoomSlotGridUtility.GetBlockTypeId(block));
        record.blockInstanceId = FirstNonEmpty(metadata.blockInstanceId, RoomSlotGridUtility.GetBlockInstanceId(block));
        record.surfaceType = metadata.surfaceType.ToString().ToLowerInvariant();
        record.warnings = warnings;

        if (metadata.surfaceType == RoomSlotSurfaceType.Floor)
        {
            record.floorAnchor = new FurnitureFloorAnchorRecord
            {
                gridX = metadata.gridX,
                gridZ = metadata.gridZ,
                widthUnits = metadata.widthUnits,
                depthUnits = metadata.depthUnits,
                floorGridXHalf = RoomSlotGridUtility.GetFloorGridXHalf(metadata),
                floorGridZHalf = RoomSlotGridUtility.GetFloorGridZHalf(metadata),
                floorWidthHalf = RoomSlotGridUtility.GetFloorWidthHalf(metadata),
                floorDepthHalf = RoomSlotGridUtility.GetFloorDepthHalf(metadata),
                rotationY = metadata.rotationY,
                localPosition = metadata.localPosition,
                localEulerAngles = metadata.localEulerAngles
            };
        }
        else
        {
            record.wallAnchor = new FurnitureWallAnchorRecord
            {
                wallSide = metadata.wallSide.ToString(),
                wallGridPosition = metadata.wallGridPosition,
                widthUnits = metadata.widthUnits,
                rotationY = metadata.rotationY,
                heightOffset = metadata.heightOffset,
                wallLayerIndex = metadata.wallLayerIndex,
                wallLayerCount = Mathf.Max(1, metadata.wallLayerCount),
                wallSurfaceHeight = metadata.wallSurfaceHeight,
                localPosition = metadata.localPosition,
                localEulerAngles = metadata.localEulerAngles
            };
        }

        record.slots = furniture != null ? CreateFurnitureSlots(furniture) : descriptor != null ? CloneSlots(descriptor.slots) : new List<FurnitureSlotPreviewRecord>();
        for (int i = 0; i < record.slots.Count; i++)
        {
            record.slotIds.Add(record.slots[i].slotId);
            if (string.IsNullOrWhiteSpace(record.slots[i].slotId))
            {
                AddWarning(result, record.warnings, "missing_slotId", ownerPrefabPath, $"Furniture placement '{metadata.name}' has a display slot without slotId.");
            }
        }

        if (record.slotIds.Count == 0 && metadata.slotIds != null)
        {
            for (int i = 0; i < metadata.slotIds.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(metadata.slotIds[i]))
                {
                    AddWarning(result, record.warnings, "missing_slotId", ownerPrefabPath, $"RoomSlotPlacementMetadata '{metadata.name}' contains an empty fallback slotId.");
                    continue;
                }

                record.slotIds.Add(metadata.slotIds[i]);
            }
        }

        if (string.IsNullOrWhiteSpace(record.sourcePrefabPath))
        {
            AddWarning(result, record.warnings, "missing_furniture_source_prefab", ownerPrefabPath, $"Could not resolve a source display furniture prefab from slotPrefabKey '{metadata.slotPrefabKey}'.");
        }

        if (string.IsNullOrWhiteSpace(record.blockTypeId))
        {
            AddWarning(result, record.warnings, "missing_blockTypeId", ownerPrefabPath, $"Furniture placement '{metadata.name}' is missing blockTypeId.");
        }

        if (string.IsNullOrWhiteSpace(record.blockInstanceId))
        {
            AddWarning(result, record.warnings, "missing_blockInstanceId", ownerPrefabPath, $"Furniture placement '{metadata.name}' is missing blockInstanceId.");
        }

        return record;
    }

    private static void AddStandaloneFurnitureRecords(
        AppPreviewCatalogScanResult result,
        Dictionary<string, FurniturePrefabDescriptor> furnitureByPath)
    {
        HashSet<string> exportedSourcePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < result.furniture.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(result.furniture[i].sourcePrefabPath))
            {
                exportedSourcePaths.Add(result.furniture[i].sourcePrefabPath);
            }
        }

        foreach (KeyValuePair<string, FurniturePrefabDescriptor> entry in furnitureByPath)
        {
            if (exportedSourcePaths.Contains(entry.Key))
            {
                continue;
            }

            FurniturePrefabDescriptor descriptor = entry.Value;
            FurniturePreviewRecord record = new FurniturePreviewRecord();
            record.furnitureId = descriptor.furnitureId;
            record.sourcePrefabPath = descriptor.assetPath;
            record.previewAssetKey = "preview-furniture-" + SanitizeKey(descriptor.furnitureId);
            record.previewMode = "primitive";
            record.silhouetteType = ResolveFurnitureSilhouette(descriptor.prefabName, descriptor.furnitureType, descriptor.dimensions);
            record.dimensions = descriptor.dimensions;
            record.surfaceType = "unknown";
            record.slots = CloneSlots(descriptor.slots);
            for (int i = 0; i < record.slots.Count; i++)
            {
                record.slotIds.Add(record.slots[i].slotId);
            }

            result.furniture.Add(record);
        }
    }

    private static void ScanItems(AppPreviewCatalogScanResult result)
    {
        Dictionary<string, MemoryItemData> itemDataById = new Dictionary<string, MemoryItemData>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string> itemDataPathById = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        string[] itemDataGuids = AssetDatabase.FindAssets("t:MemoryItemData", GetExistingRoots(ScriptableObjectRoot, DataRoot));
        result.summary.memoryItemDataCount = itemDataGuids != null ? itemDataGuids.Length : 0;
        if (itemDataGuids != null)
        {
            for (int i = 0; i < itemDataGuids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(itemDataGuids[i]);
                MemoryItemData data = AssetDatabase.LoadAssetAtPath<MemoryItemData>(assetPath);
                if (data == null)
                {
                    AddWarning(result, null, "missing_memory_item_data", assetPath, "Failed to load MemoryItemData asset.");
                    continue;
                }

                string itemId = FirstNonEmpty(data.ItemId, Path.GetFileNameWithoutExtension(assetPath));
                itemDataById[itemId] = data;
                itemDataPathById[itemId] = assetPath;
            }
        }

        HashSet<string> exportedItemIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { MemoryItemPrefabRoot });
        result.summary.memoryItemPrefabCount = prefabGuids != null ? prefabGuids.Length : 0;

        if (prefabGuids != null)
        {
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                GameObject prefabRoot = null;

                try
                {
                    prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
                    if (prefabRoot == null)
                    {
                        AddWarning(result, null, "missing_item_prefab_root", prefabPath, "Failed to load memory item prefab contents.");
                        continue;
                    }

                    MemoryObject memoryObject = prefabRoot.GetComponent<MemoryObject>();
                    if (memoryObject == null)
                    {
                        memoryObject = prefabRoot.GetComponentInChildren<MemoryObject>(true);
                    }

                    if (memoryObject == null)
                    {
                        AddWarning(result, null, "missing_memory_object", prefabPath, "Prefab under MemoryItems does not contain MemoryObject.");
                        continue;
                    }

                    ItemPreviewRecord record = CreateItemRecord(result, prefabRoot, memoryObject, prefabPath, itemDataById, itemDataPathById);
                    result.items.Add(record);
                    if (!string.IsNullOrWhiteSpace(record.itemId))
                    {
                        exportedItemIds.Add(record.itemId);
                    }
                }
                catch (Exception exception)
                {
                    AddWarning(result, null, "item_prefab_scan_failed", prefabPath, exception.Message);
                }
                finally
                {
                    if (prefabRoot != null)
                    {
                        PrefabUtility.UnloadPrefabContents(prefabRoot);
                    }
                }
            }
        }

        foreach (KeyValuePair<string, MemoryItemData> entry in itemDataById)
        {
            if (exportedItemIds.Contains(entry.Key))
            {
                continue;
            }

            List<string> warnings = new List<string>();
            string dataPath = itemDataPathById.TryGetValue(entry.Key, out string resolvedPath) ? resolvedPath : string.Empty;
            AddWarning(result, warnings, "missing_item_prefab", dataPath, $"MemoryItemData '{entry.Key}' has no matching prefab export in MemoryItems.");

            ItemPreviewRecord record = new ItemPreviewRecord();
            record.itemId = entry.Key;
            record.displayName = FirstNonEmpty(entry.Value.ItemName, entry.Key);
            record.sourceDataAssetPath = dataPath;
            record.previewAssetKey = "preview-item-" + SanitizeKey(entry.Key);
            record.previewMode = ResolveItemPreviewMode(entry.Value, Vector3.zero);
            record.silhouetteType = ResolveItemSilhouette(entry.Key, entry.Value.ItemName);
            record.dimensions = Vector3.zero;
            record.enablePlacement = false;
            record.emotionType = entry.Value.EmotionType;
            record.thumbnailHint = ResolveThumbnailHint(entry.Value);
            record.warnings = warnings;
            result.items.Add(record);
        }
    }

    private static ItemPreviewRecord CreateItemRecord(
        AppPreviewCatalogScanResult result,
        GameObject prefabRoot,
        MemoryObject memoryObject,
        string prefabPath,
        Dictionary<string, MemoryItemData> itemDataById,
        Dictionary<string, string> itemDataPathById)
    {
        List<string> warnings = new List<string>();
        MemoryItemData data = memoryObject.MemoryItemData;
        string itemId = FirstNonEmpty(memoryObject.ItemId, data != null ? data.ItemId : string.Empty, Path.GetFileNameWithoutExtension(prefabPath));
        if (string.IsNullOrWhiteSpace(memoryObject.ItemId) && (data == null || string.IsNullOrWhiteSpace(data.ItemId)))
        {
            AddWarning(result, warnings, "missing_itemId", prefabPath, $"Memory item prefab '{prefabRoot.name}' is missing itemId. Falling back to prefab name.");
        }

        if (data == null && itemDataById.TryGetValue(itemId, out MemoryItemData fallbackData))
        {
            data = fallbackData;
        }

        ItemPreviewRecord record = new ItemPreviewRecord();
        record.itemId = itemId;
        record.displayName = FirstNonEmpty(memoryObject.ItemName, data != null ? data.ItemName : string.Empty, prefabRoot.name);
        record.sourcePrefabPath = prefabPath;
        record.sourceDataAssetPath = data != null ? AssetDatabase.GetAssetPath(data) : itemDataPathById.TryGetValue(itemId, out string dataPath) ? dataPath : string.Empty;
        record.previewAssetKey = "preview-item-" + SanitizeKey(itemId);
        record.dimensions = ResolveLocalDimensions(memoryObject.transform);
        record.previewMode = ResolveItemPreviewMode(data, record.dimensions);
        record.silhouetteType = ResolveItemSilhouette(prefabRoot.name, record.displayName);
        record.itemSize = memoryObject.GetPlacementItemSize().ToString();
        record.enablePlacement = memoryObject.EnablePlacement;
        record.emotionType = FirstNonEmpty(memoryObject.EmotionType, data != null ? data.EmotionType : string.Empty);
        record.thumbnailHint = ResolveThumbnailHint(data);
        record.warnings = warnings;

        IReadOnlyList<SlotType> allowedSlotTypes = memoryObject.AllowedSlotTypes;
        if (allowedSlotTypes != null)
        {
            for (int i = 0; i < allowedSlotTypes.Count; i++)
            {
                record.allowedSlotTypes.Add(allowedSlotTypes[i].ToString());
            }
        }

        if (data == null)
        {
            AddWarning(result, record.warnings, "missing_memory_item_data", prefabPath, $"Memory item prefab '{prefabRoot.name}' has no linked MemoryItemData.");
        }

        return record;
    }

    private static void BuildManifest(AppPreviewCatalogScanResult result)
    {
        result.manifest.Clear();

        for (int i = 0; i < result.blocks.Count; i++)
        {
            BlockPreviewRecord block = result.blocks[i];
            PreviewAssetManifestRecord record = new PreviewAssetManifestRecord();
            record.previewAssetKey = block.previewAssetKey;
            record.sourceType = "block";
            record.sourceAssetPath = FirstNonEmpty(block.sourceAssetPath, block.sourcePrefabPath);
            record.previewMode = block.previewMode;
            record.recommendedAppRenderer = "BlockProceduralPreview";
            record.fallbackShape = "blockGrid";
            record.dimensions = block.previewBounds != null ? block.previewBounds.size : Vector3.zero;
            record.notes.Add("Use block grid, wall edges, and doorway ports to rebuild a lightweight preview.");
            AppendWarnings(record.notes, block.warnings);
            result.manifest.Add(record);
        }

        for (int i = 0; i < result.furniture.Count; i++)
        {
            FurniturePreviewRecord furniture = result.furniture[i];
            PreviewAssetManifestRecord record = new PreviewAssetManifestRecord();
            record.previewAssetKey = furniture.previewAssetKey;
            record.sourceType = "furniture";
            record.sourceAssetPath = furniture.sourcePrefabPath;
            record.previewMode = furniture.previewMode;
            record.recommendedAppRenderer = "PrimitiveFurniturePreview";
            record.fallbackShape = FirstNonEmpty(furniture.silhouetteType, "box");
            record.dimensions = furniture.dimensions;
            record.notes.Add("Treat this as a lightweight silhouette instead of a direct Unity prefab import.");
            AppendWarnings(record.notes, furniture.warnings);
            result.manifest.Add(record);
        }

        for (int i = 0; i < result.items.Count; i++)
        {
            ItemPreviewRecord item = result.items[i];
            PreviewAssetManifestRecord record = new PreviewAssetManifestRecord();
            record.previewAssetKey = item.previewAssetKey;
            record.sourceType = "item";
            record.sourceAssetPath = FirstNonEmpty(item.sourcePrefabPath, item.sourceDataAssetPath);
            record.previewMode = item.previewMode;
            record.recommendedAppRenderer = item.previewMode == "billboard" ? "BillboardItemPreview" : "PrimitiveItemPreview";
            record.fallbackShape = FirstNonEmpty(item.silhouetteType, "unknown");
            record.dimensions = item.dimensions;
            record.thumbnailPath = item.thumbnailHint;
            record.notes.Add("Do not reproduce XR, gaze, rigidbody, or Memory Mode behavior in the App.");
            AppendWarnings(record.notes, item.warnings);
            result.manifest.Add(record);
        }
    }

    private static void FinalizeSummary(AppPreviewCatalogScanResult result)
    {
        result.summary.blockCount = result.blocks.Count;
        result.summary.furnitureCount = result.furniture.Count;
        result.summary.itemCount = result.items.Count;
        result.summary.warningCount = result.warnings.Count;

        int slotCount = 0;
        for (int i = 0; i < result.furniture.Count; i++)
        {
            if (result.furniture[i].slots != null)
            {
                slotCount += result.furniture[i].slots.Count;
            }
        }

        int portCount = 0;
        for (int i = 0; i < result.blocks.Count; i++)
        {
            if (result.blocks[i].doorwayPorts != null)
            {
                portCount += result.blocks[i].doorwayPorts.Count;
            }
        }

        result.summary.slotCount = slotCount;
        result.summary.portCount = portCount;
    }

    private static void ResolveDuplicateBlockPreviewKeys(AppPreviewCatalogScanResult result)
    {
        if (result == null || result.blocks == null || result.blocks.Count == 0)
        {
            return;
        }

        Dictionary<string, List<BlockPreviewRecord>> blocksByTypeId =
            new Dictionary<string, List<BlockPreviewRecord>>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < result.blocks.Count; i++)
        {
            BlockPreviewRecord record = result.blocks[i];
            string blockTypeId = FirstNonEmpty(record.blockTypeId, "(missing)");
            if (!blocksByTypeId.TryGetValue(blockTypeId, out List<BlockPreviewRecord> grouped))
            {
                grouped = new List<BlockPreviewRecord>();
                blocksByTypeId[blockTypeId] = grouped;
            }

            grouped.Add(record);
        }

        foreach (KeyValuePair<string, List<BlockPreviewRecord>> entry in blocksByTypeId)
        {
            if (entry.Value.Count <= 1)
            {
                continue;
            }

            HashSet<string> usedPreviewKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < entry.Value.Count; index++)
            {
                BlockPreviewRecord record = entry.Value[index];
                string sourceName = Path.GetFileNameWithoutExtension(FirstNonEmpty(record.sourceAssetPath, record.sourcePrefabPath));
                string basePreviewKey = "preview-block-" + SanitizeKey(FirstNonEmpty(sourceName, record.blockTypeId, "block"));
                string resolvedPreviewKey = basePreviewKey;
                int suffix = 2;
                while (!usedPreviewKeys.Add(resolvedPreviewKey))
                {
                    resolvedPreviewKey = basePreviewKey + "-" + suffix.ToString(CultureInfo.InvariantCulture);
                    suffix++;
                }

                if (!string.Equals(record.previewAssetKey, resolvedPreviewKey, StringComparison.OrdinalIgnoreCase))
                {
                    AddWarning(
                        result,
                        record.warnings,
                        "duplicate_blockTypeId",
                        FirstNonEmpty(record.sourceAssetPath, record.sourcePrefabPath),
                        $"Block type '{record.blockTypeId}' appears multiple times. Using previewAssetKey '{resolvedPreviewKey}' to keep v0 preview records unique.");
                    record.previewAssetKey = resolvedPreviewKey;
                }
            }
        }
    }

    private static void SortResult(AppPreviewCatalogScanResult result)
    {
        result.blocks.Sort((a, b) => string.Compare(a.blockTypeId, b.blockTypeId, StringComparison.OrdinalIgnoreCase));
        result.furniture.Sort((a, b) => string.Compare(a.furnitureId, b.furnitureId, StringComparison.OrdinalIgnoreCase));
        result.items.Sort((a, b) => string.Compare(a.itemId, b.itemId, StringComparison.OrdinalIgnoreCase));
        result.manifest.Sort((a, b) => string.Compare(a.previewAssetKey, b.previewAssetKey, StringComparison.OrdinalIgnoreCase));
        result.warnings.Sort((a, b) =>
        {
            int codeCompare = string.Compare(a.code, b.code, StringComparison.OrdinalIgnoreCase);
            if (codeCompare != 0)
            {
                return codeCompare;
            }

            return string.Compare(a.sourcePath, b.sourcePath, StringComparison.OrdinalIgnoreCase);
        });
    }

    private static List<StructuralPlacementPreviewRecord> ConvertPlacements(List<SpaceSegmentPlacementRecord> placements)
    {
        List<StructuralPlacementPreviewRecord> records = new List<StructuralPlacementPreviewRecord>();
        if (placements == null)
        {
            return records;
        }

        for (int i = 0; i < placements.Count; i++)
        {
            SpaceSegmentPlacementRecord placement = placements[i];
            if (placement == null)
            {
                continue;
            }

            StructuralPlacementPreviewRecord record = new StructuralPlacementPreviewRecord();
            record.placementId = placement.placementId;
            record.segmentId = placement.segmentId;
            record.category = placement.category.ToString();
            record.gridX = placement.gridX;
            record.gridZ = placement.gridZ;
            record.side = placement.side.ToString();
            record.rotationY = placement.rotationY;
            record.footprint = placement.footprint;
            record.overlaySegmentId = placement.overlaySegmentId;
            record.isConnectorCandidate = placement.isConnectorCandidate;
            records.Add(record);
        }

        return records;
    }

    private static List<WallEdgePreviewRecord> CreateWallEdges(List<SpaceSegmentPlacementRecord> placements)
    {
        List<WallEdgePreviewRecord> records = new List<WallEdgePreviewRecord>();
        if (placements == null)
        {
            return records;
        }

        for (int i = 0; i < placements.Count; i++)
        {
            SpaceSegmentPlacementRecord placement = placements[i];
            if (placement == null || placement.category != SegmentCategory.Wall)
            {
                continue;
            }

            List<string> edgeKeys = RoomSlotGridUtility.GetWallEdgeKeys(
                placement.gridX,
                placement.gridZ,
                placement.side,
                Mathf.Max(1, placement.footprint.x));
            for (int edgeIndex = 0; edgeIndex < edgeKeys.Count; edgeIndex++)
            {
                WallEdgePreviewRecord edge = new WallEdgePreviewRecord();
                edge.side = placement.side.ToString();
                edge.edgeKey = edgeKeys[edgeIndex];
                edge.gridX = placement.gridX;
                edge.gridZ = placement.gridZ;
                edge.spanUnits = Mathf.Max(1, placement.footprint.x);
                records.Add(edge);
            }
        }

        return records;
    }

    private static List<DoorwayPortPreviewRecord> CreateDoorwayPorts(MemorySpaceBlock block)
    {
        List<DoorwayPortPreviewRecord> records = new List<DoorwayPortPreviewRecord>();
        if (block == null)
        {
            return records;
        }

        SpaceOpeningPort[] ports = block.GetComponentsInChildren<SpaceOpeningPort>(true);
        for (int i = 0; i < ports.Length; i++)
        {
            SpaceOpeningPort port = ports[i];
            if (port == null)
            {
                continue;
            }

            Transform anchor = port.EffectiveAnchor;
            DoorwayPortPreviewRecord record = new DoorwayPortPreviewRecord();
            record.portId = FirstNonEmpty(port.openingId, port.name);
            record.openingType = port.openingType.ToString();
            record.connectionKind = port.connectionKind.ToString();
            record.widthUnits = port.widthUnits;
            record.height = port.height;
            record.wallSide = port.wallSide.ToString();
            record.gridPosition = port.gridPosition;
            record.localPosition = anchor != null
                ? block.transform.InverseTransformPoint(anchor.position)
                : block.transform.InverseTransformPoint(port.transform.position);
            record.localForward = anchor != null
                ? block.transform.InverseTransformDirection(anchor.forward)
                : block.transform.InverseTransformDirection(port.transform.forward);
            record.isOccupied = port.isOccupied || port.connectedPort != null;
            records.Add(record);
        }

        return records;
    }

    private static bool HasDoorwayLikeContent(MemorySpaceBlock block, BlockPreviewRecord record)
    {
        if (record != null && record.structuralPlacements != null)
        {
            for (int i = 0; i < record.structuralPlacements.Count; i++)
            {
                StructuralPlacementPreviewRecord placement = record.structuralPlacements[i];
                if (!string.IsNullOrWhiteSpace(placement.overlaySegmentId)
                    && placement.overlaySegmentId.IndexOf("door", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
        }

        if (block == null || block.wallSegments == null)
        {
            return false;
        }

        for (int i = 0; i < block.wallSegments.Count; i++)
        {
            WallSegmentSlot slot = block.wallSegments[i];
            if (slot == null)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(slot.overlayId)
                && slot.overlayId.IndexOf("door", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private static float ResolveBlockHeight(MemorySpaceBlock block)
    {
        if (block == null || block.segmentKit == null || block.segmentKit.segments == null)
        {
            return DefaultBlockHeight;
        }

        float height = 0f;
        for (int i = 0; i < block.segmentKit.segments.Count; i++)
        {
            SpaceSegmentDefinition definition = block.segmentKit.segments[i];
            if (definition == null || definition.category != SegmentCategory.Wall)
            {
                continue;
            }

            height = Mathf.Max(height, definition.height);
        }

        return height > 0f ? height : DefaultBlockHeight;
    }

    private static string ResolveFurnitureSourcePrefabPath(
        RoomSlotPlacementMetadata metadata,
        Dictionary<string, FurniturePrefabDescriptor> furnitureByName)
    {
        if (metadata == null)
        {
            return string.Empty;
        }

        string nearestPrefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(metadata.gameObject);
        if (!string.IsNullOrWhiteSpace(nearestPrefabPath)
            && nearestPrefabPath.StartsWith(DisplayFurniturePrefabRoot, StringComparison.OrdinalIgnoreCase))
        {
            return nearestPrefabPath;
        }

        if (!string.IsNullOrWhiteSpace(metadata.slotPrefabKey)
            && furnitureByName.TryGetValue(metadata.slotPrefabKey, out FurniturePrefabDescriptor descriptor))
        {
            return descriptor.assetPath;
        }

        return string.Empty;
    }

    private static Vector3 ResolveFurniturePlacementDimensions(
        MemorySpaceBlock block,
        RoomSlotPlacementMetadata metadata,
        MemoryDisplayFurniture furniture,
        FurniturePrefabDescriptor descriptor)
    {
        if (furniture != null)
        {
            Vector3 dimensions = ResolveFurnitureDimensions(furniture, furniture.transform);
            if (dimensions.sqrMagnitude > 0f)
            {
                return dimensions;
            }
        }

        if (descriptor != null && descriptor.dimensions.sqrMagnitude > 0f)
        {
            return descriptor.dimensions;
        }

        float gridSize = block != null ? RoomSlotGridUtility.GetGridSize(block) : 1f;
        if (metadata.surfaceType == RoomSlotSurfaceType.Wall)
        {
            return new Vector3(
                Mathf.Max(1, metadata.widthUnits) * gridSize,
                metadata.wallSurfaceHeight > 0f && metadata.wallLayerCount > 0
                    ? metadata.wallSurfaceHeight / Mathf.Max(1, metadata.wallLayerCount)
                    : 1f,
                gridSize * 0.2f);
        }

        return new Vector3(
            Mathf.Max(1, metadata.widthUnits) * gridSize,
            Mathf.Max(0.1f, gridSize * 0.6f),
            Mathf.Max(1, metadata.depthUnits) * gridSize);
    }

    private static Vector3 ResolveFurnitureDimensions(MemoryDisplayFurniture furniture, Transform furnitureRoot)
    {
        if (furniture == null || furnitureRoot == null)
        {
            return Vector3.zero;
        }

        furniture.AutoAssignPlacementBounds();
        if (furniture.PlacementBoundsCollider != null)
        {
            return MultiplyByAbsLossyScale(furniture.PlacementBoundsCollider.size, furniture.PlacementBoundsCollider.transform.lossyScale);
        }

        return ResolveLocalDimensions(furnitureRoot);
    }

    private static List<FurnitureSlotPreviewRecord> CreateFurnitureSlots(MemoryDisplayFurniture furniture)
    {
        List<FurnitureSlotPreviewRecord> records = new List<FurnitureSlotPreviewRecord>();
        if (furniture == null)
        {
            return records;
        }

        furniture.AutoCollectSlots();
        IReadOnlyList<MemoryDisplaySlot> slots = furniture.Slots;
        if (slots == null)
        {
            return records;
        }

        Transform root = furniture.transform;
        for (int i = 0; i < slots.Count; i++)
        {
            MemoryDisplaySlot slot = slots[i];
            if (slot == null)
            {
                continue;
            }

            FurnitureSlotPreviewRecord record = new FurnitureSlotPreviewRecord();
            record.slotId = slot.SlotId;
            record.slotType = slot.Type.ToString();
            record.localPosition = root.InverseTransformPoint(slot.transform.position);
            record.localRotation = slot.transform.localEulerAngles;
            if (slot.AcceptedItemSizes != null)
            {
                for (int sizeIndex = 0; sizeIndex < slot.AcceptedItemSizes.Count; sizeIndex++)
                {
                    record.acceptedItemSizes.Add(slot.AcceptedItemSizes[sizeIndex].ToString());
                }
            }

            if (slot.OccupiedItem != null)
            {
                MemoryObject occupiedMemoryObject = slot.OccupiedItem.GetComponent<MemoryObject>();
                record.occupiedItemId = occupiedMemoryObject != null
                    ? FirstNonEmpty(occupiedMemoryObject.ItemId, slot.OccupiedItem.name)
                    : slot.OccupiedItem.name;
            }

            records.Add(record);
        }

        return records;
    }

    private static List<FurnitureSlotPreviewRecord> CloneSlots(List<FurnitureSlotPreviewRecord> source)
    {
        List<FurnitureSlotPreviewRecord> clone = new List<FurnitureSlotPreviewRecord>();
        if (source == null)
        {
            return clone;
        }

        for (int i = 0; i < source.Count; i++)
        {
            FurnitureSlotPreviewRecord slot = source[i];
            if (slot == null)
            {
                continue;
            }

            FurnitureSlotPreviewRecord copy = new FurnitureSlotPreviewRecord();
            copy.slotId = slot.slotId;
            copy.slotType = slot.slotType;
            copy.localPosition = slot.localPosition;
            copy.localRotation = slot.localRotation;
            copy.occupiedItemId = slot.occupiedItemId;
            for (int sizeIndex = 0; sizeIndex < slot.acceptedItemSizes.Count; sizeIndex++)
            {
                copy.acceptedItemSizes.Add(slot.acceptedItemSizes[sizeIndex]);
            }

            clone.Add(copy);
        }

        return clone;
    }

    private static Vector3 ResolveLocalDimensions(Transform root)
    {
        if (root == null)
        {
            return Vector3.zero;
        }

        if (!TryGetCompositeLocalBounds(root, out Bounds bounds))
        {
            return Vector3.zero;
        }

        return bounds.size;
    }

    private static bool TryGetCompositeLocalBounds(Transform root, out Bounds bounds)
    {
        bounds = default;
        Matrix4x4 worldToLocal = root.worldToLocalMatrix;
        bool hasBounds = false;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            EncapsulateWorldBounds(ref bounds, ref hasBounds, worldToLocal, renderers[i].bounds);
        }

        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            EncapsulateWorldBounds(ref bounds, ref hasBounds, worldToLocal, colliders[i].bounds);
        }

        return hasBounds;
    }

    private static void EncapsulateWorldBounds(ref Bounds aggregate, ref bool hasBounds, Matrix4x4 worldToLocal, Bounds source)
    {
        Vector3 min = source.min;
        Vector3 max = source.max;
        Vector3[] corners =
        {
            new Vector3(min.x, min.y, min.z),
            new Vector3(max.x, min.y, min.z),
            new Vector3(min.x, max.y, min.z),
            new Vector3(max.x, max.y, min.z),
            new Vector3(min.x, min.y, max.z),
            new Vector3(max.x, min.y, max.z),
            new Vector3(min.x, max.y, max.z),
            new Vector3(max.x, max.y, max.z)
        };

        for (int i = 0; i < corners.Length; i++)
        {
            Vector3 point = worldToLocal.MultiplyPoint3x4(corners[i]);
            if (!hasBounds)
            {
                aggregate = new Bounds(point, Vector3.zero);
                hasBounds = true;
            }
            else
            {
                aggregate.Encapsulate(point);
            }
        }
    }

    private static Vector3 MultiplyByAbsLossyScale(Vector3 vector, Vector3 lossyScale)
    {
        return new Vector3(
            Mathf.Abs(vector.x * lossyScale.x),
            Mathf.Abs(vector.y * lossyScale.y),
            Mathf.Abs(vector.z * lossyScale.z));
    }

    private static PreviewBoundsData CreatePreviewBounds(int gridWidth, int gridDepth, float gridSize, float height)
    {
        float resolvedGridSize = gridSize > 0f ? gridSize : 1f;
        float resolvedHeight = height > 0f ? height : DefaultBlockHeight;
        PreviewBoundsData data = new PreviewBoundsData();
        data.center = new Vector3(0f, resolvedHeight * 0.5f, 0f);
        data.size = new Vector3(
            Mathf.Max(0, gridWidth) * resolvedGridSize,
            resolvedHeight,
            Mathf.Max(0, gridDepth) * resolvedGridSize);
        return data;
    }

    private static string ResolveFurnitureSilhouette(string nameOrKey, FurnitureType type, Vector3 dimensions)
    {
        string normalized = FirstNonEmpty(nameOrKey).ToLowerInvariant();
        if (type == FurnitureType.WallShelf || normalized.Contains("wall"))
        {
            return "wallShelf";
        }

        if (type == FurnitureType.Shelf || normalized.Contains("shelf"))
        {
            return "shelf";
        }

        if (type == FurnitureType.Plinth || normalized.Contains("plinth") || normalized.Contains("pedestal"))
        {
            return "stand";
        }

        if (normalized.Contains("frame"))
        {
            return "frame";
        }

        if (normalized.Contains("lamp") || dimensions.y > Mathf.Max(dimensions.x, dimensions.z) * 1.25f)
        {
            return "stand";
        }

        if (normalized.Contains("table") || normalized.Contains("desk") || normalized.Contains("carpet"))
        {
            return "lowTable";
        }

        if (dimensions.y > 0f && dimensions.y < Mathf.Max(dimensions.x, dimensions.z) * 0.45f)
        {
            return "lowTable";
        }

        return "box";
    }

    private static string ResolveItemSilhouette(string prefabName, string displayName)
    {
        string normalized = FirstNonEmpty(prefabName, displayName).ToLowerInvariant();
        if (normalized.Contains("doll") || normalized.Contains("teddy"))
        {
            return "teddy";
        }

        if (normalized.Contains("cup") || normalized.Contains("mug"))
        {
            return "cup";
        }

        if (normalized.Contains("camera"))
        {
            return "camera";
        }

        if (normalized.Contains("photo") || normalized.Contains("frame"))
        {
            return "photo";
        }

        if (normalized.Contains("book"))
        {
            return "book";
        }

        if (normalized.Contains("ball") || normalized.Contains("sphere"))
        {
            return "sphere";
        }

        if (normalized.Contains("cube") || normalized.Contains("box"))
        {
            return "box";
        }

        return "unknown";
    }

    private static string ResolveItemPreviewMode(MemoryItemData data, Vector3 dimensions)
    {
        if (data != null && data.Photos != null && data.Photos.Length > 0)
        {
            return "billboard";
        }

        float minDimension = Mathf.Min(dimensions.x, Mathf.Min(dimensions.y, dimensions.z));
        float maxDimension = Mathf.Max(dimensions.x, Mathf.Max(dimensions.y, dimensions.z));
        if (minDimension > 0f && maxDimension > 0f && minDimension <= maxDimension * 0.12f)
        {
            return "billboard";
        }

        return "primitive";
    }

    private static string ResolveThumbnailHint(MemoryItemData data)
    {
        if (data == null || data.Photos == null || data.Photos.Length == 0 || data.Photos[0] == null)
        {
            return string.Empty;
        }

        return AssetDatabase.GetAssetPath(data.Photos[0]);
    }

    private static void AppendWarnings(List<string> notes, List<string> warnings)
    {
        if (notes == null || warnings == null)
        {
            return;
        }

        for (int i = 0; i < warnings.Count; i++)
        {
            notes.Add(warnings[i]);
        }
    }

    private static void AddWarning(
        AppPreviewCatalogScanResult result,
        List<string> recordWarnings,
        string code,
        string sourcePath,
        string message)
    {
        if (result == null || string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        result.warnings.Add(new AppPreviewCatalogWarning
        {
            code = code,
            sourcePath = sourcePath ?? string.Empty,
            message = message
        });

        if (recordWarnings != null)
        {
            recordWarnings.Add(code + ": " + message);
        }
    }

    private static string[] GetExistingRoots(params string[] roots)
    {
        List<string> existingRoots = new List<string>();
        if (roots == null)
        {
            return existingRoots.ToArray();
        }

        for (int i = 0; i < roots.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(roots[i]) && AssetDatabase.IsValidFolder(roots[i]))
            {
                existingRoots.Add(roots[i]);
            }
        }

        return existingRoots.ToArray();
    }

    private static string FirstNonEmpty(params string[] values)
    {
        if (values == null)
        {
            return string.Empty;
        }

        for (int i = 0; i < values.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(values[i]))
            {
                return values[i].Trim();
            }
        }

        return string.Empty;
    }

    private static string SanitizeKey(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return "unknown";
        }

        char[] chars = input.Trim().ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '_' && chars[i] != '-')
            {
                chars[i] = '_';
            }
        }

        return new string(chars);
    }
}
