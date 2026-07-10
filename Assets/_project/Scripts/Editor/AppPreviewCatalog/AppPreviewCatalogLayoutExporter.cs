using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static partial class AppPreviewCatalogExporter
{
    private const string GardenLayoutScenePath = "Assets/_project/Scenes/00_Prototype/_02_VR_test_Displayzone.unity";

    private static void ScanGardenLayout(AppPreviewCatalogScanResult result)
    {
        Scene layoutScene = ResolveGardenLayoutScene(result);
        GardenLayoutPreviewDocument layout = new GardenLayoutPreviewDocument();
        layout.exportedAt = result.generatedAtUtc;
        layout.sourceScene = layoutScene.IsValid() ? FirstNonEmpty(layoutScene.path, layoutScene.name) : GardenLayoutScenePath;
        result.gardenLayout = layout;

        List<GameObject> roots = CollectLayoutRoots(result, layoutScene);
        List<MemorySpaceBlock> blocks = CollectLayoutBlocks(roots);
        if (blocks.Count == 0)
        {
            AddLayoutWarning(
                result,
                "missing_layout_blocks",
                FirstNonEmpty(layout.sourceScene, "(no loaded scene)"),
                "No MemorySpaceBlock instances were found in the configured 02 garden layout scene.");
        }

        Dictionary<MemorySpaceBlock, string> blockInstanceIds = new Dictionary<MemorySpaceBlock, string>();
        HashSet<string> usedBlockInstanceIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < blocks.Count; i++)
        {
            MemorySpaceBlock block = blocks[i];
            GardenLayoutBlockInstanceRecord record = CreateLayoutBlockInstance(result, block);
            record.blockInstanceId = ResolveUniqueLayoutId(
                result,
                record.blockInstanceId,
                usedBlockInstanceIds,
                "duplicate_blockInstanceId",
                record.scenePath,
                GetSceneObjectPath(block.transform),
                "blockInstanceId");
            layout.blockInstances.Add(record);
            blockInstanceIds[block] = record.blockInstanceId;
        }

        Dictionary<MemoryDisplayFurniture, string> furnitureIds = new Dictionary<MemoryDisplayFurniture, string>();
        Dictionary<MemoryDisplaySlot, string> slotIds = new Dictionary<MemoryDisplaySlot, string>();
        HashSet<string> usedFurnitureIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        HashSet<string> usedSlotIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        ScanLayoutConnections(result, roots, blockInstanceIds);
        ScanLayoutFurniture(result, blocks, blockInstanceIds, furnitureIds, slotIds, usedFurnitureIds, usedSlotIds);
        ScanLayoutSlots(result, blocks, blockInstanceIds, furnitureIds, slotIds, usedSlotIds);
        ScanLayoutItems(result, roots, blockInstanceIds, furnitureIds, slotIds);
    }

    private static Scene ResolveGardenLayoutScene(AppPreviewCatalogScanResult result)
    {
        for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
        {
            Scene scene = SceneManager.GetSceneAt(sceneIndex);
            if (scene.IsValid()
                && scene.isLoaded
                && string.Equals(scene.path, GardenLayoutScenePath, StringComparison.OrdinalIgnoreCase))
            {
                result.scanRoots.Add("Garden layout scene: " + GardenLayoutScenePath);
                return scene;
            }
        }

        if (Application.isBatchMode && AssetDatabase.LoadAssetAtPath<SceneAsset>(GardenLayoutScenePath) != null)
        {
            Scene openedScene = EditorSceneManager.OpenScene(GardenLayoutScenePath, OpenSceneMode.Single);
            result.scanRoots.Add("Garden layout scene: " + GardenLayoutScenePath);
            return openedScene;
        }

        AddLayoutWarning(
            result,
            "missing_layout_scene",
            GardenLayoutScenePath,
            "The configured 02 garden layout scene is not loaded. Open it before exporting from the Editor, or run batchmode export.");
        return default;
    }

    private static List<GameObject> CollectLayoutRoots(AppPreviewCatalogScanResult result, Scene layoutScene)
    {
        List<GameObject> roots = new List<GameObject>();
        HashSet<int> rootIds = new HashSet<int>();

        if (layoutScene.IsValid() && layoutScene.isLoaded)
        {
            GameObject[] sceneRoots = layoutScene.GetRootGameObjects();
            for (int i = 0; i < sceneRoots.Length; i++)
            {
                AddLayoutRoot(sceneRoots[i], roots, rootIds);
            }
        }

        return roots;
    }

    private static void AddLayoutRoot(GameObject root, List<GameObject> roots, HashSet<int> rootIds)
    {
        if (root == null || roots == null || rootIds == null)
        {
            return;
        }

        int id = root.GetInstanceID();
        if (rootIds.Add(id))
        {
            roots.Add(root);
        }
    }

    private static List<MemorySpaceBlock> CollectLayoutBlocks(List<GameObject> roots)
    {
        List<MemorySpaceBlock> blocks = new List<MemorySpaceBlock>();
        HashSet<int> blockIds = new HashSet<int>();
        if (roots == null)
        {
            return blocks;
        }

        for (int i = 0; i < roots.Count; i++)
        {
            if (roots[i] == null)
            {
                continue;
            }

            MemorySpaceBlock[] rootBlocks = roots[i].GetComponentsInChildren<MemorySpaceBlock>(true);
            for (int blockIndex = 0; blockIndex < rootBlocks.Length; blockIndex++)
            {
                MemorySpaceBlock block = rootBlocks[blockIndex];
                if (block != null && blockIds.Add(block.GetInstanceID()))
                {
                    blocks.Add(block);
                }
            }
        }

        blocks.Sort((a, b) => string.Compare(GetSceneObjectPath(a.transform), GetSceneObjectPath(b.transform), StringComparison.OrdinalIgnoreCase));
        return blocks;
    }

    private static GardenLayoutBlockInstanceRecord CreateLayoutBlockInstance(AppPreviewCatalogScanResult result, MemorySpaceBlock block)
    {
        string sourcePath = GetSourcePath(block != null ? block.gameObject : null);
        string scenePath = GetScenePath(block != null ? block.gameObject : null);
        string blockInstanceId = ResolveLayoutBlockInstanceId(result, block, scenePath);
        string blockTypeId = ResolveLayoutBlockTypeId(result, block, scenePath);
        int gridWidth = block != null ? RoomSlotGridUtility.GetGridWidth(block) : 0;
        int gridDepth = block != null ? RoomSlotGridUtility.GetGridDepth(block) : 0;
        float gridSize = block != null ? RoomSlotGridUtility.GetGridSize(block) : 1f;
        float height = block != null ? ResolveBlockHeight(block) : DefaultBlockHeight;

        GardenLayoutBlockInstanceRecord record = new GardenLayoutBlockInstanceRecord();
        record.blockInstanceId = blockInstanceId;
        record.blockTypeId = blockTypeId;
        record.sourcePrefabPath = sourcePath;
        record.scenePath = scenePath;
        record.position = block.transform.position;
        record.rotationEuler = block.transform.eulerAngles;
        record.scale = block.transform.lossyScale;
        record.gridWidth = gridWidth;
        record.gridDepth = gridDepth;
        record.gridSize = gridSize;
        record.boundsCenter = block.transform.TransformPoint(new Vector3(0f, height * 0.5f, 0f));
        record.boundsSize = new Vector3(gridWidth * gridSize, height, gridDepth * gridSize);
        return record;
    }

    private static void ScanLayoutConnections(
        AppPreviewCatalogScanResult result,
        List<GameObject> roots,
        Dictionary<MemorySpaceBlock, string> blockInstanceIds)
    {
        HashSet<int> componentIds = new HashSet<int>();
        HashSet<string> connectionKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (roots == null)
        {
            return;
        }

        for (int i = 0; i < roots.Count; i++)
        {
            if (roots[i] == null)
            {
                continue;
            }

            SpaceConnection[] connections = roots[i].GetComponentsInChildren<SpaceConnection>(true);
            for (int connectionIndex = 0; connectionIndex < connections.Length; connectionIndex++)
            {
                SpaceConnection connection = connections[connectionIndex];
                if (connection != null && componentIds.Add(connection.GetInstanceID()))
                {
                    AddLayoutConnection(result, connection, connection.portA, connection.portB, connectionKeys, blockInstanceIds);
                }
            }
        }

        for (int i = 0; i < roots.Count; i++)
        {
            if (roots[i] == null)
            {
                continue;
            }

            SpaceOpeningPort[] ports = roots[i].GetComponentsInChildren<SpaceOpeningPort>(true);
            for (int portIndex = 0; portIndex < ports.Length; portIndex++)
            {
                SpaceOpeningPort port = ports[portIndex];
                if (port != null && port.connectedPort != null)
                {
                    AddLayoutConnection(result, null, port, port.connectedPort, connectionKeys, blockInstanceIds);
                }
            }
        }
    }

    private static void AddLayoutConnection(
        AppPreviewCatalogScanResult result,
        SpaceConnection connection,
        SpaceOpeningPort portA,
        SpaceOpeningPort portB,
        HashSet<string> connectionKeys,
        Dictionary<MemorySpaceBlock, string> blockInstanceIds)
    {
        string sourcePath = connection != null ? GetSceneObjectPath(connection.transform) : GetSceneObjectPath(portA != null ? portA.transform : null);
        if (portA == null || portB == null)
        {
            AddLayoutWarning(result, "missing_connection_port", sourcePath, "SpaceConnection is missing portA or portB.");
            return;
        }

        string keyA = portA.GetInstanceID().ToString(CultureInfo.InvariantCulture);
        string keyB = portB.GetInstanceID().ToString(CultureInfo.InvariantCulture);
        string connectionKey = string.CompareOrdinal(keyA, keyB) <= 0 ? keyA + "|" + keyB : keyB + "|" + keyA;
        if (!connectionKeys.Add(connectionKey))
        {
            return;
        }

        string portAId = ResolveLayoutPortId(result, portA, sourcePath);
        string portBId = ResolveLayoutPortId(result, portB, sourcePath);
        MemorySpaceBlock blockA = portA.OwningBlock;
        MemorySpaceBlock blockB = portB.OwningBlock;

        GardenLayoutConnectionRecord record = new GardenLayoutConnectionRecord();
        record.connectionId = "connection-" + SanitizeKey(FirstNonEmpty(portAId, "portA")) + "-" + SanitizeKey(FirstNonEmpty(portBId, "portB"));
        record.portAId = portAId;
        record.portBId = portBId;
        record.blockAInstanceId = ResolveKnownBlockInstanceId(result, blockA, blockInstanceIds, sourcePath);
        record.blockBInstanceId = ResolveKnownBlockInstanceId(result, blockB, blockInstanceIds, sourcePath);
        record.connectionKind = portA.connectionKind.ToString();
        record.widthUnits = portA.widthUnits;
        record.height = portA.height;
        record.connectorLength = connection != null && connection.connectorLength > 0f
            ? connection.connectorLength
            : Vector3.Distance(portA.EffectiveAnchor.position, portB.EffectiveAnchor.position);
        record.isPreview = connection == null || connection.isPreview;
        result.gardenLayout.connections.Add(record);
    }

    private static void ScanLayoutFurniture(
        AppPreviewCatalogScanResult result,
        List<MemorySpaceBlock> blocks,
        Dictionary<MemorySpaceBlock, string> blockInstanceIds,
        Dictionary<MemoryDisplayFurniture, string> furnitureIds,
        Dictionary<MemoryDisplaySlot, string> slotIds,
        HashSet<string> usedFurnitureIds,
        HashSet<string> usedSlotIds)
    {
        HashSet<int> exportedFurniture = new HashSet<int>();
        HashSet<int> exportedMetadata = new HashSet<int>();
        for (int i = 0; i < blocks.Count; i++)
        {
            MemorySpaceBlock block = blocks[i];
            RoomSlotPlacementMetadata[] metadataRecords = block.GetComponentsInChildren<RoomSlotPlacementMetadata>(true);
            for (int metadataIndex = 0; metadataIndex < metadataRecords.Length; metadataIndex++)
            {
                RoomSlotPlacementMetadata metadata = metadataRecords[metadataIndex];
                if (metadata == null || !exportedMetadata.Add(metadata.GetInstanceID()))
                {
                    continue;
                }

                result.gardenLayout.furniturePlacements.Add(CreateLayoutFurniturePlacement(
                    result,
                    block,
                    metadata,
                    blockInstanceIds,
                    furnitureIds,
                    slotIds,
                    usedFurnitureIds,
                    usedSlotIds));
                MemoryDisplayFurniture furniture = ResolveFurnitureForMetadata(metadata);
                if (furniture != null)
                {
                    exportedFurniture.Add(furniture.GetInstanceID());
                }
            }

            MemoryDisplayFurniture[] furnitureRecords = block.GetComponentsInChildren<MemoryDisplayFurniture>(true);
            for (int furnitureIndex = 0; furnitureIndex < furnitureRecords.Length; furnitureIndex++)
            {
                MemoryDisplayFurniture furniture = furnitureRecords[furnitureIndex];
                if (furniture == null || exportedFurniture.Contains(furniture.GetInstanceID()))
                {
                    continue;
                }

                result.gardenLayout.furniturePlacements.Add(CreateUnanchoredLayoutFurniture(
                    result,
                    block,
                    furniture,
                    blockInstanceIds,
                    furnitureIds,
                    slotIds,
                    usedFurnitureIds,
                    usedSlotIds));
                exportedFurniture.Add(furniture.GetInstanceID());
            }
        }
    }

    private static GardenLayoutFurniturePlacementRecord CreateLayoutFurniturePlacement(
        AppPreviewCatalogScanResult result,
        MemorySpaceBlock block,
        RoomSlotPlacementMetadata metadata,
        Dictionary<MemorySpaceBlock, string> blockInstanceIds,
        Dictionary<MemoryDisplayFurniture, string> furnitureIds,
        Dictionary<MemoryDisplaySlot, string> slotIds,
        HashSet<string> usedFurnitureIds,
        HashSet<string> usedSlotIds)
    {
        MemoryDisplayFurniture furniture = ResolveFurnitureForMetadata(metadata);
        string sourcePath = GetSourcePath(furniture != null ? furniture.gameObject : metadata.gameObject);
        string warningSource = FirstNonEmpty(sourcePath, GetSceneObjectPath(metadata.transform));
        string furnitureId = ResolveMappedFurnitureId(result, furniture, metadata, warningSource, furnitureIds, usedFurnitureIds);

        GardenLayoutFurniturePlacementRecord record = new GardenLayoutFurniturePlacementRecord();
        record.furnitureId = furnitureId;
        record.sourcePrefabPath = sourcePath;
        record.blockInstanceId = ResolveKnownBlockInstanceId(result, block, blockInstanceIds, warningSource);
        record.blockTypeId = FirstNonEmpty(metadata.blockTypeId, ResolveLayoutBlockTypeId(result, block, warningSource));
        record.surfaceType = metadata.surfaceType.ToString().ToLowerInvariant();
        record.position = metadata.transform.position;
        record.rotationEuler = metadata.transform.eulerAngles;
        record.scale = metadata.transform.lossyScale;

        if (metadata.surfaceType == RoomSlotSurfaceType.Floor)
        {
            record.floorAnchor = CreateFloorAnchor(metadata);
        }
        else if (metadata.surfaceType == RoomSlotSurfaceType.Wall)
        {
            record.wallAnchor = CreateWallAnchor(metadata);
        }
        else
        {
            AddLayoutWarning(result, "missing_layout_anchor", warningSource, $"Furniture placement '{metadata.name}' has no recognized floor or wall anchor.");
        }

        if (furniture != null)
        {
            MemoryDisplaySlot[] slots = furniture.GetComponentsInChildren<MemoryDisplaySlot>(true);
            for (int i = 0; i < slots.Length; i++)
            {
                string slotId = ResolveMappedSlotId(result, slots[i], warningSource, slotIds, usedSlotIds);
                if (!string.IsNullOrWhiteSpace(slotId))
                {
                    record.slotIds.Add(slotId);
                }
            }
        }
        else
        {
            AppendSlotIds(record.slotIds, metadata.slotIds);
        }

        return record;
    }

    private static GardenLayoutFurniturePlacementRecord CreateUnanchoredLayoutFurniture(
        AppPreviewCatalogScanResult result,
        MemorySpaceBlock block,
        MemoryDisplayFurniture furniture,
        Dictionary<MemorySpaceBlock, string> blockInstanceIds,
        Dictionary<MemoryDisplayFurniture, string> furnitureIds,
        Dictionary<MemoryDisplaySlot, string> slotIds,
        HashSet<string> usedFurnitureIds,
        HashSet<string> usedSlotIds)
    {
        string sourcePath = GetSourcePath(furniture != null ? furniture.gameObject : null);
        string warningSource = FirstNonEmpty(sourcePath, GetSceneObjectPath(furniture != null ? furniture.transform : null));
        AddLayoutWarning(result, "unanchored_furniture", warningSource, $"Furniture '{(furniture != null ? furniture.name : "unknown")}' has no RoomSlotPlacementMetadata anchor.");

        GardenLayoutFurniturePlacementRecord record = new GardenLayoutFurniturePlacementRecord();
        record.furnitureId = ResolveMappedFurnitureId(result, furniture, null, warningSource, furnitureIds, usedFurnitureIds);
        record.sourcePrefabPath = sourcePath;
        record.blockInstanceId = ResolveKnownBlockInstanceId(result, block, blockInstanceIds, warningSource);
        record.blockTypeId = ResolveLayoutBlockTypeId(result, block, warningSource);
        record.surfaceType = "unknown";
        record.position = furniture.transform.position;
        record.rotationEuler = furniture.transform.eulerAngles;
        record.scale = furniture.transform.lossyScale;

        MemoryDisplaySlot[] slots = furniture.GetComponentsInChildren<MemoryDisplaySlot>(true);
        for (int i = 0; i < slots.Length; i++)
        {
            string slotId = ResolveMappedSlotId(result, slots[i], warningSource, slotIds, usedSlotIds);
            if (!string.IsNullOrWhiteSpace(slotId))
            {
                record.slotIds.Add(slotId);
            }
        }

        return record;
    }

    private static void ScanLayoutSlots(
        AppPreviewCatalogScanResult result,
        List<MemorySpaceBlock> blocks,
        Dictionary<MemorySpaceBlock, string> blockInstanceIds,
        Dictionary<MemoryDisplayFurniture, string> furnitureIds,
        Dictionary<MemoryDisplaySlot, string> slotIds,
        HashSet<string> usedSlotIds)
    {
        HashSet<int> exportedSlots = new HashSet<int>();
        for (int i = 0; i < blocks.Count; i++)
        {
            MemoryDisplaySlot[] slots = blocks[i].GetComponentsInChildren<MemoryDisplaySlot>(true);
            for (int slotIndex = 0; slotIndex < slots.Length; slotIndex++)
            {
                MemoryDisplaySlot slot = slots[slotIndex];
                if (slot != null && exportedSlots.Add(slot.GetInstanceID()))
                {
                    result.gardenLayout.slotPlacements.Add(CreateLayoutSlotPlacement(
                        result,
                        blocks[i],
                        slot,
                        blockInstanceIds,
                        furnitureIds,
                        slotIds,
                        usedSlotIds));
                }
            }
        }
    }

    private static GardenLayoutSlotPlacementRecord CreateLayoutSlotPlacement(
        AppPreviewCatalogScanResult result,
        MemorySpaceBlock block,
        MemoryDisplaySlot slot,
        Dictionary<MemorySpaceBlock, string> blockInstanceIds,
        Dictionary<MemoryDisplayFurniture, string> furnitureIds,
        Dictionary<MemoryDisplaySlot, string> slotIds,
        HashSet<string> usedSlotIds)
    {
        MemoryDisplayFurniture furniture = slot.GetComponentInParent<MemoryDisplayFurniture>();
        string warningSource = GetSceneObjectPath(slot.transform);
        GardenLayoutSlotPlacementRecord record = new GardenLayoutSlotPlacementRecord();
        record.slotId = ResolveMappedSlotId(result, slot, warningSource, slotIds, usedSlotIds);
        record.furnitureId = furniture != null && furnitureIds.TryGetValue(furniture, out string mappedFurnitureId)
            ? mappedFurnitureId
            : ResolveLayoutFurnitureId(result, furniture, null, warningSource);
        record.blockInstanceId = ResolveKnownBlockInstanceId(result, block, blockInstanceIds, warningSource);
        record.slotType = slot.Type.ToString();
        record.worldPosition = slot.transform.position;
        record.localPosition = furniture != null ? furniture.transform.InverseTransformPoint(slot.transform.position) : slot.transform.localPosition;
        record.localRotationEuler = slot.transform.localEulerAngles;

        if (slot.AcceptedItemSizes != null)
        {
            for (int i = 0; i < slot.AcceptedItemSizes.Count; i++)
            {
                record.acceptedItemSizes.Add(slot.AcceptedItemSizes[i].ToString());
            }
        }

        if (slot.OccupiedItem != null)
        {
            MemoryObject occupiedObject = slot.OccupiedItem.GetComponent<MemoryObject>();
            record.occupiedItemId = occupiedObject != null
                ? ResolveLayoutItemId(result, occupiedObject, warningSource)
                : slot.OccupiedItem.name;
        }

        return record;
    }

    private static void ScanLayoutItems(
        AppPreviewCatalogScanResult result,
        List<GameObject> roots,
        Dictionary<MemorySpaceBlock, string> blockInstanceIds,
        Dictionary<MemoryDisplayFurniture, string> furnitureIds,
        Dictionary<MemoryDisplaySlot, string> slotIds)
    {
        HashSet<int> exportedItems = new HashSet<int>();
        if (roots == null)
        {
            return;
        }

        for (int i = 0; i < roots.Count; i++)
        {
            if (roots[i] == null)
            {
                continue;
            }

            MemoryObject[] items = roots[i].GetComponentsInChildren<MemoryObject>(true);
            for (int itemIndex = 0; itemIndex < items.Length; itemIndex++)
            {
                MemoryObject item = items[itemIndex];
                if (item != null && exportedItems.Add(item.GetInstanceID()))
                {
                    result.gardenLayout.itemPlacements.Add(CreateLayoutItemPlacement(result, item, blockInstanceIds, furnitureIds, slotIds));
                }
            }
        }
    }

    private static GardenLayoutItemPlacementRecord CreateLayoutItemPlacement(
        AppPreviewCatalogScanResult result,
        MemoryObject item,
        Dictionary<MemorySpaceBlock, string> blockInstanceIds,
        Dictionary<MemoryDisplayFurniture, string> furnitureIds,
        Dictionary<MemoryDisplaySlot, string> slotIds)
    {
        string warningSource = FirstNonEmpty(GetSourcePath(item.gameObject), GetSceneObjectPath(item.transform));
        MemoryDisplaySlot slot = item.CurrentSlot != null ? item.CurrentSlot : item.GetComponentInParent<MemoryDisplaySlot>();
        MemoryDisplayFurniture furniture = slot != null ? slot.GetComponentInParent<MemoryDisplayFurniture>() : item.GetComponentInParent<MemoryDisplayFurniture>();
        MemorySpaceBlock block = slot != null ? slot.GetComponentInParent<MemorySpaceBlock>() : item.GetComponentInParent<MemorySpaceBlock>();

        if (slot == null)
        {
            AddLayoutWarning(result, "unplaced_item", warningSource, $"MemoryObject '{item.name}' is not assigned to a MemoryDisplaySlot.");
        }

        GardenLayoutItemPlacementRecord record = new GardenLayoutItemPlacementRecord();
        record.itemId = ResolveLayoutItemId(result, item, warningSource);
        record.itemName = item.ItemName;
        record.sourcePrefabPath = GetSourcePath(item.gameObject);
        record.furnitureId = furniture != null && furnitureIds.TryGetValue(furniture, out string mappedFurnitureId)
            ? mappedFurnitureId
            : furniture != null ? ResolveLayoutFurnitureId(result, furniture, null, warningSource) : string.Empty;
        record.slotId = slot != null && slotIds.TryGetValue(slot, out string mappedSlotId)
            ? mappedSlotId
            : slot != null ? ResolveLayoutSlotId(result, slot, warningSource) : string.Empty;
        record.blockInstanceId = block != null ? ResolveKnownBlockInstanceId(result, block, blockInstanceIds, warningSource) : string.Empty;
        record.worldPosition = item.transform.position;
        record.rotationEuler = item.transform.eulerAngles;
        record.itemSize = item.GetPlacementItemSize().ToString();
        record.enablePlacement = item.EnablePlacement;

        if (item.AllowedSlotTypes != null)
        {
            for (int i = 0; i < item.AllowedSlotTypes.Count; i++)
            {
                record.allowedSlotTypes.Add(item.AllowedSlotTypes[i].ToString());
            }
        }

        return record;
    }

    private static FurnitureFloorAnchorRecord CreateFloorAnchor(RoomSlotPlacementMetadata metadata)
    {
        return new FurnitureFloorAnchorRecord
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

    private static FurnitureWallAnchorRecord CreateWallAnchor(RoomSlotPlacementMetadata metadata)
    {
        return new FurnitureWallAnchorRecord
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

    private static MemoryDisplayFurniture ResolveFurnitureForMetadata(RoomSlotPlacementMetadata metadata)
    {
        if (metadata == null)
        {
            return null;
        }

        MemoryDisplayFurniture furniture = metadata.GetComponent<MemoryDisplayFurniture>();
        if (furniture == null)
        {
            furniture = metadata.GetComponentInChildren<MemoryDisplayFurniture>(true);
        }

        return furniture;
    }

    private static void AppendSlotIds(List<string> target, List<string> source)
    {
        if (target == null || source == null)
        {
            return;
        }

        for (int i = 0; i < source.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(source[i]))
            {
                target.Add(source[i]);
            }
        }
    }

    private static string ResolveLayoutBlockInstanceId(AppPreviewCatalogScanResult result, MemorySpaceBlock block, string sourcePath)
    {
        string id = block != null ? block.spaceBlockId : string.Empty;
        if (!string.IsNullOrWhiteSpace(id))
        {
            return id;
        }

        id = block != null ? FirstNonEmpty(RoomSlotGridUtility.GetBlockInstanceId(block), block.name) : string.Empty;
        AddLayoutWarning(result, "missing_blockInstanceId", sourcePath, $"MemorySpaceBlock '{(block != null ? block.name : "unknown")}' is missing spaceBlockId. Falling back to name-derived identity.");
        return id;
    }

    private static string ResolveLayoutBlockTypeId(AppPreviewCatalogScanResult result, MemorySpaceBlock block, string sourcePath)
    {
        string id = block != null && block.blockDefinition != null ? block.blockDefinition.blockId : string.Empty;
        if (!string.IsNullOrWhiteSpace(id))
        {
            return id;
        }

        id = block != null ? FirstNonEmpty(RoomSlotGridUtility.GetBlockTypeId(block), block.name) : string.Empty;
        AddLayoutWarning(result, "missing_blockTypeId", sourcePath, $"MemorySpaceBlock '{(block != null ? block.name : "unknown")}' is missing SpaceBlockDefinition.blockId. Falling back to name-derived identity.");
        return id;
    }

    private static string ResolveLayoutPortId(AppPreviewCatalogScanResult result, SpaceOpeningPort port, string sourcePath)
    {
        if (port == null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(port.openingId))
        {
            return port.openingId;
        }

        AddLayoutWarning(result, "missing_portId", sourcePath, $"SpaceOpeningPort '{port.name}' is missing openingId. Falling back to object name.");
        return port.name;
    }

    private static string ResolveLayoutFurnitureId(
        AppPreviewCatalogScanResult result,
        MemoryDisplayFurniture furniture,
        RoomSlotPlacementMetadata metadata,
        string sourcePath)
    {
        string id = FirstNonEmpty(furniture != null ? furniture.FurnitureId : string.Empty, metadata != null ? metadata.furnitureId : string.Empty);
        if (!string.IsNullOrWhiteSpace(id))
        {
            return id;
        }

        id = FirstNonEmpty(metadata != null ? metadata.slotPlacementId : string.Empty, metadata != null ? metadata.name : string.Empty, furniture != null ? furniture.name : string.Empty);
        AddLayoutWarning(result, "missing_furnitureId", sourcePath, "Furniture placement is missing furnitureId. Falling back to placement or object name.");
        return id;
    }

    private static string ResolveLayoutSlotId(AppPreviewCatalogScanResult result, MemoryDisplaySlot slot, string sourcePath)
    {
        if (slot == null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(slot.SlotId))
        {
            return slot.SlotId;
        }

        AddLayoutWarning(result, "missing_slotId", sourcePath, $"MemoryDisplaySlot '{slot.name}' is missing slotId. Falling back to object name.");
        return slot.name;
    }

    private static string ResolveMappedFurnitureId(
        AppPreviewCatalogScanResult result,
        MemoryDisplayFurniture furniture,
        RoomSlotPlacementMetadata metadata,
        string sourcePath,
        Dictionary<MemoryDisplayFurniture, string> furnitureIds,
        HashSet<string> usedFurnitureIds)
    {
        if (furniture != null && furnitureIds != null && furnitureIds.TryGetValue(furniture, out string mappedId))
        {
            return mappedId;
        }

        string id = ResolveLayoutFurnitureId(result, furniture, metadata, sourcePath);
        id = ResolveUniqueLayoutId(
            result,
            id,
            usedFurnitureIds,
            "duplicate_furnitureId",
            sourcePath,
            GetSceneObjectPath(furniture != null ? furniture.transform : metadata != null ? metadata.transform : null),
            "furnitureId");

        if (furniture != null && furnitureIds != null)
        {
            furnitureIds[furniture] = id;
        }

        return id;
    }

    private static string ResolveMappedSlotId(
        AppPreviewCatalogScanResult result,
        MemoryDisplaySlot slot,
        string sourcePath,
        Dictionary<MemoryDisplaySlot, string> slotIds,
        HashSet<string> usedSlotIds)
    {
        if (slot != null && slotIds != null && slotIds.TryGetValue(slot, out string mappedId))
        {
            return mappedId;
        }

        string id = ResolveLayoutSlotId(result, slot, sourcePath);
        id = ResolveUniqueLayoutId(
            result,
            id,
            usedSlotIds,
            "duplicate_slotId",
            sourcePath,
            GetSceneObjectPath(slot != null ? slot.transform : null),
            "slotId");

        if (slot != null && slotIds != null)
        {
            slotIds[slot] = id;
        }

        return id;
    }

    private static string ResolveUniqueLayoutId(
        AppPreviewCatalogScanResult result,
        string preferredId,
        HashSet<string> usedIds,
        string warningCode,
        string sourcePath,
        string fallbackToken,
        string label)
    {
        string baseId = FirstNonEmpty(preferredId, fallbackToken, label);
        if (usedIds == null || usedIds.Add(baseId))
        {
            return baseId;
        }

        string suffix = SanitizeKey(FirstNonEmpty(fallbackToken, Guid.NewGuid().ToString("N")));
        string uniqueId = baseId + "-" + suffix;
        int index = 2;
        while (!usedIds.Add(uniqueId))
        {
            uniqueId = baseId + "-" + suffix + "-" + index.ToString(CultureInfo.InvariantCulture);
            index++;
        }

        AddLayoutWarning(
            result,
            warningCode,
            sourcePath,
            $"Duplicate {label} '{baseId}' found in 02 scene. Exporting '{uniqueId}' to keep layout preview IDs unique.");
        return uniqueId;
    }

    private static string ResolveLayoutItemId(AppPreviewCatalogScanResult result, MemoryObject item, string sourcePath)
    {
        if (item == null)
        {
            return string.Empty;
        }

        string id = FirstNonEmpty(item.ItemId, item.MemoryItemData != null ? item.MemoryItemData.ItemId : string.Empty);
        if (!string.IsNullOrWhiteSpace(id))
        {
            return id;
        }

        AddLayoutWarning(result, "missing_itemId", sourcePath, $"MemoryObject '{item.name}' is missing ItemId. Falling back to object name.");
        return item.name;
    }

    private static string ResolveKnownBlockInstanceId(
        AppPreviewCatalogScanResult result,
        MemorySpaceBlock block,
        Dictionary<MemorySpaceBlock, string> blockInstanceIds,
        string sourcePath)
    {
        if (block == null)
        {
            AddLayoutWarning(result, "missing_blockInstanceId", sourcePath, "Layout child could not resolve a parent MemorySpaceBlock.");
            return string.Empty;
        }

        if (blockInstanceIds != null && blockInstanceIds.TryGetValue(block, out string id))
        {
            return id;
        }

        return ResolveLayoutBlockInstanceId(result, block, sourcePath);
    }

    private static void AddLayoutWarning(AppPreviewCatalogScanResult result, string code, string sourcePath, string message)
    {
        AddWarning(result, null, code, sourcePath, message);
        if (result == null || result.gardenLayout == null || string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        result.gardenLayout.warnings.Add(new AppPreviewCatalogWarning
        {
            code = code,
            sourcePath = sourcePath ?? string.Empty,
            message = message
        });
    }

    private static string GetSourcePath(GameObject gameObject)
    {
        if (gameObject == null)
        {
            return string.Empty;
        }

        return PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(gameObject);
    }

    private static string GetScenePath(GameObject gameObject)
    {
        if (gameObject == null)
        {
            return string.Empty;
        }

        Scene scene = gameObject.scene;
        return scene.IsValid() ? FirstNonEmpty(scene.path, scene.name) : string.Empty;
    }

    private static string GetSceneObjectPath(Transform transform)
    {
        if (transform == null)
        {
            return string.Empty;
        }

        List<string> parts = new List<string>();
        Transform current = transform;
        while (current != null)
        {
            parts.Add(current.name);
            current = current.parent;
        }

        parts.Reverse();
        return string.Join("/", parts);
    }

}
