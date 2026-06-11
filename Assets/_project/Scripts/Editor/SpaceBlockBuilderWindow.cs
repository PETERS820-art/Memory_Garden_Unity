#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class SpaceBlockBuilderWindow : EditorWindow
{
    private const string MenuItemPath = "Tools/Memory Garden/Space Block Builder";
    private const string WindowTitle = "Space Block Builder";
    private const float WallSlotHeight = 2.5f;
    private const string SpaceBlockDefinitionFolder = "Assets/_project/ScriptableObjects/SpaceBlocks";
    private const string SpaceBlockPrefabFolder = "Assets/_project/Prefabs/Environment/SpaceBlocks";

    private enum BuilderMode
    {
        QuickRectangularBlock,
        GridPaintMode
    }

    private struct SegmentPaletteEntry
    {
        public string Label;
        public SpaceSegmentDefinition Definition;
    }

    private struct GridBlockRoots
    {
        public Transform FloorSegments;
        public Transform WallSegments;
        public Transform CeilingSegments;
        public Transform OpeningOverlays;
        public Transform ConnectorPorts;
        public Transform FurniturePlacementPoints;
        public Transform Debug;
    }

    [SerializeField] private BuilderMode builderMode = BuilderMode.QuickRectangularBlock;

    [SerializeField] private SpaceSegmentKit selectedSegmentKit;
    [SerializeField] private int widthUnits = 5;
    [SerializeField] private int depthUnits = 5;
    [SerializeField] private string wallStyleFilter = "white";

    [SerializeField] private int gridWidth = 10;
    [SerializeField] private int gridDepth = 10;
    [SerializeField] private float gridSize = 1f;
    [SerializeField] private string gridBlockId = "Block_01";
    [SerializeField] private SegmentCategory placementCategoryFilter = SegmentCategory.Floor;
    [SerializeField] private SpaceSegmentDefinition selectedGridSegmentDefinition;
    [SerializeField] private int selectedPaletteIndex;
    [SerializeField] private int placeGridX;
    [SerializeField] private int placeGridZ;
    [SerializeField] private WallSide placeWallSide = WallSide.North;
    [SerializeField] private int placeRotationY;
    [SerializeField] private bool replaceExistingPlacement;
    [SerializeField] private bool markConnectorCandidate;
    [SerializeField] private SpaceBlockDefinition selectedBlockDefinitionAsset;

    private readonly List<SegmentPaletteEntry> segmentPaletteEntries = new List<SegmentPaletteEntry>();

    [MenuItem(MenuItemPath)]
    public static void OpenWindow()
    {
        SpaceBlockBuilderWindow window = GetWindow<SpaceBlockBuilderWindow>();
        window.titleContent = new GUIContent(WindowTitle);
        window.minSize = new Vector2(520f, 420f);
        window.Show();
    }

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
    }

    private void OnGUI()
    {
        builderMode = (BuilderMode)EditorGUILayout.EnumPopup("Mode", builderMode);
        EditorGUILayout.Space();

        switch (builderMode)
        {
            case BuilderMode.GridPaintMode:
                DrawGridPaintMode();
                break;
            default:
                DrawQuickRectangularMode();
                break;
        }
    }

    private void DrawQuickRectangularMode()
    {
        EditorGUILayout.HelpBox(
            "Keeps the existing fast rectangular block workflow. Select a WallSegmentSlot in the hierarchy to swap variants in the Inspector.",
            MessageType.Info);

        selectedSegmentKit = (SpaceSegmentKit)EditorGUILayout.ObjectField(
            "Segment Kit",
            selectedSegmentKit,
            typeof(SpaceSegmentKit),
            false);

        widthUnits = Mathf.Max(1, EditorGUILayout.IntField("Width Units", widthUnits));
        depthUnits = Mathf.Max(1, EditorGUILayout.IntField("Depth Units", depthUnits));
        wallStyleFilter = EditorGUILayout.TextField("Wall Style Filter", wallStyleFilter);

        EditorGUILayout.Space();

        if (GUILayout.Button("Build New SpaceBlock"))
        {
            BuildNewSpaceBlock();
        }

        if (GUILayout.Button("Rebuild Selected SpaceBlock"))
        {
            RebuildSelectedSpaceBlock();
        }

        if (GUILayout.Button("Validate Selected SpaceBlock"))
        {
            ValidateSelectedSpaceBlock();
        }
    }

    private void DrawGridPaintMode()
    {
        EditorGUILayout.HelpBox(
            "Grid Paint Mode places SegmentKit assets on a 1m authoring grid. Use the manual gridX/gridZ/side fields for this MVP, then save to a SpaceBlockDefinition and bake a reusable prefab.",
            MessageType.Info);

        selectedSegmentKit = (SpaceSegmentKit)EditorGUILayout.ObjectField(
            "Segment Kit",
            selectedSegmentKit,
            typeof(SpaceSegmentKit),
            false);

        gridBlockId = EditorGUILayout.TextField("Block Id", gridBlockId);
        gridWidth = Mathf.Max(1, EditorGUILayout.IntField("Grid Width", gridWidth));
        gridDepth = Mathf.Max(1, EditorGUILayout.IntField("Grid Depth", gridDepth));
        gridSize = Mathf.Max(0.1f, EditorGUILayout.FloatField("Grid Size", gridSize));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Segment Palette", EditorStyles.boldLabel);
        placementCategoryFilter = DrawPlacementCategoryFilter(placementCategoryFilter);
        RebuildSegmentPalette();

        selectedGridSegmentDefinition = (SpaceSegmentDefinition)EditorGUILayout.ObjectField(
            "Selected Segment",
            selectedGridSegmentDefinition,
            typeof(SpaceSegmentDefinition),
            false);

        if (selectedGridSegmentDefinition != null)
        {
            placementCategoryFilter = GetSupportedPlacementCategory(selectedGridSegmentDefinition.category);
        }

        if (segmentPaletteEntries.Count > 0)
        {
            selectedPaletteIndex = Mathf.Clamp(selectedPaletteIndex, 0, segmentPaletteEntries.Count - 1);
            string[] labels = new string[segmentPaletteEntries.Count];
            for (int i = 0; i < segmentPaletteEntries.Count; i++)
            {
                labels[i] = segmentPaletteEntries[i].Label;
            }

            int nextIndex = EditorGUILayout.Popup("Palette", selectedPaletteIndex, labels);
            if (nextIndex != selectedPaletteIndex)
            {
                selectedPaletteIndex = nextIndex;
                selectedGridSegmentDefinition = segmentPaletteEntries[selectedPaletteIndex].Definition;
            }

            if (selectedGridSegmentDefinition == null && selectedPaletteIndex >= 0 && selectedPaletteIndex < segmentPaletteEntries.Count)
            {
                selectedGridSegmentDefinition = segmentPaletteEntries[selectedPaletteIndex].Definition;
            }
        }
        else
        {
            EditorGUILayout.HelpBox("No segments match the current category filter.", MessageType.Warning);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Placement", EditorStyles.boldLabel);
        placeGridX = EditorGUILayout.IntField("Grid X", placeGridX);
        placeGridZ = EditorGUILayout.IntField("Grid Z", placeGridZ);
        if (placementCategoryFilter == SegmentCategory.Wall || placementCategoryFilter == SegmentCategory.OpeningOverlay)
        {
            placeWallSide = (WallSide)EditorGUILayout.EnumPopup("Wall Side", placeWallSide);
        }

        placeRotationY = NormalizeRotation(EditorGUILayout.IntField("Rotation Y", placeRotationY));
        replaceExistingPlacement = EditorGUILayout.Toggle("Replace Overlap", replaceExistingPlacement);
        markConnectorCandidate = EditorGUILayout.Toggle("Connector Candidate", markConnectorCandidate);

        EditorGUILayout.Space();

        if (GUILayout.Button("Create New Editable Block"))
        {
            CreateNewEditableBlock();
        }

        if (GUILayout.Button("Place Selected Segment"))
        {
            PlaceSelectedSegment();
        }

        if (GUILayout.Button("Delete Selected Segment"))
        {
            DeleteSelectedSegment();
        }

        EditorGUILayout.Space();
        selectedBlockDefinitionAsset = (SpaceBlockDefinition)EditorGUILayout.ObjectField(
            "Block Definition",
            selectedBlockDefinitionAsset,
            typeof(SpaceBlockDefinition),
            false);

        if (GUILayout.Button("Save Block Definition"))
        {
            SaveBlockDefinition();
        }

        if (GUILayout.Button("Load Block Definition"))
        {
            LoadBlockDefinition();
        }

        if (GUILayout.Button("Bake Block Prefab"))
        {
            BakeBlockPrefab();
        }

        if (GUILayout.Button("Validate Block"))
        {
            ValidateGridBlock();
        }
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        if (builderMode != BuilderMode.GridPaintMode)
        {
            return;
        }

        MemorySpaceBlock block = GetSelectedBlock();
        if (block == null)
        {
            return;
        }

        Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;
        DrawAuthoringGrid(block.transform, gridWidth, gridDepth, gridSize);
    }

    private void DrawAuthoringGrid(Transform root, int width, int depth, float size)
    {
        if (root == null || width <= 0 || depth <= 0 || size <= 0f)
        {
            return;
        }

        float halfWidth = width * size * 0.5f;
        float halfDepth = depth * size * 0.5f;
        Handles.color = new Color(0.4f, 0.9f, 1f, 0.65f);

        for (int x = 0; x <= width; x++)
        {
            float localX = -halfWidth + (x * size);
            Vector3 from = root.TransformPoint(new Vector3(localX, 0f, -halfDepth));
            Vector3 to = root.TransformPoint(new Vector3(localX, 0f, halfDepth));
            Handles.DrawLine(from, to);
        }

        for (int z = 0; z <= depth; z++)
        {
            float localZ = -halfDepth + (z * size);
            Vector3 from = root.TransformPoint(new Vector3(-halfWidth, 0f, localZ));
            Vector3 to = root.TransformPoint(new Vector3(halfWidth, 0f, localZ));
            Handles.DrawLine(from, to);
        }
    }

    private SegmentCategory DrawPlacementCategoryFilter(SegmentCategory currentValue)
    {
        string[] labels =
        {
            SegmentCategory.Floor.ToString(),
            SegmentCategory.Wall.ToString(),
            SegmentCategory.Ceiling.ToString(),
            SegmentCategory.Beam.ToString(),
            SegmentCategory.OpeningOverlay.ToString(),
            SegmentCategory.Threshold.ToString()
        };

        SegmentCategory[] values =
        {
            SegmentCategory.Floor,
            SegmentCategory.Wall,
            SegmentCategory.Ceiling,
            SegmentCategory.Beam,
            SegmentCategory.OpeningOverlay,
            SegmentCategory.Threshold
        };

        int currentIndex = 0;
        for (int i = 0; i < values.Length; i++)
        {
            if (values[i] == currentValue)
            {
                currentIndex = i;
                break;
            }
        }

        int selectedIndex = EditorGUILayout.Popup("Category Filter", currentIndex, labels);
        return values[selectedIndex];
    }

    private void RebuildSegmentPalette()
    {
        segmentPaletteEntries.Clear();
        if (selectedSegmentKit == null || selectedSegmentKit.segments == null)
        {
            return;
        }

        for (int i = 0; i < selectedSegmentKit.segments.Count; i++)
        {
            SpaceSegmentDefinition definition = selectedSegmentKit.segments[i];
            if (definition == null)
            {
                continue;
            }

            if (GetSupportedPlacementCategory(definition.category) != placementCategoryFilter)
            {
                continue;
            }

            segmentPaletteEntries.Add(new SegmentPaletteEntry
            {
                Label = BuildSegmentPaletteLabel(definition),
                Definition = definition
            });
        }

        segmentPaletteEntries.Sort((a, b) => string.Compare(a.Label, b.Label, StringComparison.OrdinalIgnoreCase));

        if (selectedGridSegmentDefinition != null)
        {
            for (int i = 0; i < segmentPaletteEntries.Count; i++)
            {
                if (segmentPaletteEntries[i].Definition == selectedGridSegmentDefinition)
                {
                    selectedPaletteIndex = i;
                    return;
                }
            }
        }

        if (segmentPaletteEntries.Count > 0)
        {
            selectedPaletteIndex = Mathf.Clamp(selectedPaletteIndex, 0, segmentPaletteEntries.Count - 1);
        }
    }

    private static string BuildSegmentPaletteLabel(SpaceSegmentDefinition definition)
    {
        string style = string.IsNullOrWhiteSpace(definition.styleId) ? "default" : definition.styleId;
        string size = $"{definition.sizeXZ.x:0.##}x{definition.sizeXZ.y:0.##}";
        string variant = definition.variant.ToString().ToLowerInvariant();
        return $"{definition.category.ToString().ToLowerInvariant()}/{style}/{variant} [{size}]";
    }

    private static SegmentCategory GetSupportedPlacementCategory(SegmentCategory rawCategory)
    {
        switch (rawCategory)
        {
            case SegmentCategory.Floor:
            case SegmentCategory.Wall:
            case SegmentCategory.Ceiling:
            case SegmentCategory.Beam:
            case SegmentCategory.OpeningOverlay:
            case SegmentCategory.Threshold:
                return rawCategory;
            default:
                return SegmentCategory.Floor;
        }
    }

    private void CreateNewEditableBlock()
    {
        try
        {
            if (!ValidateGridInputs(requireSegmentKit: true))
            {
                return;
            }

            string blockName = string.IsNullOrWhiteSpace(gridBlockId) ? GetNextBlockName() : gridBlockId.Trim();
            GameObject root = new GameObject(blockName);
            Undo.RegisterCreatedObjectUndo(root, "Create Editable SpaceBlock");

            MemorySpaceBlock block = root.AddComponent<MemorySpaceBlock>();
            block.spaceBlockId = blockName;
            block.spaceBlockType = SpaceBlockType.Custom;
            block.widthUnits = gridWidth;
            block.depthUnits = gridDepth;
            block.segmentKit = selectedSegmentKit;
            block.blockDefinition = null;

            EnsureGridBlockRoots(block.transform);
            Selection.activeGameObject = root;
            EditorGUIUtility.PingObject(root);
            ShowSummary($"Created editable SpaceBlock: {blockName}");
        }
        catch (Exception exception)
        {
            ReportException("Create New Editable Block", exception);
        }
    }

    private void PlaceSelectedSegment()
    {
        try
        {
            MemorySpaceBlock block = GetSelectedBlock();
            if (block == null)
            {
                ShowSummary("Select an editable MemorySpaceBlock first.");
                return;
            }

            SpaceSegmentKit kit = selectedSegmentKit != null ? selectedSegmentKit : block.segmentKit;
            if (kit == null)
            {
                ShowSummary("Grid Paint Mode requires a SegmentKit.");
                return;
            }

            block.segmentKit = kit;
            gridWidth = Mathf.Max(1, gridWidth);
            gridDepth = Mathf.Max(1, gridDepth);
            gridSize = Mathf.Max(0.1f, gridSize);
            block.widthUnits = gridWidth;
            block.depthUnits = gridDepth;
            block.spaceBlockId = string.IsNullOrWhiteSpace(gridBlockId) ? block.name : gridBlockId.Trim();

            if (placementCategoryFilter == SegmentCategory.OpeningOverlay)
            {
                PlaceOverlayOnSelectedWall(block, kit);
                return;
            }

            if (selectedGridSegmentDefinition == null)
            {
                ShowSummary("Select a segment definition from the palette first.");
                return;
            }

            SpaceSegmentPlacementRecord record = BuildPlacementRecord(selectedGridSegmentDefinition);
            List<string> validationMessages = ValidatePlacementRecord(record, selectedGridSegmentDefinition, block, kit);
            if (validationMessages.Count > 0)
            {
                ShowMessages("Place Selected Segment", validationMessages);
                return;
            }

            List<SpaceSegmentPlacementMetadata> overlaps = FindOverlappingPlacements(block, record);
            if (overlaps.Count > 0 && !replaceExistingPlacement)
            {
                ShowSummary($"Placement overlaps {overlaps.Count} existing segment(s). Enable Replace Overlap to replace them.");
                return;
            }

            Undo.RegisterFullObjectHierarchyUndo(block.gameObject, "Place Space Segment");
            for (int i = 0; i < overlaps.Count; i++)
            {
                Undo.DestroyObjectImmediate(overlaps[i].gameObject);
            }

            CreatePlacementInstance(block, record, selectedGridSegmentDefinition, kit, editable: true);
            block.NormalizeWallSlotRoots();
            EditorUtility.SetDirty(block);
            Selection.activeGameObject = block.gameObject;
            ShowSummary($"Placed {selectedGridSegmentDefinition.segmentId}.");
        }
        catch (Exception exception)
        {
            ReportException("Place Selected Segment", exception);
        }
    }

    private void DeleteSelectedSegment()
    {
        try
        {
            MemorySpaceBlock block = GetSelectedBlock();
            if (block == null)
            {
                ShowSummary("Select an editable MemorySpaceBlock first.");
                return;
            }

            SpaceSegmentPlacementMetadata metadata = Selection.activeGameObject != null
                ? Selection.activeGameObject.GetComponentInParent<SpaceSegmentPlacementMetadata>()
                : null;

            if (metadata == null)
            {
                ShowSummary("Select a placed segment to delete.");
                return;
            }

            if (placementCategoryFilter == SegmentCategory.OpeningOverlay && !string.IsNullOrWhiteSpace(metadata.record.overlaySegmentId))
            {
                Undo.RegisterFullObjectHierarchyUndo(metadata.gameObject, "Clear Wall Overlay");
                WallSegmentSlot wallSlot = metadata.GetComponent<WallSegmentSlot>();
                if (wallSlot != null)
                {
                    wallSlot.ClearOverlay();
                }

                metadata.record.overlaySegmentId = string.Empty;
                metadata.overlayDefinition = null;
                EditorUtility.SetDirty(metadata);
                ShowSummary("Removed overlay from selected wall.");
                return;
            }

            Undo.DestroyObjectImmediate(metadata.gameObject);
            EditorUtility.SetDirty(block);
            ShowSummary("Deleted selected placed segment.");
        }
        catch (Exception exception)
        {
            ReportException("Delete Selected Segment", exception);
        }
    }

    private void SaveBlockDefinition()
    {
        try
        {
            MemorySpaceBlock block = GetSelectedBlock();
            if (block == null)
            {
                ShowSummary("Select a MemorySpaceBlock to save.");
                return;
            }

            EnsureFolderPath(SpaceBlockDefinitionFolder);

            SpaceBlockDefinition definition = selectedBlockDefinitionAsset;
            if (definition == null)
            {
                string assetName = string.IsNullOrWhiteSpace(block.spaceBlockId) ? block.name : block.spaceBlockId;
                string assetPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(SpaceBlockDefinitionFolder, $"SBD_{assetName}.asset"));
                definition = CreateInstance<SpaceBlockDefinition>();
                AssetDatabase.CreateAsset(definition, assetPath.Replace("\\", "/"));
            }

            PopulateDefinitionFromBlock(definition, block);
            EditorUtility.SetDirty(definition);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            selectedBlockDefinitionAsset = definition;
            block.blockDefinition = definition;
            EditorUtility.SetDirty(block);
            ShowSummary($"Saved block definition: {definition.name}");
        }
        catch (Exception exception)
        {
            ReportException("Save Block Definition", exception);
        }
    }

    private void LoadBlockDefinition()
    {
        try
        {
            if (selectedBlockDefinitionAsset == null)
            {
                ShowSummary("Assign a SpaceBlockDefinition asset first.");
                return;
            }

            MemorySpaceBlock block = GetSelectedBlock();
            if (block == null)
            {
                CreateNewEditableBlock();
                block = GetSelectedBlock();
            }

            if (block == null)
            {
                ShowSummary("Unable to create or select an editable block.");
                return;
            }

            SpaceSegmentKit kit = selectedSegmentKit != null ? selectedSegmentKit : block.segmentKit;
            if (kit == null)
            {
                ShowSummary("Loading requires a SegmentKit so segment ids can resolve to prefabs.");
                return;
            }

            Undo.RegisterFullObjectHierarchyUndo(block.gameObject, "Load Space Block Definition");

            gridBlockId = string.IsNullOrWhiteSpace(selectedBlockDefinitionAsset.blockId)
                ? selectedBlockDefinitionAsset.name
                : selectedBlockDefinitionAsset.blockId;
            gridWidth = Mathf.Max(1, selectedBlockDefinitionAsset.gridWidth);
            gridDepth = Mathf.Max(1, selectedBlockDefinitionAsset.gridDepth);
            gridSize = Mathf.Max(0.1f, selectedBlockDefinitionAsset.gridSize);

            block.spaceBlockId = gridBlockId;
            block.widthUnits = gridWidth;
            block.depthUnits = gridDepth;
            block.segmentKit = kit;
            block.blockDefinition = selectedBlockDefinitionAsset;

            ClearGridPlacementChildren(block.transform);

            List<string> loadWarnings = new List<string>();
            for (int i = 0; i < selectedBlockDefinitionAsset.placements.Count; i++)
            {
                SpaceSegmentPlacementRecord record = CloneRecord(selectedBlockDefinitionAsset.placements[i]);
                SpaceSegmentDefinition definition = kit.GetSegment(record.segmentId);
                if (definition == null)
                {
                    loadWarnings.Add($"Missing segment definition while loading: {record.segmentId}");
                    continue;
                }

                CreatePlacementInstance(block, record, definition, kit, editable: true);
            }

            block.NormalizeWallSlotRoots();
            Selection.activeGameObject = block.gameObject;
            if (loadWarnings.Count > 0)
            {
                ShowMessages("Load Block Definition", loadWarnings);
            }
            else
            {
                ShowSummary($"Loaded block definition: {selectedBlockDefinitionAsset.name}");
            }
        }
        catch (Exception exception)
        {
            ReportException("Load Block Definition", exception);
        }
    }

    private void BakeBlockPrefab()
    {
        try
        {
            MemorySpaceBlock sourceBlock = GetSelectedBlock();
            if (sourceBlock == null)
            {
                ShowSummary("Select a MemorySpaceBlock to bake.");
                return;
            }

            SpaceSegmentKit kit = selectedSegmentKit != null ? selectedSegmentKit : sourceBlock.segmentKit;
            if (kit == null)
            {
                ShowSummary("Baking requires a SegmentKit.");
                return;
            }

            EnsureFolderPath(SpaceBlockPrefabFolder);
            EnsureFolderPath(SpaceBlockDefinitionFolder);

            if (selectedBlockDefinitionAsset == null)
            {
                SaveBlockDefinition();
            }

            string blockId = string.IsNullOrWhiteSpace(sourceBlock.spaceBlockId) ? sourceBlock.name : sourceBlock.spaceBlockId;
            GameObject bakeRoot = new GameObject($"PF_SB_{blockId}");
            try
            {
                MemorySpaceBlock bakedBlock = bakeRoot.AddComponent<MemorySpaceBlock>();
                bakedBlock.spaceBlockId = blockId;
                bakedBlock.spaceBlockType = SpaceBlockType.Custom;
                bakedBlock.widthUnits = gridWidth;
                bakedBlock.depthUnits = gridDepth;
                bakedBlock.segmentKit = kit;
                bakedBlock.blockDefinition = selectedBlockDefinitionAsset;

                GridBlockRoots roots = EnsureGridBlockRoots(bakeRoot.transform);
                List<SpaceSegmentPlacementRecord> records = CollectPlacementRecords(sourceBlock);

                for (int i = 0; i < records.Count; i++)
                {
                    SpaceSegmentPlacementRecord record = records[i];
                    SpaceSegmentDefinition definition = kit.GetSegment(record.segmentId);
                    if (definition == null)
                    {
                        Debug.LogWarning($"[SpaceBlockBuilder] Skipping missing segment while baking: {record.segmentId}", sourceBlock);
                        continue;
                    }

                    CreatePlacementInstance(bakedBlock, record, definition, kit, editable: false);
                }

                bakedBlock.NormalizeWallSlotRoots();
                string prefabPath = Path.Combine(SpaceBlockPrefabFolder, $"PF_SB_{blockId}.prefab").Replace("\\", "/");
                PrefabUtility.SaveAsPrefabAsset(bakeRoot, prefabPath);
                ShowSummary($"Baked block prefab: {prefabPath}");
            }
            finally
            {
                DestroyImmediate(bakeRoot);
            }
        }
        catch (Exception exception)
        {
            ReportException("Bake Block Prefab", exception);
        }
    }

    private void ValidateGridBlock()
    {
        try
        {
            MemorySpaceBlock block = GetSelectedBlock();
            if (block == null)
            {
                ShowSummary("Select a MemorySpaceBlock to validate.");
                return;
            }

            List<string> messages = GetGridValidationMessages(block);
            if (messages.Count == 0)
            {
                ShowSummary($"Grid block validation passed: {block.name}");
                return;
            }

            ShowMessages("Validate Block", messages);
        }
        catch (Exception exception)
        {
            ReportException("Validate Block", exception);
        }
    }

    private void PlaceOverlayOnSelectedWall(MemorySpaceBlock block, SpaceSegmentKit kit)
    {
        if (selectedGridSegmentDefinition == null)
        {
            ShowSummary("Select an overlay definition from the palette first.");
            return;
        }

        if (selectedGridSegmentDefinition.category != SegmentCategory.OpeningOverlay)
        {
            ShowSummary("The selected definition is not an OpeningOverlay segment.");
            return;
        }

        SpaceSegmentPlacementMetadata metadata = Selection.activeGameObject != null
            ? Selection.activeGameObject.GetComponentInParent<SpaceSegmentPlacementMetadata>()
            : null;

        if (metadata == null || metadata.record.category != SegmentCategory.Wall)
        {
            ShowSummary("Select a placed wall segment to attach the overlay.");
            return;
        }

        WallSegmentSlot wallSlot = metadata.GetComponent<WallSegmentSlot>();
        if (wallSlot == null)
        {
            ShowSummary("Selected wall placement is missing its WallSegmentSlot component.");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(metadata.gameObject, "Place Opening Overlay");
        wallSlot.SetOverlay(selectedGridSegmentDefinition);
        metadata.record.overlaySegmentId = selectedGridSegmentDefinition.segmentId;
        metadata.overlayDefinition = selectedGridSegmentDefinition;
        metadata.record.isConnectorCandidate = markConnectorCandidate;
        EditorUtility.SetDirty(metadata);
        EditorUtility.SetDirty(block);
        ShowSummary($"Attached overlay {selectedGridSegmentDefinition.segmentId}.");
    }

    private SpaceSegmentPlacementRecord BuildPlacementRecord(SpaceSegmentDefinition definition)
    {
        return new SpaceSegmentPlacementRecord
        {
            placementId = $"PL_{Guid.NewGuid():N}".Substring(0, 11),
            segmentId = definition.segmentId,
            category = GetSupportedPlacementCategory(definition.category),
            gridX = placeGridX,
            gridZ = placeGridZ,
            side = placeWallSide,
            rotationY = NormalizeRotation(placeRotationY),
            footprint = GetDefinitionFootprint(definition),
            overlaySegmentId = string.Empty,
            isConnectorCandidate = markConnectorCandidate
        };
    }

    private static int NormalizeRotation(int rawRotation)
    {
        int normalized = rawRotation % 360;
        if (normalized < 0)
        {
            normalized += 360;
        }

        return normalized;
    }

    private static Vector2Int GetDefinitionFootprint(SpaceSegmentDefinition definition)
    {
        if (definition == null)
        {
            return Vector2Int.one;
        }

        switch (definition.category)
        {
            case SegmentCategory.Floor:
            case SegmentCategory.Ceiling:
            case SegmentCategory.Threshold:
                return new Vector2Int(
                    Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(1f, definition.sizeXZ.x))),
                    Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(1f, definition.sizeXZ.y))));
            default:
                return new Vector2Int(Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(1f, definition.sizeXZ.x))), 1);
        }
    }

    private List<string> ValidatePlacementRecord(
        SpaceSegmentPlacementRecord record,
        SpaceSegmentDefinition definition,
        MemorySpaceBlock block,
        SpaceSegmentKit kit)
    {
        List<string> messages = new List<string>();
        if (kit == null)
        {
            messages.Add("Missing SegmentKit.");
            return messages;
        }

        if (definition == null)
        {
            messages.Add("Missing segment definition.");
            return messages;
        }

        if (record.gridX < 0 || record.gridZ < 0)
        {
            messages.Add("Grid coordinates must be non-negative.");
        }

        switch (record.category)
        {
            case SegmentCategory.Floor:
            case SegmentCategory.Ceiling:
            case SegmentCategory.Threshold:
                if (record.gridX + record.footprint.x > gridWidth || record.gridZ + record.footprint.y > gridDepth)
                {
                    messages.Add("Placement exceeds the grid bounds.");
                }
                break;
            case SegmentCategory.Wall:
                if (!IsWallPlacementInsideGrid(record))
                {
                    messages.Add("Wall placement is outside the valid grid edge range.");
                }
                break;
        }

        return messages;
    }

    private bool IsWallPlacementInsideGrid(SpaceSegmentPlacementRecord record)
    {
        int wallLength = Mathf.Max(1, record.footprint.x);
        switch (record.side)
        {
            case WallSide.North:
            case WallSide.South:
                return record.gridX >= 0
                    && record.gridX + wallLength <= gridWidth
                    && record.gridZ >= 0
                    && record.gridZ < gridDepth;
            case WallSide.East:
            case WallSide.West:
                return record.gridX >= 0
                    && record.gridX < gridWidth
                    && record.gridZ >= 0
                    && record.gridZ + wallLength <= gridDepth;
            default:
                return false;
        }
    }

    private List<SpaceSegmentPlacementMetadata> FindOverlappingPlacements(MemorySpaceBlock block, SpaceSegmentPlacementRecord candidate)
    {
        List<SpaceSegmentPlacementMetadata> overlaps = new List<SpaceSegmentPlacementMetadata>();
        SpaceSegmentPlacementMetadata[] placements = block.GetComponentsInChildren<SpaceSegmentPlacementMetadata>(true);
        for (int i = 0; i < placements.Length; i++)
        {
            SpaceSegmentPlacementMetadata placement = placements[i];
            if (placement == null || placement.record == null)
            {
                continue;
            }

            if (!DoPlacementsOverlap(placement.record, candidate))
            {
                continue;
            }

            overlaps.Add(placement);
        }

        return overlaps;
    }

    private static bool DoPlacementsOverlap(SpaceSegmentPlacementRecord a, SpaceSegmentPlacementRecord b)
    {
        if (a == null || b == null || a.category != b.category)
        {
            return false;
        }

        switch (a.category)
        {
            case SegmentCategory.Floor:
            case SegmentCategory.Ceiling:
            case SegmentCategory.Threshold:
                return DoFloorAreasOverlap(a, b);
            case SegmentCategory.Wall:
                return DoWallEdgesOverlap(a, b);
            default:
                return false;
        }
    }

    private static bool DoFloorAreasOverlap(SpaceSegmentPlacementRecord a, SpaceSegmentPlacementRecord b)
    {
        int aMinX = a.gridX;
        int aMaxX = a.gridX + Mathf.Max(1, a.footprint.x);
        int aMinZ = a.gridZ;
        int aMaxZ = a.gridZ + Mathf.Max(1, a.footprint.y);

        int bMinX = b.gridX;
        int bMaxX = b.gridX + Mathf.Max(1, b.footprint.x);
        int bMinZ = b.gridZ;
        int bMaxZ = b.gridZ + Mathf.Max(1, b.footprint.y);

        return aMinX < bMaxX && aMaxX > bMinX && aMinZ < bMaxZ && aMaxZ > bMinZ;
    }

    private static bool DoWallEdgesOverlap(SpaceSegmentPlacementRecord a, SpaceSegmentPlacementRecord b)
    {
        if (a.side != b.side)
        {
            return false;
        }

        if (a.side == WallSide.North || a.side == WallSide.South)
        {
            if (a.gridZ != b.gridZ)
            {
                return false;
            }

            int aMin = a.gridX;
            int aMax = a.gridX + Mathf.Max(1, a.footprint.x);
            int bMin = b.gridX;
            int bMax = b.gridX + Mathf.Max(1, b.footprint.x);
            return aMin < bMax && aMax > bMin;
        }

        if (a.gridX != b.gridX)
        {
            return false;
        }

        int aMinZ = a.gridZ;
        int aMaxZ = a.gridZ + Mathf.Max(1, a.footprint.x);
        int bMinZ = b.gridZ;
        int bMaxZ = b.gridZ + Mathf.Max(1, b.footprint.x);
        return aMinZ < bMaxZ && aMaxZ > bMinZ;
    }

    private void CreatePlacementInstance(
        MemorySpaceBlock block,
        SpaceSegmentPlacementRecord record,
        SpaceSegmentDefinition definition,
        SpaceSegmentKit kit,
        bool editable)
    {
        GridBlockRoots roots = EnsureGridBlockRoots(block.transform);
        Transform parent = GetCategoryParent(roots, record.category);

        GameObject placementRoot = new GameObject(string.IsNullOrWhiteSpace(record.placementId) ? $"PL_{definition.segmentId}" : record.placementId);
        placementRoot.transform.SetParent(parent, false);
        placementRoot.transform.localPosition = GetPlacementLocalPosition(record, definition);
        placementRoot.transform.localRotation = Quaternion.identity;
        placementRoot.transform.localScale = Vector3.one;

        SpaceSegmentPlacementMetadata metadata = null;
        if (editable)
        {
            metadata = placementRoot.AddComponent<SpaceSegmentPlacementMetadata>();
            metadata.record = CloneRecord(record);
            metadata.definition = definition;
        }

        switch (record.category)
        {
            case SegmentCategory.Wall:
                CreateWallPlacement(placementRoot, record, definition, kit, metadata);
                break;
            default:
                CreateGenericPlacement(placementRoot.transform, record, definition);
                break;
        }
    }

    private void CreateWallPlacement(
        GameObject placementRoot,
        SpaceSegmentPlacementRecord record,
        SpaceSegmentDefinition definition,
        SpaceSegmentKit kit,
        SpaceSegmentPlacementMetadata metadata)
    {
        WallSegmentSlot slot = placementRoot.AddComponent<WallSegmentSlot>();
        slot.side = record.side;
        slot.segmentIndex = 0;
        slot.allowConnection = record.isConnectorCandidate;
        slot.segmentRoot = placementRoot.transform;
        slot.cornerPlacement = CornerPlacement.None;

        GameObject overlayRoot = new GameObject("OverlayRoot");
        overlayRoot.transform.SetParent(placementRoot.transform, false);
        slot.overlayRoot = overlayRoot.transform;
        slot.SetSegment(definition);

        if (!string.IsNullOrWhiteSpace(record.overlaySegmentId))
        {
            SpaceSegmentDefinition overlayDefinition = kit != null ? kit.GetSegment(record.overlaySegmentId) : null;
            if (overlayDefinition != null)
            {
                slot.SetOverlay(overlayDefinition);
                if (metadata != null)
                {
                    metadata.overlayDefinition = overlayDefinition;
                }
            }
        }
    }

    private void CreateGenericPlacement(Transform placementRoot, SpaceSegmentPlacementRecord record, SpaceSegmentDefinition definition)
    {
        if (definition == null || definition.prefab == null)
        {
            return;
        }

        GameObject instance = Instantiate(definition.prefab, placementRoot, false);
        instance.name = definition.prefab.name;
        ApplyGenericPlacementTransform(instance.transform, record, definition);
    }

    private void ApplyGenericPlacementTransform(Transform instanceTransform, SpaceSegmentPlacementRecord record, SpaceSegmentDefinition definition)
    {
        switch (record.category)
        {
            case SegmentCategory.Floor:
                instanceTransform.localRotation = Quaternion.Euler(-90f, record.rotationY, 0f);
                AlignInstanceToLocalBounds(instanceTransform, 0f, 0f, 0f);
                break;
            case SegmentCategory.Ceiling:
                instanceTransform.localRotation = Quaternion.Euler(-90f, record.rotationY, 0f);
                AlignInstanceToBoundsAtHeight(instanceTransform, 0f, GetAuthoringCeilingHeight(), 0f);
                break;
            case SegmentCategory.Threshold:
                instanceTransform.localRotation = Quaternion.Euler(0f, record.rotationY, 0f);
                AlignInstanceToLocalBounds(instanceTransform, 0f, 0f, 0f);
                break;
            case SegmentCategory.Beam:
                instanceTransform.localRotation = Quaternion.Euler(0f, record.rotationY, 0f);
                AlignInstanceToBoundsAtHeight(instanceTransform, 0f, GetAuthoringCeilingHeight(), 0f);
                break;
            default:
                instanceTransform.localPosition = Vector3.zero;
                instanceTransform.localRotation = Quaternion.identity;
                break;
        }
    }

    private Vector3 GetPlacementLocalPosition(SpaceSegmentPlacementRecord record, SpaceSegmentDefinition definition)
    {
        float halfWidth = gridWidth * gridSize * 0.5f;
        float halfDepth = gridDepth * gridSize * 0.5f;

        switch (record.category)
        {
            case SegmentCategory.Wall:
                return GetWallPlacementLocalPosition(record, halfWidth, halfDepth);
            default:
                return new Vector3(
                    -halfWidth + ((record.gridX + (record.footprint.x * 0.5f)) * gridSize),
                    0f,
                    -halfDepth + ((record.gridZ + (record.footprint.y * 0.5f)) * gridSize));
        }
    }

    private Vector3 GetWallPlacementLocalPosition(SpaceSegmentPlacementRecord record, float halfWidth, float halfDepth)
    {
        float cellMinX = -halfWidth + (record.gridX * gridSize);
        float cellMinZ = -halfDepth + (record.gridZ * gridSize);
        float segmentLength = Mathf.Max(1, record.footprint.x) * gridSize;

        switch (record.side)
        {
            case WallSide.North:
                return new Vector3(cellMinX + (segmentLength * 0.5f), 0f, cellMinZ + gridSize);
            case WallSide.South:
                return new Vector3(cellMinX + (segmentLength * 0.5f), 0f, cellMinZ);
            case WallSide.East:
                return new Vector3(cellMinX + gridSize, 0f, cellMinZ + (segmentLength * 0.5f));
            case WallSide.West:
                return new Vector3(cellMinX, 0f, cellMinZ + (segmentLength * 0.5f));
            default:
                return Vector3.zero;
        }
    }

    private GridBlockRoots EnsureGridBlockRoots(Transform blockRoot)
    {
        GridBlockRoots roots = new GridBlockRoots
        {
            FloorSegments = GetOrCreateChild(blockRoot, "FloorSegments"),
            WallSegments = GetOrCreateChild(blockRoot, "WallSegments"),
            CeilingSegments = GetOrCreateChild(blockRoot, "CeilingSegments"),
            OpeningOverlays = GetOrCreateChild(blockRoot, "OpeningOverlays"),
            ConnectorPorts = GetOrCreateChild(blockRoot, "ConnectorPorts"),
            FurniturePlacementPoints = GetOrCreateChild(blockRoot, "FurniturePlacementPoints"),
            Debug = GetOrCreateChild(blockRoot, "Debug")
        };

        return roots;
    }

    private static Transform GetCategoryParent(GridBlockRoots roots, SegmentCategory category)
    {
        switch (category)
        {
            case SegmentCategory.Wall:
                return roots.WallSegments;
            case SegmentCategory.Ceiling:
                return roots.CeilingSegments;
            case SegmentCategory.Beam:
                return roots.CeilingSegments;
            case SegmentCategory.Threshold:
                return roots.FloorSegments;
            default:
                return roots.FloorSegments;
        }
    }

    private void PopulateDefinitionFromBlock(SpaceBlockDefinition definition, MemorySpaceBlock block)
    {
        definition.blockId = string.IsNullOrWhiteSpace(block.spaceBlockId) ? block.name : block.spaceBlockId;
        definition.gridWidth = gridWidth;
        definition.gridDepth = gridDepth;
        definition.gridSize = gridSize;
        definition.placements.Clear();

        List<SpaceSegmentPlacementRecord> records = CollectPlacementRecords(block);
        for (int i = 0; i < records.Count; i++)
        {
            definition.placements.Add(CloneRecord(records[i]));
        }
    }

    private List<SpaceSegmentPlacementRecord> CollectPlacementRecords(MemorySpaceBlock block)
    {
        List<SpaceSegmentPlacementRecord> records = new List<SpaceSegmentPlacementRecord>();
        SpaceSegmentPlacementMetadata[] placements = block.GetComponentsInChildren<SpaceSegmentPlacementMetadata>(true);
        Array.Sort(placements, ComparePlacementMetadata);
        for (int i = 0; i < placements.Length; i++)
        {
            if (placements[i] == null || placements[i].record == null)
            {
                continue;
            }

            records.Add(CloneRecord(placements[i].record));
        }

        return records;
    }

    private static int ComparePlacementMetadata(SpaceSegmentPlacementMetadata a, SpaceSegmentPlacementMetadata b)
    {
        string aId = a != null && a.record != null ? a.record.placementId : string.Empty;
        string bId = b != null && b.record != null ? b.record.placementId : string.Empty;
        return string.Compare(aId, bId, StringComparison.OrdinalIgnoreCase);
    }

    private static SpaceSegmentPlacementRecord CloneRecord(SpaceSegmentPlacementRecord source)
    {
        if (source == null)
        {
            return new SpaceSegmentPlacementRecord();
        }

        return new SpaceSegmentPlacementRecord
        {
            placementId = source.placementId,
            segmentId = source.segmentId,
            category = source.category,
            gridX = source.gridX,
            gridZ = source.gridZ,
            side = source.side,
            rotationY = source.rotationY,
            footprint = source.footprint,
            overlaySegmentId = source.overlaySegmentId,
            isConnectorCandidate = source.isConnectorCandidate
        };
    }

    private void ClearGridPlacementChildren(Transform blockRoot)
    {
        GridBlockRoots roots = EnsureGridBlockRoots(blockRoot);
        ClearChildren(roots.FloorSegments);
        ClearChildren(roots.WallSegments);
        ClearChildren(roots.CeilingSegments);
        ClearChildren(roots.OpeningOverlays);
        ClearChildren(roots.ConnectorPorts);
        ClearChildren(roots.FurniturePlacementPoints);
        ClearChildren(roots.Debug);
    }

    private List<string> GetGridValidationMessages(MemorySpaceBlock block)
    {
        List<string> messages = new List<string>();
        SpaceSegmentKit kit = selectedSegmentKit != null ? selectedSegmentKit : block.segmentKit;
        if (kit == null)
        {
            messages.Add("Missing SegmentKit.");
        }

        if (!AssetDatabase.IsValidFolder(SpaceBlockPrefabFolder))
        {
            messages.Add($"Prefab output folder missing: {SpaceBlockPrefabFolder}");
        }

        HashSet<string> placementIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        HashSet<string> occupiedFloorCells = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        HashSet<string> occupiedWallEdges = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        SpaceSegmentPlacementMetadata[] placements = block.GetComponentsInChildren<SpaceSegmentPlacementMetadata>(true);
        for (int i = 0; i < placements.Length; i++)
        {
            SpaceSegmentPlacementMetadata placement = placements[i];
            if (placement == null || placement.record == null)
            {
                continue;
            }

            SpaceSegmentPlacementRecord record = placement.record;
            if (string.IsNullOrWhiteSpace(record.placementId) || !placementIds.Add(record.placementId))
            {
                messages.Add($"Duplicate placementId: {record.placementId}");
            }

            if (kit != null)
            {
                SpaceSegmentDefinition definition = kit.GetSegment(record.segmentId);
                if (definition == null)
                {
                    messages.Add($"Missing segment definition: {record.segmentId}");
                }
                else if (definition.prefab == null)
                {
                    messages.Add($"Missing prefab on definition: {record.segmentId}");
                }
            }

            if (record.gridX < 0 || record.gridZ < 0)
            {
                messages.Add($"Invalid grid coordinate on {record.placementId}: ({record.gridX}, {record.gridZ})");
            }

            switch (record.category)
            {
                case SegmentCategory.Floor:
                    if (record.gridX + Mathf.Max(1, record.footprint.x) > gridWidth
                        || record.gridZ + Mathf.Max(1, record.footprint.y) > gridDepth)
                    {
                        messages.Add($"Floor placement out of bounds: {record.placementId}");
                    }

                    AddFloorOccupancy(record, occupiedFloorCells, messages);
                    break;
                case SegmentCategory.Wall:
                    if (!IsWallPlacementInsideGrid(record))
                    {
                        messages.Add($"Wall placement out of bounds: {record.placementId}");
                    }

                    AddWallOccupancy(record, occupiedWallEdges, messages);
                    if (!string.IsNullOrWhiteSpace(record.overlaySegmentId) && placement.GetComponent<WallSegmentSlot>() == null)
                    {
                        messages.Add($"Opening overlay without target wall slot: {record.placementId}");
                    }
                    break;
            }
        }

        return messages;
    }

    private void AddFloorOccupancy(SpaceSegmentPlacementRecord record, HashSet<string> occupiedCells, List<string> messages)
    {
        for (int x = 0; x < Mathf.Max(1, record.footprint.x); x++)
        {
            for (int z = 0; z < Mathf.Max(1, record.footprint.y); z++)
            {
                string key = $"{record.gridX + x}:{record.gridZ + z}";
                if (!occupiedCells.Add(key))
                {
                    messages.Add($"Overlapping floor cell at ({record.gridX + x}, {record.gridZ + z}).");
                }
            }
        }
    }

    private void AddWallOccupancy(SpaceSegmentPlacementRecord record, HashSet<string> occupiedEdges, List<string> messages)
    {
        int segmentLength = Mathf.Max(1, record.footprint.x);
        for (int step = 0; step < segmentLength; step++)
        {
            string key;
            if (record.side == WallSide.North || record.side == WallSide.South)
            {
                key = $"{record.side}:{record.gridX + step}:{record.gridZ}";
            }
            else
            {
                key = $"{record.side}:{record.gridX}:{record.gridZ + step}";
            }

            if (!occupiedEdges.Add(key))
            {
                messages.Add($"Overlapping wall edge at {key}.");
            }
        }
    }

    private float GetAuthoringCeilingHeight()
    {
        if (selectedSegmentKit == null || selectedSegmentKit.segments == null)
        {
            return WallSlotHeight;
        }

        float bestHeight = 0f;
        for (int i = 0; i < selectedSegmentKit.segments.Count; i++)
        {
            SpaceSegmentDefinition definition = selectedSegmentKit.segments[i];
            if (definition != null && definition.category == SegmentCategory.Wall)
            {
                bestHeight = Mathf.Max(bestHeight, definition.height);
            }
        }

        return bestHeight > 0f ? bestHeight : WallSlotHeight;
    }

    private bool ValidateGridInputs(bool requireSegmentKit)
    {
        if (requireSegmentKit && selectedSegmentKit == null)
        {
            ShowSummary("Select a SegmentKit asset first.");
            return false;
        }

        if (gridWidth <= 0 || gridDepth <= 0)
        {
            ShowSummary("Grid Width and Grid Depth must be greater than 0.");
            return false;
        }

        if (gridSize <= 0f)
        {
            ShowSummary("Grid Size must be greater than 0.");
            return false;
        }

        return true;
    }

    private static void EnsureFolderPath(string targetFolder)
    {
        string normalizedTarget = targetFolder.Replace("\\", "/");
        if (AssetDatabase.IsValidFolder(normalizedTarget))
        {
            return;
        }

        string[] parts = normalizedTarget.Split('/');
        string currentPath = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string nextPath = $"{currentPath}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(nextPath))
            {
                AssetDatabase.CreateFolder(currentPath, parts[i]);
            }

            currentPath = nextPath;
        }
    }

    private static void ShowMessages(string title, List<string> messages)
    {
        if (messages == null || messages.Count == 0)
        {
            return;
        }

        string body = string.Join("\n", messages.ToArray());
        Debug.LogWarning($"[SpaceBlockBuilder] {title}\n{body}");
        if (!Application.isBatchMode)
        {
            EditorUtility.DisplayDialog(title, body, "OK");
        }
    }

    private void BuildNewSpaceBlock()
    {
        try
        {
            if (!ValidateBuilderInputs())
            {
                return;
            }

            GameObject root = new GameObject(GetNextBlockName());
            Undo.RegisterCreatedObjectUndo(root, "Build SpaceBlock");

            MemorySpaceBlock block = root.AddComponent<MemorySpaceBlock>();
            block.spaceBlockId = root.name;
            block.spaceBlockType = SpaceBlockType.Room;
            block.widthUnits = widthUnits;
            block.depthUnits = depthUnits;
            block.segmentKit = selectedSegmentKit;

            BuildOrRebuildBlock(block, widthUnits, depthUnits, selectedSegmentKit, wallStyleFilter);

            Selection.activeGameObject = root;
            EditorGUIUtility.PingObject(root);
            ShowSummary($"Built new SpaceBlock: {root.name}");
        }
        catch (Exception exception)
        {
            ReportException("Build New SpaceBlock", exception);
        }
    }

    private void RebuildSelectedSpaceBlock()
    {
        try
        {
            MemorySpaceBlock block = GetSelectedBlock();
            if (block == null)
            {
                ShowSummary("Select a MemorySpaceBlock to rebuild.");
                return;
            }

            SpaceSegmentKit kit = selectedSegmentKit != null ? selectedSegmentKit : block.segmentKit;
            if (kit == null)
            {
                ShowSummary("Selected SpaceBlock has no SegmentKit and none is selected in the builder.");
                return;
            }

            Undo.RegisterFullObjectHierarchyUndo(block.gameObject, "Rebuild SpaceBlock");
            BuildOrRebuildBlock(block, widthUnits, depthUnits, kit, wallStyleFilter);
            Selection.activeGameObject = block.gameObject;
            ShowSummary($"Rebuilt SpaceBlock: {block.name}");
        }
        catch (Exception exception)
        {
            ReportException("Rebuild Selected SpaceBlock", exception);
        }
    }

    private void ValidateSelectedSpaceBlock()
    {
        MemorySpaceBlock block = GetSelectedBlock();
        if (block == null)
        {
            ShowSummary("Select a MemorySpaceBlock to validate.");
            return;
        }

        block.AutoCollectWallSegments();
        block.ValidateBlock();
        List<string> messages = block.GetValidationMessages();
        ShowSummary(messages.Count == 0
            ? $"SpaceBlock validation passed: {block.name}"
            : $"SpaceBlock validation warnings: {messages.Count}");
    }

    private void BuildOrRebuildBlock(
        MemorySpaceBlock block,
        int targetWidthUnits,
        int targetDepthUnits,
        SpaceSegmentKit kit,
        string styleFilter)
    {
        if (block == null)
        {
            throw new InvalidOperationException("MemorySpaceBlock target is missing.");
        }

        if (kit == null)
        {
            throw new InvalidOperationException("SegmentKit is required.");
        }

        block.widthUnits = Mathf.Max(1, targetWidthUnits);
        block.depthUnits = Mathf.Max(1, targetDepthUnits);
        block.segmentKit = kit;
        block.spaceBlockId = string.IsNullOrWhiteSpace(block.spaceBlockId) ? block.name : block.spaceBlockId;
        block.spaceBlockType = SpaceBlockType.Room;

        Transform floorGridRoot = GetOrCreateChild(block.transform, "FloorGrid");
        Transform wallsRoot = GetOrCreateChild(block.transform, "Walls");
        Transform openingsRoot = GetOrCreateChild(block.transform, "Openings");
        Transform connectorPortsRoot = GetOrCreateChild(block.transform, "ConnectorPorts");
        Transform furniturePlacementPointsRoot = GetOrCreateChild(block.transform, "FurniturePlacementPoints");
        Transform debugRoot = GetOrCreateChild(block.transform, "Debug");

        Transform northRoot = GetOrCreateChild(wallsRoot, "Wall_North");
        Transform southRoot = GetOrCreateChild(wallsRoot, "Wall_South");
        Transform eastRoot = GetOrCreateChild(wallsRoot, "Wall_East");
        Transform westRoot = GetOrCreateChild(wallsRoot, "Wall_West");

        ClearChildren(floorGridRoot);
        ClearChildren(northRoot);
        ClearChildren(southRoot);
        ClearChildren(eastRoot);
        ClearChildren(westRoot);
        ClearChildren(openingsRoot);
        ClearChildren(connectorPortsRoot);
        ClearChildren(furniturePlacementPointsRoot);
        ClearChildren(debugRoot);

        SpaceSegmentDefinition floorDefinition = FindFloorSegmentDefinition(kit, styleFilter);
        if (floorDefinition == null)
        {
            throw new InvalidOperationException("No floor segment definition was found for the selected SegmentKit.");
        }

        SpaceSegmentDefinition defaultWallDefinition = FindWallSegmentDefinition(
            kit,
            styleFilter,
            SegmentVariant.Solid,
            GetDefaultWallHeight(kit, styleFilter));
        if (defaultWallDefinition == null)
        {
            throw new InvalidOperationException("No default wall segment definition was found for the selected SegmentKit.");
        }

        SpaceSegmentDefinition cornerWallDefinition = FindWallSegmentDefinition(
            kit,
            styleFilter,
            SegmentVariant.Corner,
            defaultWallDefinition.height > 0f ? defaultWallDefinition.height : GetDefaultWallHeight(kit, styleFilter));

        BuildFloorGrid(floorGridRoot, floorDefinition, block.widthUnits, block.depthUnits);
        BuildWallSlots(northRoot, WallSide.North, block.widthUnits, block.widthUnits, block.depthUnits, defaultWallDefinition, cornerWallDefinition);
        BuildWallSlots(southRoot, WallSide.South, block.widthUnits, block.widthUnits, block.depthUnits, defaultWallDefinition, cornerWallDefinition);
        BuildWallSlots(eastRoot, WallSide.East, Mathf.Max(0, block.depthUnits - 2), block.widthUnits, block.depthUnits, defaultWallDefinition, null);
        BuildWallSlots(westRoot, WallSide.West, Mathf.Max(0, block.depthUnits - 2), block.widthUnits, block.depthUnits, defaultWallDefinition, null);

        block.NormalizeWallSlotRoots();
        EditorUtility.SetDirty(block);
    }

    private void BuildFloorGrid(Transform floorGridRoot, SpaceSegmentDefinition floorDefinition, int width, int depth)
    {
        for (int z = 0; z < depth; z++)
        {
            for (int x = 0; x < width; x++)
            {
                if (floorDefinition.prefab == null)
                {
                    continue;
                }

                GameObject instance = Instantiate(floorDefinition.prefab, floorGridRoot, false);
                instance.name = $"Floor_{x:00}_{z:00}";
                instance.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                AlignInstanceToLocalBounds(
                    instance.transform,
                    -width * 0.5f + 0.5f + x,
                    0f,
                    -depth * 0.5f + 0.5f + z);
            }
        }
    }

    private void BuildWallSlots(
        Transform wallRoot,
        WallSide side,
        int slotCount,
        int widthUnitsValue,
        int depthUnitsValue,
        SpaceSegmentDefinition defaultWallDefinition,
        SpaceSegmentDefinition cornerWallDefinition)
    {
        for (int index = 0; index < slotCount; index++)
        {
            GameObject slotObject = new GameObject($"Slot_{side}_{index:00}");
            slotObject.transform.SetParent(wallRoot, false);
            slotObject.transform.localPosition = GetWallSlotPosition(side, index, widthUnitsValue, depthUnitsValue);
            slotObject.transform.localRotation = Quaternion.identity;

            WallSegmentSlot slot = slotObject.AddComponent<WallSegmentSlot>();
            slot.side = side;
            slot.segmentIndex = index;
            slot.allowConnection = false;
            slot.segmentRoot = slotObject.transform;
            slot.cornerPlacement = GetCornerPlacement(side, index, slotCount);

            GameObject overlayRootObject = new GameObject("OverlayRoot");
            overlayRootObject.transform.SetParent(slotObject.transform, false);
            slot.overlayRoot = overlayRootObject.transform;

            SpaceSegmentDefinition slotDefinition = ShouldUseCorner(side, index, slotCount) && cornerWallDefinition != null
                ? cornerWallDefinition
                : defaultWallDefinition;
            slot.SetSegment(slotDefinition);
        }
    }

    private static Vector3 GetWallSlotPosition(WallSide side, int index, int widthUnitsValue, int depthUnitsValue)
    {
        switch (side)
        {
            case WallSide.North:
                if (index == 0)
                {
                    return new Vector3(-widthUnitsValue * 0.5f, 0f, depthUnitsValue * 0.5f);
                }

                if (index == widthUnitsValue - 1)
                {
                    return new Vector3(widthUnitsValue * 0.5f, 0f, depthUnitsValue * 0.5f);
                }

                return new Vector3(-widthUnitsValue * 0.5f + 0.5f + index, 0f, depthUnitsValue * 0.5f);
            case WallSide.South:
                if (index == 0)
                {
                    return new Vector3(widthUnitsValue * 0.5f, 0f, -depthUnitsValue * 0.5f);
                }

                if (index == widthUnitsValue - 1)
                {
                    return new Vector3(-widthUnitsValue * 0.5f, 0f, -depthUnitsValue * 0.5f);
                }

                return new Vector3(widthUnitsValue * 0.5f - 0.5f - index, 0f, -depthUnitsValue * 0.5f);
            case WallSide.East:
                return new Vector3(widthUnitsValue * 0.5f, 0f, -depthUnitsValue * 0.5f + 1.5f + index);
            case WallSide.West:
                return new Vector3(-widthUnitsValue * 0.5f, 0f, depthUnitsValue * 0.5f - 1.5f - index);
            default:
                return Vector3.zero;
        }
    }

    private static bool ShouldUseCorner(WallSide side, int index, int slotCount)
    {
        if ((side != WallSide.North && side != WallSide.South) || slotCount <= 0)
        {
            return false;
        }

        return index == 0 || index == slotCount - 1;
    }

    private static CornerPlacement GetCornerPlacement(WallSide side, int index, int slotCount)
    {
        if (side == WallSide.North)
        {
            if (index == 0)
            {
                return CornerPlacement.NorthWest;
            }

            if (index == slotCount - 1)
            {
                return CornerPlacement.NorthEast;
            }
        }
        else if (side == WallSide.South)
        {
            if (index == 0)
            {
                return CornerPlacement.SouthEast;
            }

            if (index == slotCount - 1)
            {
                return CornerPlacement.SouthWest;
            }
        }

        return CornerPlacement.None;
    }

    private SpaceSegmentDefinition FindFloorSegmentDefinition(SpaceSegmentKit kit, string styleFilter)
    {
        return FindBestSegment(kit, SegmentCategory.Floor, styleFilter, SegmentVariant.Default, 1f, 1f, 0f, false);
    }

    private SpaceSegmentDefinition FindWallSegmentDefinition(
        SpaceSegmentKit kit,
        string styleHint,
        SegmentVariant variant,
        float preferredHeight)
    {
        return FindBestSegment(kit, SegmentCategory.Wall, styleHint, variant, 1f, 1f, preferredHeight > 0f ? preferredHeight : WallSlotHeight, true);
    }

    private float GetDefaultWallHeight(SpaceSegmentKit kit, string styleFilter)
    {
        float bestHeight = 0f;
        if (kit == null || kit.segments == null)
        {
            return WallSlotHeight;
        }

        for (int i = 0; i < kit.segments.Count; i++)
        {
            SpaceSegmentDefinition definition = kit.segments[i];
            if (definition == null || definition.category != SegmentCategory.Wall || definition.variant != SegmentVariant.Solid)
            {
                continue;
            }

            if (!MatchesStyle(definition, styleFilter))
            {
                continue;
            }

            bestHeight = Mathf.Max(bestHeight, definition.height);
        }

        return bestHeight > 0f ? bestHeight : WallSlotHeight;
    }

    private SpaceSegmentDefinition FindBestSegment(
        SpaceSegmentKit kit,
        SegmentCategory category,
        string styleFilter,
        SegmentVariant variant,
        float targetSizeX,
        float targetSizeY,
        float preferredHeight,
        bool exactVariantRequired)
    {
        if (kit == null || kit.segments == null)
        {
            return null;
        }

        SpaceSegmentDefinition best = null;
        float bestScore = float.MaxValue;

        for (int i = 0; i < kit.segments.Count; i++)
        {
            SpaceSegmentDefinition definition = kit.segments[i];
            if (definition == null || definition.category != category)
            {
                continue;
            }

            if (!MatchesStyle(definition, styleFilter))
            {
                continue;
            }

            if (exactVariantRequired && definition.variant != variant)
            {
                continue;
            }

            if (!Approximately(definition.sizeXZ.x, targetSizeX) || !Approximately(definition.sizeXZ.y, targetSizeY))
            {
                continue;
            }

            float score = Mathf.Abs(definition.height - preferredHeight);
            score += definition.variant == variant ? 0f : 10f;
            score += string.IsNullOrWhiteSpace(styleFilter) ? 0f : GetStylePenalty(definition.styleId, styleFilter);

            if (score < bestScore)
            {
                best = definition;
                bestScore = score;
            }
        }

        return best;
    }

    private static bool MatchesStyle(SpaceSegmentDefinition definition, string styleFilter)
    {
        if (definition == null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(styleFilter))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(definition.styleId)
            && definition.styleId.IndexOf(styleFilter, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static float GetStylePenalty(string styleId, string styleFilter)
    {
        if (string.IsNullOrWhiteSpace(styleFilter))
        {
            return 0f;
        }

        if (string.Equals(styleId, styleFilter, StringComparison.OrdinalIgnoreCase))
        {
            return 0f;
        }

        return styleId != null && styleId.IndexOf(styleFilter, StringComparison.OrdinalIgnoreCase) >= 0 ? 1f : 100f;
    }

    private static bool Approximately(float a, float b)
    {
        return Mathf.Abs(a - b) <= 0.001f;
    }

    private static void AlignInstanceToBoundsAtHeight(Transform instanceTransform, float targetCenterX, float targetMinY, float targetCenterZ)
    {
        if (instanceTransform == null || instanceTransform.parent == null)
        {
            return;
        }

        instanceTransform.localPosition = Vector3.zero;
        if (!TryGetLocalBounds(instanceTransform, out Vector3 localCenter, out Vector3 localSize))
        {
            return;
        }

        float localMinY = localCenter.y - (localSize.y * 0.5f);
        instanceTransform.localPosition = new Vector3(
            targetCenterX - localCenter.x,
            targetMinY - localMinY,
            targetCenterZ - localCenter.z);
    }

    private static void AlignInstanceToLocalBounds(Transform instanceTransform, float targetCenterX, float targetMinY, float targetCenterZ)
    {
        if (instanceTransform == null || instanceTransform.parent == null)
        {
            return;
        }

        instanceTransform.localPosition = Vector3.zero;
        if (!TryGetLocalBounds(instanceTransform, out Vector3 localCenter, out Vector3 localSize))
        {
            return;
        }

        float localMinY = localCenter.y - (localSize.y * 0.5f);
        instanceTransform.localPosition = new Vector3(
            targetCenterX - localCenter.x,
            targetMinY - localMinY,
            targetCenterZ - localCenter.z);
    }

    private static bool TryGetLocalBounds(Transform instanceTransform, out Vector3 localCenter, out Vector3 localSize)
    {
        localCenter = Vector3.zero;
        localSize = Vector3.zero;

        Renderer[] renderers = instanceTransform.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
        {
            return false;
        }

        Bounds combinedBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            combinedBounds.Encapsulate(renderers[i].bounds);
        }

        Transform parent = instanceTransform.parent;
        localCenter = parent.InverseTransformPoint(combinedBounds.center);
        Vector3 localSizeVector = parent.InverseTransformVector(combinedBounds.size);
        localSize = new Vector3(Mathf.Abs(localSizeVector.x), Mathf.Abs(localSizeVector.y), Mathf.Abs(localSizeVector.z));
        return true;
    }

    private static Transform GetOrCreateChild(Transform parent, string childName)
    {
        Transform child = parent.Find(childName);
        if (child != null)
        {
            child.localPosition = Vector3.zero;
            child.localRotation = Quaternion.identity;
            child.localScale = Vector3.one;
            return child;
        }

        GameObject childObject = new GameObject(childName);
        childObject.transform.SetParent(parent, false);
        return childObject.transform;
    }

    private static void ClearChildren(Transform root)
    {
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Undo.DestroyObjectImmediate(root.GetChild(i).gameObject);
        }
    }

    private static string GetNextBlockName()
    {
        int index = 1;
        while (GameObject.Find($"MemorySpaceBlock_{index:00}") != null)
        {
            index++;
        }

        return $"MemorySpaceBlock_{index:00}";
    }

    private MemorySpaceBlock GetSelectedBlock()
    {
        if (Selection.activeGameObject == null)
        {
            return null;
        }

        return Selection.activeGameObject.GetComponentInParent<MemorySpaceBlock>();
    }

    private bool ValidateBuilderInputs()
    {
        if (selectedSegmentKit == null)
        {
            ShowSummary("Select a SegmentKit asset first.");
            return false;
        }

        if (widthUnits <= 0 || depthUnits <= 0)
        {
            ShowSummary("Width Units and Depth Units must be greater than 0.");
            return false;
        }

        return true;
    }

    private static void ShowSummary(string message)
    {
        Debug.Log($"[SpaceBlockBuilder] {message}");
        if (!Application.isBatchMode)
        {
            EditorUtility.DisplayDialog(WindowTitle, message, "OK");
        }
    }

    private static void ReportException(string actionLabel, Exception exception)
    {
        Debug.LogError($"[SpaceBlockBuilder] {actionLabel} failed: {exception}");
        if (!Application.isBatchMode)
        {
            EditorUtility.DisplayDialog(WindowTitle, exception.Message, "OK");
        }
    }
}
#endif
