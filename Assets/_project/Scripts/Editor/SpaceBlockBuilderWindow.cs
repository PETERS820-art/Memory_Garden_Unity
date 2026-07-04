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
    private const float PaletteButtonWidth = 96f;
    private const float PaletteButtonHeight = 104f;
    private const string AllPaletteSubFiltersLabel = "All";

    private enum BuilderMode
    {
        QuickRectangularBlock,
        GridPaintMode
    }

    private enum ConnectionPortAssignment
    {
        PortA,
        PortB
    }

    private struct SegmentPaletteEntry
    {
        public string Label;
        public string ButtonLabel;
        public string SecondaryLabel;
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

    private struct ScenePlacementPreview
    {
        public bool HasPreview;
        public bool CanPlace;
        public bool IsDeletePreview;
        public bool IsOverlayPreview;
        public Vector3 LocalCenter;
        public Vector3 LocalSize;
        public SpaceSegmentPlacementRecord Record;
        public SpaceSegmentDefinition Definition;
        public string Message;
        public List<SpaceSegmentPlacementMetadata> Overlaps;
        public SpaceSegmentPlacementMetadata TargetPlacement;
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
    [SerializeField] private string bakePrefabName = string.Empty;
    [SerializeField] private bool scenePlacementEnabled = true;
    [SerializeField] private bool sceneAutoPickWallSide = true;
    [SerializeField] private bool sceneDeleteMode;
    [SerializeField] private SpaceOpeningPort connectionPortA;
    [SerializeField] private SpaceOpeningPort connectionPortB;
    [SerializeField] private bool connectionAutoAlignBlockB = true;
    [SerializeField] private bool connectionAllowOverlapAnyway;
    [SerializeField] private bool blockSettingsFoldout = true;
    [SerializeField] private bool segmentPaletteFoldout = true;
    [SerializeField] private bool selectedSegmentAssetFoldout = true;
    [SerializeField] private bool blockDefinitionFoldout = true;
    [SerializeField] private bool blockConnectionFoldout;
    [SerializeField] private bool scenePaintSettingsFoldout;
    [SerializeField] private Vector2 segmentPaletteScrollPosition;
    [SerializeField] private string segmentPaletteSearch = string.Empty;
    [SerializeField] private string segmentPaletteSubFilter = string.Empty;
    [SerializeField] private Vector2 connectionPortBrowserScrollPosition;
    [SerializeField] private string connectionPortSearch = string.Empty;
    [SerializeField] private bool connectionShowOccupiedPorts = true;

    private readonly List<SegmentPaletteEntry> segmentPaletteEntries = new List<SegmentPaletteEntry>();
    private readonly Dictionary<int, Texture> segmentPaletteThumbnailCache = new Dictionary<int, Texture>();
    private readonly List<string> segmentPaletteSubFilterOptions = new List<string>();
    private bool deferredRepaintQueued;
    private bool deferredSceneRepaintQueued;
    private bool segmentPaletteDirty = true;
    private SpaceSegmentKit cachedPaletteKit;
    private SegmentCategory cachedPaletteCategory;
    private string cachedPaletteSearch = string.Empty;
    private string cachedPaletteSubFilter = string.Empty;
    private GUIStyle segmentPaletteButtonStyle;

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
        segmentPaletteDirty = true;
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

        EditorGUILayout.Space();
        DrawBlockConnectionTool();
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
            "Grid Paint Mode places SegmentKit assets directly in Scene view on the block authoring grid. Pick a segment from the palette, paint in Scene view, then save to a SpaceBlockDefinition and bake a reusable prefab.",
            MessageType.Info);

        DrawGridBlockSettingsSection();
        EditorGUILayout.Space();
        DrawSegmentPaletteSection();
        EditorGUILayout.Space();
        DrawSelectedSegmentAssetSection();
        EditorGUILayout.Space();
        DrawBlockDefinitionSection();
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

        sceneView.wantsMouseMove = scenePlacementEnabled || sceneDeleteMode;
        Event current = Event.current;
        RequestLiveScenePreviewRefresh(current);
        bool isRepaintEvent = current != null && current.type == EventType.Repaint;
        if (isRepaintEvent)
        {
            Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;
            DrawAuthoringGrid(block.transform, gridWidth, gridDepth, gridSize);
        }

        HandleSceneHotkeys(current);

        ScenePlacementPreview preview;
        if (TryBuildDeletePreview(block, current, out preview))
        {
            if (isRepaintEvent)
            {
                DrawScenePlacementPreview(block.transform, preview);
            }

            if (TryHandleDeletePreviewClick(current, preview))
            {
                QueueDeferredRepaint();
            }

            return;
        }

        if (!scenePlacementEnabled)
        {
            return;
        }

        if (TryBuildScenePlacementPreview(block, current, out preview))
        {
            if (isRepaintEvent)
            {
                DrawScenePlacementPreview(block.transform, preview);
            }

            if (TryHandleScenePlacementClick(current, block, preview))
            {
                QueueDeferredRepaint();
            }
        }
    }

    private void RequestLiveScenePreviewRefresh(Event current)
    {
        if (current == null)
        {
            return;
        }

        if (current.type == EventType.MouseMove || current.type == EventType.MouseDrag)
        {
            QueueDeferredSceneRepaint();
        }
    }

    private void DrawGridBlockSettingsSection()
    {
        blockSettingsFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(blockSettingsFoldout, "Block Settings");
        if (blockSettingsFoldout)
        {
            EditorGUI.BeginChangeCheck();
            SpaceSegmentKit nextSegmentKit = (SpaceSegmentKit)EditorGUILayout.ObjectField(
                "Segment Kit",
                selectedSegmentKit,
                typeof(SpaceSegmentKit),
                false);
            if (EditorGUI.EndChangeCheck())
            {
                selectedSegmentKit = nextSegmentKit;
                MarkSegmentPaletteDirty();
            }

            DrawTwoColumnFieldRow(
                () => gridBlockId = EditorGUILayout.TextField("Block Id", gridBlockId),
                () => gridWidth = Mathf.Max(1, EditorGUILayout.IntField("Grid Width", gridWidth)));

            DrawTwoColumnFieldRow(
                () => gridDepth = Mathf.Max(1, EditorGUILayout.IntField("Grid Depth", gridDepth)),
                () => gridSize = Mathf.Max(0.1f, EditorGUILayout.FloatField("Grid Size", gridSize)));

            EditorGUILayout.Space(4f);
            if (GUILayout.Button("Create New Editable Block"))
            {
                CreateNewEditableBlock();
            }
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void DrawSegmentPaletteSection()
    {
        segmentPaletteFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(segmentPaletteFoldout, "Segment Palette");
        if (segmentPaletteFoldout)
        {
            SegmentCategory nextCategoryFilter = placementCategoryFilter;
            string nextSubFilter = segmentPaletteSubFilter;

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope())
                {
                    nextCategoryFilter = DrawPlacementCategoryFilter(placementCategoryFilter);
                }

                using (new EditorGUILayout.VerticalScope())
                {
                    nextSubFilter = DrawPaletteSubFilterPopup(nextCategoryFilter, segmentPaletteSubFilter);
                }
            }

            if (nextCategoryFilter != placementCategoryFilter)
            {
                placementCategoryFilter = nextCategoryFilter;
                selectedGridSegmentDefinition = null;
                selectedPaletteIndex = 0;
                segmentPaletteSubFilter = string.Empty;
                MarkSegmentPaletteDirty();
                SceneView.RepaintAll();
            }
            else if (!string.Equals(nextSubFilter, segmentPaletteSubFilter, StringComparison.Ordinal))
            {
                segmentPaletteSubFilter = nextSubFilter;
                selectedGridSegmentDefinition = null;
                selectedPaletteIndex = 0;
                MarkSegmentPaletteDirty();
                SceneView.RepaintAll();
            }

            string nextSearch = EditorGUILayout.TextField("Search", segmentPaletteSearch);
            if (!string.Equals(nextSearch, segmentPaletteSearch, StringComparison.Ordinal))
            {
                segmentPaletteSearch = nextSearch;
                selectedPaletteIndex = 0;
                MarkSegmentPaletteDirty();
            }

            EnsureSegmentPaletteIsCurrent();

            EditorGUI.BeginChangeCheck();
            SpaceSegmentDefinition nextSelectedDefinition = (SpaceSegmentDefinition)EditorGUILayout.ObjectField(
                "Selected Segment",
                selectedGridSegmentDefinition,
                typeof(SpaceSegmentDefinition),
                false);
            if (EditorGUI.EndChangeCheck())
            {
                selectedGridSegmentDefinition = nextSelectedDefinition;
                if (selectedGridSegmentDefinition != null)
                {
                    placementCategoryFilter = GetSupportedPlacementCategory(selectedGridSegmentDefinition.category);
                    segmentPaletteSubFilter = BuildPaletteStyleFolder(selectedGridSegmentDefinition);
                    MarkSegmentPaletteDirty();
                    EnsureSegmentPaletteIsCurrent();
                }

                SceneView.RepaintAll();
            }

            if (segmentPaletteEntries.Count == 0)
            {
                EditorGUILayout.HelpBox("No segments match the current category filter or search.", MessageType.Warning);
            }
            else
            {
                DrawSegmentPaletteGrid();
            }

            scenePaintSettingsFoldout = EditorGUILayout.Foldout(scenePaintSettingsFoldout, "Scene Paint Settings", true);
            if (scenePaintSettingsFoldout)
            {
                DrawTwoColumnFieldRow(
                    () => placeRotationY = NormalizeRotation(EditorGUILayout.IntField("Rotation Y", placeRotationY)),
                    () => replaceExistingPlacement = EditorGUILayout.Toggle("Replace Overlap", replaceExistingPlacement));

                DrawTwoColumnFieldRow(
                    () => markConnectorCandidate = EditorGUILayout.Toggle("Connector Candidate", markConnectorCandidate),
                    () => scenePlacementEnabled = EditorGUILayout.Toggle("Scene Placement", scenePlacementEnabled));

                DrawTwoColumnFieldRow(
                    () => sceneAutoPickWallSide = EditorGUILayout.Toggle("Auto Pick Wall Side", sceneAutoPickWallSide),
                    () => sceneDeleteMode = EditorGUILayout.Toggle("Scene Delete Mode", sceneDeleteMode));

                if (!sceneAutoPickWallSide
                    && (placementCategoryFilter == SegmentCategory.Wall || placementCategoryFilter == SegmentCategory.OpeningOverlay))
                {
                    placeWallSide = (WallSide)EditorGUILayout.EnumPopup("Wall Side", placeWallSide);
                }

                EditorGUILayout.HelpBox(
                    "Scene controls: hover the grid to preview, left-click to place, press R to rotate 90 degrees, and Shift+Click or enable Scene Delete Mode to remove a placed segment.",
                    MessageType.None);
            }
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void DrawSelectedSegmentAssetSection()
    {
        selectedSegmentAssetFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(selectedSegmentAssetFoldout, "Selected Segment Asset");
        if (selectedSegmentAssetFoldout)
        {
            if (selectedGridSegmentDefinition == null)
            {
                EditorGUILayout.HelpBox("Pick a segment from the palette to inspect or edit its asset content here.", MessageType.Info);
            }
            else
            {
                string definitionPath = AssetDatabase.GetAssetPath(selectedGridSegmentDefinition);
                EditorGUILayout.ObjectField("Definition Asset", selectedGridSegmentDefinition, typeof(SpaceSegmentDefinition), false);
                EditorGUILayout.TextField("Definition Path", definitionPath);

                GameObject prefabAsset = selectedGridSegmentDefinition.prefab;
                EditorGUILayout.ObjectField("Prefab Asset", prefabAsset, typeof(GameObject), false);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Ping Definition"))
                    {
                        EditorGUIUtility.PingObject(selectedGridSegmentDefinition);
                    }

                    using (new EditorGUI.DisabledScope(prefabAsset == null))
                    {
                        if (GUILayout.Button("Ping Prefab"))
                        {
                            EditorGUIUtility.PingObject(prefabAsset);
                        }
                    }
                }

                EditorGUILayout.Space(4f);
                DrawSelectedSegmentDefinitionInspector(selectedGridSegmentDefinition);
            }
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void DrawBlockDefinitionSection()
    {
        blockDefinitionFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(blockDefinitionFoldout, "Block Definition");
        if (blockDefinitionFoldout)
        {
            selectedBlockDefinitionAsset = (SpaceBlockDefinition)EditorGUILayout.ObjectField(
                "Block Definition",
                selectedBlockDefinitionAsset,
                typeof(SpaceBlockDefinition),
                false);
            bakePrefabName = EditorGUILayout.TextField("Bake Prefab Name", bakePrefabName);

            DrawTwoColumnButtonRow("Save Block Definition", SaveBlockDefinition, "Load Block Definition", LoadBlockDefinition);
            DrawTwoColumnButtonRow("Bake Block Prefab", BakeBlockPrefab, "Validate Block", ValidateGridBlock);
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
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

    private void HandleSceneHotkeys(Event current)
    {
        if (current == null || current.type != EventType.KeyDown)
        {
            return;
        }

        if (current.keyCode == KeyCode.R)
        {
            placeRotationY = NormalizeRotation(placeRotationY + 90);
            current.Use();
            QueueDeferredRepaint();
        }
    }

    private void QueueDeferredRepaint()
    {
        if (deferredRepaintQueued)
        {
            return;
        }

        deferredRepaintQueued = true;
        EditorApplication.delayCall += DeferredRepaint;
    }

    private void QueueDeferredSceneRepaint()
    {
        if (deferredSceneRepaintQueued || deferredRepaintQueued)
        {
            return;
        }

        deferredSceneRepaintQueued = true;
        EditorApplication.delayCall += DeferredSceneRepaint;
    }

    private void DeferredRepaint()
    {
        deferredRepaintQueued = false;

        if (this == null)
        {
            return;
        }

        Repaint();
        SceneView.RepaintAll();
    }

    private void DeferredSceneRepaint()
    {
        deferredSceneRepaintQueued = false;

        if (this == null)
        {
            return;
        }

        SceneView.RepaintAll();
    }

    private bool TryBuildDeletePreview(MemorySpaceBlock block, Event current, out ScenePlacementPreview preview)
    {
        preview = default;
        if (block == null || (!sceneDeleteMode && (current == null || !current.shift)) || current == null || current.alt)
        {
            return false;
        }

        if (!TryRaycastAuthoringPlane(block.transform, current.mousePosition, out Vector3 localPoint))
        {
            return false;
        }

        if (!TryFindNearestPlacementMetadata(block, localPoint, wallOnly: false, out SpaceSegmentPlacementMetadata metadata) || metadata == null)
        {
            return false;
        }

        if (!TryGetPlacementPreviewBounds(metadata.gameObject, out Vector3 localCenter, out Vector3 localSize))
        {
            localCenter = block != null ? block.transform.InverseTransformPoint(metadata.transform.position) : metadata.transform.localPosition;
            localSize = Vector3.one * gridSize;
        }

        preview.HasPreview = true;
        preview.CanPlace = false;
        preview.IsDeletePreview = true;
        preview.TargetPlacement = metadata;
        preview.LocalCenter = localCenter;
        preview.LocalSize = localSize;
        preview.Message = $"Delete {metadata.record.segmentId}";
        return true;
    }

    private bool TryBuildScenePlacementPreview(MemorySpaceBlock block, Event current, out ScenePlacementPreview preview)
    {
        preview = default;
        if (block == null || current == null || current.alt || selectedGridSegmentDefinition == null)
        {
            return false;
        }

        if (GetSupportedPlacementCategory(selectedGridSegmentDefinition.category) == SegmentCategory.OpeningOverlay)
        {
            return TryBuildOverlayPreview(block, current, out preview);
        }

        if (!TryRaycastAuthoringPlane(block.transform, current.mousePosition, out Vector3 localPoint))
        {
            return false;
        }

        if (!TryBuildRecordFromScenePoint(localPoint, selectedGridSegmentDefinition, out SpaceSegmentPlacementRecord record, out string message))
        {
            preview.HasPreview = true;
            preview.CanPlace = false;
            preview.Definition = selectedGridSegmentDefinition;
            preview.Message = message;
            preview.LocalCenter = localPoint;
            preview.LocalSize = Vector3.one * gridSize;
            return true;
        }

        List<SpaceSegmentPlacementMetadata> overlaps = FindOverlappingPlacements(block, record);
        List<string> validationMessages = ValidatePlacementRecord(record, selectedGridSegmentDefinition, block, selectedSegmentKit != null ? selectedSegmentKit : block.segmentKit);

        preview.HasPreview = true;
        preview.Definition = selectedGridSegmentDefinition;
        preview.Record = record;
        preview.Overlaps = overlaps;
        preview.LocalCenter = GetScenePreviewLocalCenter(record, selectedGridSegmentDefinition);
        preview.LocalSize = GetScenePreviewSize(record);
        preview.CanPlace = validationMessages.Count == 0 && (overlaps.Count == 0 || replaceExistingPlacement);

        if (validationMessages.Count > 0)
        {
            preview.Message = string.Join(" | ", validationMessages.ToArray());
        }
        else if (overlaps.Count > 0 && !replaceExistingPlacement)
        {
            preview.Message = $"Overlap with {overlaps.Count} existing placement(s)";
        }
        else if (overlaps.Count > 0)
        {
            preview.Message = $"Replace {overlaps.Count} existing placement(s)";
        }
        else
        {
            preview.Message = $"{record.segmentId} @ ({record.gridX}, {record.gridZ})";
        }

        return true;
    }

    private bool TryBuildOverlayPreview(MemorySpaceBlock block, Event current, out ScenePlacementPreview preview)
    {
        preview = default;
        if (block == null || current == null || selectedGridSegmentDefinition == null)
        {
            return false;
        }

        if (!TryRaycastAuthoringPlane(block.transform, current.mousePosition, out Vector3 localPoint))
        {
            return false;
        }

        if (!TryFindNearestPlacementMetadata(block, localPoint, wallOnly: true, out SpaceSegmentPlacementMetadata metadata) || metadata == null)
        {
            return false;
        }

        WallSegmentSlot wallSlot = metadata.GetComponent<WallSegmentSlot>();
        if (wallSlot == null || metadata.record == null || metadata.record.category != SegmentCategory.Wall)
        {
            return false;
        }

        if (!CanAttachOverlayToWall(metadata, selectedGridSegmentDefinition, out string reason))
        {
            preview.HasPreview = true;
            preview.CanPlace = false;
            preview.IsOverlayPreview = true;
            preview.Definition = selectedGridSegmentDefinition;
            preview.TargetPlacement = metadata;
            preview.Record = CloneRecord(metadata.record);
            preview.LocalCenter = GetOverlayPreviewLocalCenter(block, metadata, selectedGridSegmentDefinition);
            preview.LocalSize = GetOverlayPreviewSize(metadata.record, selectedGridSegmentDefinition);
            preview.Message = reason;
            return true;
        }

        preview.HasPreview = true;
        preview.CanPlace = true;
        preview.IsOverlayPreview = true;
        preview.Definition = selectedGridSegmentDefinition;
        preview.TargetPlacement = metadata;
        preview.Record = CloneRecord(metadata.record);
        preview.Record.overlaySegmentId = selectedGridSegmentDefinition.segmentId;
        preview.LocalCenter = GetOverlayPreviewLocalCenter(block, metadata, selectedGridSegmentDefinition);
        preview.LocalSize = GetOverlayPreviewSize(metadata.record, selectedGridSegmentDefinition);
        preview.Message = $"Attach overlay {selectedGridSegmentDefinition.segmentId}";
        return true;
    }

    private bool TryHandleScenePlacementClick(Event current, MemorySpaceBlock block, ScenePlacementPreview preview)
    {
        if (current == null
            || current.type != EventType.MouseDown
            || current.button != 0
            || current.alt
            || !preview.HasPreview)
        {
            return false;
        }

        if (preview.IsOverlayPreview)
        {
            AttachOverlayToPlacement(block, preview.TargetPlacement, selectedGridSegmentDefinition);
            current.Use();
            return true;
        }

        if (!preview.CanPlace)
        {
            current.Use();
            return true;
        }

        SpaceSegmentKit kit = selectedSegmentKit != null ? selectedSegmentKit : block.segmentKit;
        if (TryPlaceSegmentRecord(block, kit, preview.Record, preview.Definition, showDialogs: false))
        {
            placeGridX = preview.Record.gridX;
            placeGridZ = preview.Record.gridZ;
            placeWallSide = preview.Record.side;
            current.Use();
            return true;
        }

        current.Use();
        return true;
    }

    private bool TryHandleDeletePreviewClick(Event current, ScenePlacementPreview preview)
    {
        if (current == null
            || current.type != EventType.MouseDown
            || current.button != 0
            || current.alt
            || !preview.IsDeletePreview
            || preview.TargetPlacement == null)
        {
            return false;
        }

        DeletePlacementMetadata(preview.TargetPlacement);
        current.Use();
        return true;
    }

    private void DrawScenePlacementPreview(Transform blockRoot, ScenePlacementPreview preview)
    {
        if (blockRoot == null || !preview.HasPreview)
        {
            return;
        }

        Handles.matrix = blockRoot.localToWorldMatrix;
        Handles.color = GetPreviewColor(preview);
        Handles.DrawWireCube(preview.LocalCenter, preview.LocalSize);
        Handles.matrix = Matrix4x4.identity;

        Vector3 worldLabelPosition = blockRoot.TransformPoint(preview.LocalCenter + Vector3.up * 0.2f);
        Handles.Label(worldLabelPosition, preview.Message);
    }

    private static Color GetPreviewColor(ScenePlacementPreview preview)
    {
        if (preview.IsDeletePreview)
        {
            return new Color(1f, 0.35f, 0.35f, 0.95f);
        }

        if (!preview.CanPlace)
        {
            return new Color(1f, 0.45f, 0.2f, 0.95f);
        }

        if (preview.Overlaps != null && preview.Overlaps.Count > 0)
        {
            return new Color(1f, 0.85f, 0.2f, 0.95f);
        }

        return new Color(0.35f, 1f, 0.55f, 0.95f);
    }

    private void DrawSegmentPaletteGrid()
    {
        segmentPaletteScrollPosition = EditorGUILayout.BeginScrollView(
            segmentPaletteScrollPosition,
            GUILayout.MinHeight(140f),
            GUILayout.MaxHeight(320f));

        int columns = Mathf.Max(1, Mathf.FloorToInt((position.width - 32f) / PaletteButtonWidth));
        for (int index = 0; index < segmentPaletteEntries.Count; index += columns)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                for (int column = 0; column < columns; column++)
                {
                    int entryIndex = index + column;
                    if (entryIndex >= segmentPaletteEntries.Count)
                    {
                        GUILayout.FlexibleSpace();
                        continue;
                    }

                    DrawSegmentPaletteButton(segmentPaletteEntries[entryIndex], entryIndex);
                }
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawSegmentPaletteButton(SegmentPaletteEntry entry, int entryIndex)
    {
        if (entry.Definition == null)
        {
            return;
        }

        Texture previewTexture = GetSegmentPaletteThumbnail(entry.Definition);

        GUIContent content = new GUIContent(
            $"{entry.ButtonLabel}\n{entry.SecondaryLabel}",
            previewTexture,
            entry.Label);

        Color previousColor = GUI.backgroundColor;
        if (entry.Definition == selectedGridSegmentDefinition)
        {
            GUI.backgroundColor = new Color(0.45f, 0.8f, 1f, 1f);
        }

        if (GUILayout.Button(
                content,
                GetSegmentPaletteButtonStyle(),
                GUILayout.Width(PaletteButtonWidth),
                GUILayout.Height(PaletteButtonHeight)))
        {
            selectedPaletteIndex = entryIndex;
            selectedGridSegmentDefinition = entry.Definition;
            SceneView.RepaintAll();
            Repaint();
        }

        GUI.backgroundColor = previousColor;
    }

    private GUIStyle GetSegmentPaletteButtonStyle()
    {
        if (segmentPaletteButtonStyle != null)
        {
            return segmentPaletteButtonStyle;
        }

        segmentPaletteButtonStyle = new GUIStyle(GUI.skin.button)
        {
            wordWrap = true,
            imagePosition = ImagePosition.ImageAbove,
            alignment = TextAnchor.UpperCenter,
            fixedWidth = PaletteButtonWidth,
            fixedHeight = PaletteButtonHeight,
            padding = new RectOffset(6, 6, 6, 6)
        };
        return segmentPaletteButtonStyle;
    }

    private void DrawSelectedSegmentDefinitionInspector(SpaceSegmentDefinition definition)
    {
        if (definition == null)
        {
            return;
        }

        SerializedObject serializedDefinition = new SerializedObject(definition);
        serializedDefinition.UpdateIfRequiredOrScript();
        SerializedProperty iterator = serializedDefinition.GetIterator();
        bool enterChildren = true;

        while (iterator.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (string.Equals(iterator.propertyPath, "m_Script", StringComparison.Ordinal))
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.PropertyField(iterator, true);
                }

                continue;
            }

            EditorGUILayout.PropertyField(iterator, true);
        }

        if (serializedDefinition.ApplyModifiedProperties())
        {
            EditorUtility.SetDirty(definition);
            MarkSegmentPaletteDirty();
            SceneView.RepaintAll();
            Repaint();
        }
    }

    private static void DrawTwoColumnFieldRow(Action drawLeft, Action drawRight)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUILayout.VerticalScope())
            {
                drawLeft?.Invoke();
            }

            using (new EditorGUILayout.VerticalScope())
            {
                drawRight?.Invoke();
            }
        }
    }

    private void DrawTwoColumnButtonRow(string leftLabel, Action leftAction, string rightLabel, Action rightAction)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(leftLabel))
            {
                leftAction?.Invoke();
            }

            if (GUILayout.Button(rightLabel))
            {
                rightAction?.Invoke();
            }
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

            if (!SegmentMatchesSearch(definition, segmentPaletteSearch))
            {
                continue;
            }

            if (!SegmentMatchesSubFilter(definition, segmentPaletteSubFilter))
            {
                continue;
            }

            segmentPaletteEntries.Add(new SegmentPaletteEntry
            {
                Label = BuildSegmentPalettePath(definition),
                ButtonLabel = GetPaletteLeafLabel(definition),
                SecondaryLabel = $"{BuildPaletteStyleFolder(definition)} | {GetDefinitionDisplaySizeLabel(definition)}",
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
            if (selectedGridSegmentDefinition == null)
            {
                selectedGridSegmentDefinition = segmentPaletteEntries[selectedPaletteIndex].Definition;
            }
        }
    }

    private void EnsureSegmentPaletteIsCurrent()
    {
        string normalizedSearch = segmentPaletteSearch ?? string.Empty;
        string normalizedSubFilter = segmentPaletteSubFilter ?? string.Empty;
        if (!segmentPaletteDirty
            && cachedPaletteKit == selectedSegmentKit
            && cachedPaletteCategory == placementCategoryFilter
            && string.Equals(cachedPaletteSearch, normalizedSearch, StringComparison.Ordinal)
            && string.Equals(cachedPaletteSubFilter, normalizedSubFilter, StringComparison.Ordinal))
        {
            return;
        }

        RebuildSegmentPalette();
        cachedPaletteKit = selectedSegmentKit;
        cachedPaletteCategory = placementCategoryFilter;
        cachedPaletteSearch = normalizedSearch;
        cachedPaletteSubFilter = normalizedSubFilter;
        segmentPaletteDirty = false;
    }

    private void MarkSegmentPaletteDirty()
    {
        segmentPaletteDirty = true;
    }

    private Texture GetSegmentPaletteThumbnail(SpaceSegmentDefinition definition)
    {
        if (definition == null)
        {
            return null;
        }

        UnityEngine.Object thumbnailSource = definition.prefab != null ? (UnityEngine.Object)definition.prefab : definition;
        int cacheKey = thumbnailSource.GetInstanceID();
        if (segmentPaletteThumbnailCache.TryGetValue(cacheKey, out Texture cachedTexture) && cachedTexture != null)
        {
            return cachedTexture;
        }

        Texture previewTexture = AssetPreview.GetMiniThumbnail(thumbnailSource);
        if (previewTexture == null && thumbnailSource != definition)
        {
            previewTexture = AssetPreview.GetMiniThumbnail(definition);
        }

        if (previewTexture != null)
        {
            segmentPaletteThumbnailCache[cacheKey] = previewTexture;
        }

        return previewTexture;
    }

    private static string BuildSegmentPalettePath(SpaceSegmentDefinition definition)
    {
        if (definition == null)
        {
            return "none";
        }

        string category = definition.category.ToString().ToLowerInvariant();
        string style = BuildPaletteStyleFolder(definition);
        string size = GetDefinitionDisplaySizeLabel(definition);
        string leaf = GetPaletteLeafLabel(definition);
        return $"{category}/{style}/{size}/{leaf}";
    }

    private static bool SegmentMatchesSearch(SpaceSegmentDefinition definition, string search)
    {
        if (definition == null || string.IsNullOrWhiteSpace(search))
        {
            return true;
        }

        string haystack = string.Join(
            " ",
            definition.segmentId,
            definition.styleId,
            definition.category.ToString(),
            definition.variant.ToString(),
            BuildSegmentPalettePath(definition));

        return haystack.IndexOf(search.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool SegmentMatchesSubFilter(SpaceSegmentDefinition definition, string subFilter)
    {
        if (definition == null || string.IsNullOrWhiteSpace(subFilter))
        {
            return true;
        }

        return string.Equals(BuildPaletteStyleFolder(definition), subFilter, StringComparison.OrdinalIgnoreCase);
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

    private static string BuildPaletteStyleFolder(SpaceSegmentDefinition definition)
    {
        if (definition == null)
        {
            return "default";
        }

        string style = string.IsNullOrWhiteSpace(definition.styleId) ? "default" : definition.styleId.ToLowerInvariant();
        if (definition.category == SegmentCategory.Wall && !style.StartsWith("wall_", StringComparison.OrdinalIgnoreCase))
        {
            return $"wall_{style}";
        }

        return style;
    }

    private string DrawPaletteSubFilterPopup(SegmentCategory category, string currentValue)
    {
        segmentPaletteSubFilterOptions.Clear();
        segmentPaletteSubFilterOptions.Add(AllPaletteSubFiltersLabel);

        if (selectedSegmentKit != null && selectedSegmentKit.segments != null)
        {
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < selectedSegmentKit.segments.Count; i++)
            {
                SpaceSegmentDefinition definition = selectedSegmentKit.segments[i];
                if (definition == null || GetSupportedPlacementCategory(definition.category) != category)
                {
                    continue;
                }

                string style = BuildPaletteStyleFolder(definition);
                if (!string.IsNullOrWhiteSpace(style) && seen.Add(style))
                {
                    segmentPaletteSubFilterOptions.Add(style);
                }
            }
        }

        if (segmentPaletteSubFilterOptions.Count > 2)
        {
            segmentPaletteSubFilterOptions.Sort(1, segmentPaletteSubFilterOptions.Count - 1, StringComparer.OrdinalIgnoreCase);
        }

        string normalizedCurrent = string.IsNullOrWhiteSpace(currentValue) ? AllPaletteSubFiltersLabel : currentValue;
        int selectedIndex = segmentPaletteSubFilterOptions.FindIndex(option => string.Equals(option, normalizedCurrent, StringComparison.OrdinalIgnoreCase));
        if (selectedIndex < 0)
        {
            selectedIndex = 0;
        }

        int nextIndex = EditorGUILayout.Popup("Sub Filter", selectedIndex, segmentPaletteSubFilterOptions.ToArray());
        string selectedValue = segmentPaletteSubFilterOptions[Mathf.Clamp(nextIndex, 0, segmentPaletteSubFilterOptions.Count - 1)];
        return string.Equals(selectedValue, AllPaletteSubFiltersLabel, StringComparison.Ordinal) ? string.Empty : selectedValue;
    }

    private static string GetPaletteLeafLabel(SpaceSegmentDefinition definition)
    {
        if (definition == null)
        {
            return "none";
        }

        if (definition.category == SegmentCategory.OpeningOverlay
            && IsDoorwayDefinition(definition))
        {
            return "doorway";
        }

        if (definition.variant == SegmentVariant.Default || definition.variant == SegmentVariant.Solid)
        {
            return "solid";
        }

        return definition.variant.ToString().ToLowerInvariant();
    }

    private static string GetDefinitionDisplaySizeLabel(SpaceSegmentDefinition definition)
    {
        if (definition == null)
        {
            return "1x1";
        }

        float width = GetDefinitionDisplayWidth(definition);
        float height = GetDefinitionDisplayHeight(definition);
        return $"{FormatDefinitionNumber(width)}x{FormatDefinitionNumber(height)}";
    }

    private static float GetDefinitionDisplayWidth(SpaceSegmentDefinition definition)
    {
        if (definition == null)
        {
            return 1f;
        }

        if (definition.sizeXZ.x > 1f)
        {
            return Mathf.Max(1f, definition.sizeXZ.x);
        }

        if (TryExtractDefinitionSize(definition, out float width, out _))
        {
            return Mathf.Max(1f, width);
        }

        return Mathf.Max(1f, definition.sizeXZ.x);
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

        if ((definition.category == SegmentCategory.OpeningOverlay || definition.category == SegmentCategory.Wall)
            && TryExtractDefinitionSize(definition, out _, out float parsedHeight))
        {
            return Mathf.Max(1f, parsedHeight);
        }

        if (definition.category == SegmentCategory.OpeningOverlay && definition.sizeXZ.y > 1f)
        {
            return definition.sizeXZ.y;
        }

        return definition.category == SegmentCategory.Wall ? 1f : Mathf.Max(1f, definition.sizeXZ.y);
    }

    private static bool TryExtractDefinitionSize(SpaceSegmentDefinition definition, out float sizeX, out float sizeY)
    {
        sizeX = 1f;
        sizeY = 1f;

        if (definition == null || string.IsNullOrWhiteSpace(definition.segmentId))
        {
            return false;
        }

        string[] tokens = definition.segmentId.Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = tokens.Length - 1; i >= 0; i--)
        {
            if (TryParseDefinitionSizeToken(tokens[i], out sizeX, out sizeY))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryParseDefinitionSizeToken(string token, out float sizeX, out float sizeY)
    {
        sizeX = 1f;
        sizeY = 1f;
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        string[] parts = token.Split('x', 'X');
        if (parts.Length != 2)
        {
            return false;
        }

        return float.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out sizeX)
            && float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out sizeY);
    }

    private static string FormatDefinitionNumber(float value)
    {
        return value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static bool IsDoorwayDefinition(SpaceSegmentDefinition definition)
    {
        return definition != null
            && definition.segmentId != null
            && definition.segmentId.IndexOf("doorway", StringComparison.OrdinalIgnoreCase) >= 0;
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
            if (TryPlaceSegmentRecord(block, kit, record, selectedGridSegmentDefinition, showDialogs: true))
            {
                ShowSummary($"Placed {selectedGridSegmentDefinition.segmentId}.");
            }
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

            DeletePlacementMetadata(metadata);
            ShowSummary("Deleted selected placed segment.");
        }
        catch (Exception exception)
        {
            ReportException("Delete Selected Segment", exception);
        }
    }

    private bool TryPlaceSegmentRecord(
        MemorySpaceBlock block,
        SpaceSegmentKit kit,
        SpaceSegmentPlacementRecord record,
        SpaceSegmentDefinition definition,
        bool showDialogs)
    {
        if (block == null || kit == null || record == null || definition == null)
        {
            return false;
        }

        List<string> validationMessages = ValidatePlacementRecord(record, definition, block, kit);
        if (validationMessages.Count > 0)
        {
            if (showDialogs)
            {
                ShowMessages("Place Selected Segment", validationMessages);
            }

            return false;
        }

        List<SpaceSegmentPlacementMetadata> overlaps = FindOverlappingPlacements(block, record);
        if (overlaps.Count > 0 && !replaceExistingPlacement)
        {
            if (showDialogs)
            {
                ShowSummary($"Placement overlaps {overlaps.Count} existing segment(s). Enable Replace Overlap to replace them.");
            }

            return false;
        }

        Undo.RegisterFullObjectHierarchyUndo(block.gameObject, "Place Space Segment");
        for (int i = 0; i < overlaps.Count; i++)
        {
            Undo.DestroyObjectImmediate(overlaps[i].gameObject);
        }

        CreatePlacementInstance(block, record, definition, kit, editable: true);
        block.NormalizeWallSlotRoots();
        EditorUtility.SetDirty(block);
        Selection.activeGameObject = block.gameObject;
        return true;
    }

    private void DeletePlacementMetadata(SpaceSegmentPlacementMetadata metadata)
    {
        if (metadata == null)
        {
            return;
        }

        MemorySpaceBlock block = metadata.GetComponentInParent<MemorySpaceBlock>();
        Undo.RegisterFullObjectHierarchyUndo(metadata.gameObject, "Delete Space Segment");
        Undo.DestroyObjectImmediate(metadata.gameObject);
        if (block != null)
        {
            EditorUtility.SetDirty(block);
            Selection.activeGameObject = block.gameObject;
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
            string prefabFileName = GetBakePrefabFileName(sourceBlock);
            GameObject bakeRoot = new GameObject(prefabFileName);
            try
            {
                MemorySpaceBlock bakedBlock = bakeRoot.AddComponent<MemorySpaceBlock>();
                bakedBlock.spaceBlockId = blockId;
                bakedBlock.spaceBlockType = SpaceBlockType.Custom;
                bakedBlock.widthUnits = sourceBlock.widthUnits;
                bakedBlock.depthUnits = sourceBlock.depthUnits;
                bakedBlock.segmentKit = kit;
                bakedBlock.blockDefinition = selectedBlockDefinitionAsset;

                GridBlockRoots roots = EnsureGridBlockRoots(bakeRoot.transform);
                EnsureDoorwayPortsForBlock(sourceBlock);
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
                CopyDoorwayPlacementState(sourceBlock, bakedBlock);
                bakedBlock.GetOrCreateBlockBoundsCollider();
                string prefabPath = AssetDatabase.GenerateUniqueAssetPath(
                    Path.Combine(SpaceBlockPrefabFolder, $"{prefabFileName}.prefab").Replace("\\", "/"));
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

    private void DrawBlockConnectionTool()
    {
        blockConnectionFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(blockConnectionFoldout, "Block Connection Tool");
        if (blockConnectionFoldout)
        {
            connectionPortA = (SpaceOpeningPort)EditorGUILayout.ObjectField("Port A", connectionPortA, typeof(SpaceOpeningPort), true);
            connectionPortB = (SpaceOpeningPort)EditorGUILayout.ObjectField("Port B", connectionPortB, typeof(SpaceOpeningPort), true);

            DrawTwoColumnFieldRow(
                () => connectionAutoAlignBlockB = EditorGUILayout.Toggle("Auto Align Block B", connectionAutoAlignBlockB),
                () => connectionAllowOverlapAnyway = EditorGUILayout.Toggle("Allow Overlap Anyway", connectionAllowOverlapAnyway));

            DrawTwoColumnButtonRow("Use Selected As A", AssignSelectedPortToConnectionA, "Use Selected As B", AssignSelectedPortToConnectionB);
            DrawTwoColumnButtonRow("Swap A / B", SwapConnectionPorts, "Clear A / B", ClearConnectionPortSelection);

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Port Browser", EditorStyles.boldLabel);
            DrawTwoColumnFieldRow(
                () => connectionPortSearch = EditorGUILayout.TextField("Search", connectionPortSearch),
                () => connectionShowOccupiedPorts = EditorGUILayout.Toggle("Show Occupied", connectionShowOccupiedPorts));
            EditorGUILayout.HelpBox("Click Set A / Set B below to assign ports directly from the scene without selecting objects one by one in the Hierarchy.", MessageType.None);
            DrawConnectionPortBrowser();

            DrawTwoColumnButtonRow("Auto Find Matching Ports", AutoFindMatchingPorts, "Preview Connection", PreviewSelectedConnection);
            DrawTwoColumnButtonRow("Connect Selected Doorways", ConnectSelectedDoorways, "Clear Connection", ClearSelectedConnection);

            if (GUILayout.Button("Validate Ports"))
            {
                ValidateSelectedPorts();
            }
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void AutoFindMatchingPorts()
    {
        try
        {
            CaptureConnectionPortsFromSelection();
            if (connectionPortA == null && connectionPortB != null)
            {
                connectionPortA = connectionPortB;
                connectionPortB = null;
            }

            if (connectionPortA == null)
            {
                ShowSummary("Select or assign Port A first.");
                return;
            }

            SpaceConnectionManager manager = SpaceConnectionManager.GetOrCreateManager();
            if (!manager.TryFindBestMatch(connectionPortA, connectionAutoAlignBlockB, out SpaceOpeningPort match, out string message))
            {
                ShowSummary(message);
                return;
            }

            connectionPortB = match;
            Selection.activeGameObject = match.gameObject;
            ShowSummary(message);
        }
        catch (Exception exception)
        {
            ReportException("Auto Find Matching Ports", exception);
        }
    }

    private void PreviewSelectedConnection()
    {
        try
        {
            if (!TryGetConnectionManager(out SpaceConnectionManager manager))
            {
                return;
            }

            if (manager.PreviewConnection(
                connectionPortA,
                connectionPortB,
                connectionAutoAlignBlockB,
                connectionAllowOverlapAnyway,
                out string message))
            {
                ShowSummary(message);
                return;
            }

            ShowSummary(message);
        }
        catch (Exception exception)
        {
            ReportException("Preview Connection", exception);
        }
    }

    private void ConnectSelectedDoorways()
    {
        try
        {
            if (!TryGetConnectionManager(out SpaceConnectionManager manager))
            {
                return;
            }

            if (connectionPortA != null && connectionPortA.OwningBlock != null)
            {
                Undo.RegisterFullObjectHierarchyUndo(connectionPortA.OwningBlock.gameObject, "Connect Space Doorways");
            }

            if (connectionPortB != null
                && connectionPortB.OwningBlock != null
                && (connectionPortA == null || connectionPortB.OwningBlock != connectionPortA.OwningBlock))
            {
                Undo.RegisterFullObjectHierarchyUndo(connectionPortB.OwningBlock.gameObject, "Connect Space Doorways");
            }

            if (manager.ConnectPorts(
                connectionPortA,
                connectionPortB,
                connectionAutoAlignBlockB,
                connectionAllowOverlapAnyway,
                out string message))
            {
                ShowSummary(message);
                return;
            }

            ShowSummary(message);
        }
        catch (Exception exception)
        {
            ReportException("Connect Selected Doorways", exception);
        }
    }

    private void ClearSelectedConnection()
    {
        try
        {
            SpaceConnectionManager manager = SpaceConnectionManager.GetOrCreateManager();
            if (manager.ClearConnection(connectionPortA, connectionPortB, out string message))
            {
                ShowSummary(message);
                return;
            }

            ShowSummary(message);
        }
        catch (Exception exception)
        {
            ReportException("Clear Connection", exception);
        }
    }

    private void ValidateSelectedPorts()
    {
        try
        {
            if (!TryGetConnectionManager(out SpaceConnectionManager manager))
            {
                return;
            }

            string message;
            bool valid = manager.ValidatePorts(
                connectionPortA,
                connectionPortB,
                connectionAutoAlignBlockB,
                connectionAllowOverlapAnyway,
                out message);

            ShowSummary(valid ? $"Validation passed. {message}" : $"Validation failed. {message}");
        }
        catch (Exception exception)
        {
            ReportException("Validate Ports", exception);
        }
    }

    private bool TryGetConnectionManager(out SpaceConnectionManager manager)
    {
        CaptureConnectionPortsFromSelection();
        manager = SpaceConnectionManager.GetOrCreateManager();
        if (connectionPortA == null || connectionPortB == null)
        {
            ShowSummary("Assign both Port A and Port B.");
            return false;
        }

        manager.RefreshRegisteredPorts();
        return true;
    }

    private void CaptureConnectionPortsFromSelection()
    {
        SpaceOpeningPort selectedPort = GetSelectedOpeningPort();
        if (selectedPort == null)
        {
            return;
        }

        if (connectionPortA == null)
        {
            connectionPortA = selectedPort;
            return;
        }

        if (connectionPortB == null && selectedPort != connectionPortA)
        {
            connectionPortB = selectedPort;
        }
    }

    private SpaceOpeningPort GetSelectedOpeningPort()
    {
        if (Selection.activeGameObject == null)
        {
            return null;
        }

        SpaceOpeningPort selectedPort = Selection.activeGameObject.GetComponentInParent<SpaceOpeningPort>();
        if (selectedPort != null)
        {
            return selectedPort;
        }

        MemorySpaceBlock selectedBlock = Selection.activeGameObject.GetComponentInParent<MemorySpaceBlock>();
        EnsureDoorwayPortsForBlock(selectedBlock);
        return GetFirstAvailableOpeningPort(selectedBlock);
    }

    private void EnsureDoorwayPortsForBlock(MemorySpaceBlock block)
    {
        if (block == null)
        {
            return;
        }

        SpaceSegmentKit kit = selectedSegmentKit != null ? selectedSegmentKit : block.segmentKit;
        SpaceSegmentPlacementMetadata[] placements = block.GetComponentsInChildren<SpaceSegmentPlacementMetadata>(true);
        for (int i = 0; i < placements.Length; i++)
        {
            SpaceSegmentPlacementMetadata metadata = placements[i];
            if (metadata == null || metadata.record == null || metadata.record.category != SegmentCategory.Wall)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(metadata.record.overlaySegmentId))
            {
                continue;
            }

            WallSegmentSlot wallSlot = metadata.GetComponent<WallSegmentSlot>();
            if (wallSlot == null)
            {
                continue;
            }

            SpaceSegmentDefinition overlayDefinition = metadata.overlayDefinition;
            if (overlayDefinition == null && kit != null)
            {
                overlayDefinition = kit.GetSegment(metadata.record.overlaySegmentId);
                metadata.overlayDefinition = overlayDefinition;
            }

            if (overlayDefinition != null && IsDoorwayDefinition(overlayDefinition))
            {
                CreateBakedOpeningPort(block, wallSlot, metadata.record, overlayDefinition);
            }
        }
    }

    private static SpaceOpeningPort GetFirstAvailableOpeningPort(MemorySpaceBlock block)
    {
        if (block == null)
        {
            return null;
        }

        SpaceOpeningPort[] ports = block.GetComponentsInChildren<SpaceOpeningPort>(true);
        if (ports == null || ports.Length == 0)
        {
            return null;
        }

        Array.Sort(ports, CompareOpeningPorts);
        for (int i = 0; i < ports.Length; i++)
        {
            if (ports[i] != null && !ports[i].isOccupied)
            {
                return ports[i];
            }
        }

        return ports[0];
    }

    private static int CompareOpeningPorts(SpaceOpeningPort a, SpaceOpeningPort b)
    {
        string aId = a != null ? a.openingId : string.Empty;
        string bId = b != null ? b.openingId : string.Empty;
        return string.Compare(aId, bId, StringComparison.OrdinalIgnoreCase);
    }

    private void AssignSelectedPortToConnectionA()
    {
        AssignSelectedPort(ConnectionPortAssignment.PortA);
    }

    private void AssignSelectedPortToConnectionB()
    {
        AssignSelectedPort(ConnectionPortAssignment.PortB);
    }

    private void AssignSelectedPort(ConnectionPortAssignment assignment)
    {
        SpaceOpeningPort selectedPort = GetSelectedOpeningPort();
        if (selectedPort == null)
        {
            ShowSummary("Select a doorway port or a baked block in the scene first.");
            return;
        }

        AssignConnectionPort(assignment, selectedPort);
    }

    private void SwapConnectionPorts()
    {
        SpaceOpeningPort previousA = connectionPortA;
        connectionPortA = connectionPortB;
        connectionPortB = previousA;
        Repaint();
        SceneView.RepaintAll();
    }

    private void ClearConnectionPortSelection()
    {
        connectionPortA = null;
        connectionPortB = null;
        Repaint();
    }

    private void DrawConnectionPortBrowser()
    {
        List<SpaceOpeningPort> ports = GetSceneOpeningPorts();
        if (ports.Count == 0)
        {
            EditorGUILayout.HelpBox("No scene ports found for the current filter.", MessageType.Info);
            return;
        }

        connectionPortBrowserScrollPosition = EditorGUILayout.BeginScrollView(
            connectionPortBrowserScrollPosition,
            GUILayout.MinHeight(160f),
            GUILayout.MaxHeight(300f));

        for (int i = 0; i < ports.Count; i++)
        {
            DrawConnectionPortBrowserRow(ports[i]);
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawConnectionPortBrowserRow(SpaceOpeningPort port)
    {
        if (port == null)
        {
            return;
        }

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(BuildConnectionPortLabel(port), GUILayout.MinWidth(260f));

                using (new EditorGUI.DisabledScope(port == connectionPortA))
                {
                    if (GUILayout.Button(port == connectionPortA ? "A Selected" : "Set A", GUILayout.Width(72f)))
                    {
                        AssignConnectionPort(ConnectionPortAssignment.PortA, port);
                    }
                }

                using (new EditorGUI.DisabledScope(port == connectionPortB))
                {
                    if (GUILayout.Button(port == connectionPortB ? "B Selected" : "Set B", GUILayout.Width(72f)))
                    {
                        AssignConnectionPort(ConnectionPortAssignment.PortB, port);
                    }
                }

                if (GUILayout.Button("Ping", GUILayout.Width(48f)))
                {
                    EditorGUIUtility.PingObject(port.gameObject);
                    Selection.activeGameObject = port.gameObject;
                }
            }
        }
    }

    private void AssignConnectionPort(ConnectionPortAssignment assignment, SpaceOpeningPort port)
    {
        if (port == null)
        {
            return;
        }

        switch (assignment)
        {
            case ConnectionPortAssignment.PortA:
                connectionPortA = port;
                if (connectionPortB == connectionPortA)
                {
                    connectionPortB = null;
                }
                break;
            case ConnectionPortAssignment.PortB:
                connectionPortB = port;
                if (connectionPortA == connectionPortB)
                {
                    connectionPortA = null;
                }
                break;
        }

        Selection.activeGameObject = port.gameObject;
        Repaint();
        SceneView.RepaintAll();
    }

    private List<SpaceOpeningPort> GetSceneOpeningPorts()
    {
        SpaceOpeningPort[] allPorts = Resources.FindObjectsOfTypeAll<SpaceOpeningPort>();
        List<SpaceOpeningPort> ports = new List<SpaceOpeningPort>();
        for (int i = 0; i < allPorts.Length; i++)
        {
            SpaceOpeningPort port = allPorts[i];
            if (port == null
                || EditorUtility.IsPersistent(port)
                || port.gameObject == null
                || !port.gameObject.scene.IsValid())
            {
                continue;
            }

            if (!connectionShowOccupiedPorts
                && port != connectionPortA
                && port != connectionPortB
                && (port.isOccupied || port.connectedPort != null))
            {
                continue;
            }

            if (!ConnectionPortMatchesSearch(port, connectionPortSearch))
            {
                continue;
            }

            ports.Add(port);
        }

        ports.Sort(CompareSceneOpeningPorts);
        return ports;
    }

    private static int CompareSceneOpeningPorts(SpaceOpeningPort a, SpaceOpeningPort b)
    {
        string blockA = a != null && a.OwningBlock != null ? a.OwningBlock.spaceBlockId : string.Empty;
        string blockB = b != null && b.OwningBlock != null ? b.OwningBlock.spaceBlockId : string.Empty;
        int blockCompare = string.Compare(blockA, blockB, StringComparison.OrdinalIgnoreCase);
        if (blockCompare != 0)
        {
            return blockCompare;
        }

        return CompareOpeningPorts(a, b);
    }

    private static bool ConnectionPortMatchesSearch(SpaceOpeningPort port, string search)
    {
        if (port == null || string.IsNullOrWhiteSpace(search))
        {
            return true;
        }

        string ownerId = port.OwningBlock != null ? port.OwningBlock.spaceBlockId : string.Empty;
        string haystack = string.Join(
            " ",
            port.openingId,
            ownerId,
            port.wallSide.ToString(),
            port.gridPosition.x.ToString(),
            port.gridPosition.y.ToString(),
            port.isOccupied ? "occupied" : "free");

        return haystack.IndexOf(search.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string BuildConnectionPortLabel(SpaceOpeningPort port)
    {
        if (port == null)
        {
            return "Missing port";
        }

        string ownerId = port.OwningBlock != null ? port.OwningBlock.spaceBlockId : "NoBlock";
        string occupancy = port.isOccupied || port.connectedPort != null ? "Occupied" : "Free";
        return $"{ownerId} | {port.openingId} | {port.wallSide} ({port.gridPosition.x}, {port.gridPosition.y}) | {occupancy}";
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

        if (!CanAttachOverlayToWall(metadata, selectedGridSegmentDefinition, out string reason))
        {
            ShowSummary(reason);
            return;
        }

        AttachOverlayToPlacement(block, metadata, selectedGridSegmentDefinition);
        ShowSummary($"Attached overlay {selectedGridSegmentDefinition.segmentId}.");
    }

    private void AttachOverlayToPlacement(
        MemorySpaceBlock block,
        SpaceSegmentPlacementMetadata metadata,
        SpaceSegmentDefinition overlayDefinition)
    {
        if (block == null || metadata == null || overlayDefinition == null)
        {
            return;
        }

        WallSegmentSlot wallSlot = metadata.GetComponent<WallSegmentSlot>();
        if (wallSlot == null)
        {
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(metadata.gameObject, "Place Opening Overlay");
        wallSlot.SetOverlay(overlayDefinition);
        metadata.record.overlaySegmentId = overlayDefinition.segmentId;
        metadata.overlayDefinition = overlayDefinition;
        metadata.record.isConnectorCandidate = markConnectorCandidate || IsDoorwayDefinition(overlayDefinition);
        if (IsDoorwayDefinition(overlayDefinition))
        {
            CreateBakedOpeningPort(block, wallSlot, metadata.record, overlayDefinition);
        }

        EditorUtility.SetDirty(metadata);
        EditorUtility.SetDirty(block);
    }

    private bool CanAttachOverlayToWall(
        SpaceSegmentPlacementMetadata wallPlacement,
        SpaceSegmentDefinition overlayDefinition,
        out string reason)
    {
        reason = string.Empty;
        if (wallPlacement == null || wallPlacement.record == null || overlayDefinition == null)
        {
            reason = "Overlay target is missing.";
            return false;
        }

        if (wallPlacement.record.category != SegmentCategory.Wall)
        {
            reason = "Opening overlays can only be attached to wall placements.";
            return false;
        }

        float overlayWidth = GetDefinitionDisplayWidth(overlayDefinition);
        float wallWidth = Mathf.Max(1f, wallPlacement.record.footprint.x);

        if (overlayWidth - wallWidth > 0.001f)
        {
            reason = $"Overlay width {FormatDefinitionNumber(overlayWidth)} exceeds wall width {FormatDefinitionNumber(wallWidth)}.";
            return false;
        }

        return true;
    }

    private void CreateBakedOpeningPort(
        MemorySpaceBlock block,
        WallSegmentSlot wallSlot,
        SpaceSegmentPlacementRecord record,
        SpaceSegmentDefinition overlayDefinition)
    {
        if (block == null
            || wallSlot == null
            || record == null
            || overlayDefinition == null
            || wallSlot.overlayRoot == null
            || wallSlot.overlayRoot.childCount == 0)
        {
            return;
        }

        Transform doorwayTransform = wallSlot.overlayRoot.GetChild(0);
        if (doorwayTransform == null)
        {
            return;
        }

        SpaceOpeningPort port = doorwayTransform.GetComponent<SpaceOpeningPort>();
        if (port == null)
        {
            port = doorwayTransform.gameObject.AddComponent<SpaceOpeningPort>();
        }

        port.openingId = $"{block.spaceBlockId}_{record.placementId}_{overlayDefinition.segmentId}";
        port.openingType = SpaceOpeningType.Doorway;
        port.connectionKind = SpaceConnectionKind.Passage;
        port.widthUnits = Mathf.Max(1, Mathf.RoundToInt(GetDefinitionDisplayWidth(overlayDefinition)));
        port.height = Mathf.Max(1f, GetDefinitionDisplayHeight(overlayDefinition));
        port.wallSide = record.side;
        port.gridPosition = new Vector2Int(record.gridX, record.gridZ);
        port.isOccupied = false;
        port.connectedPort = null;

        Transform connectorAnchor = doorwayTransform.Find("ConnectorAnchor");
        if (connectorAnchor == null)
        {
            GameObject anchorObject = new GameObject("ConnectorAnchor");
            connectorAnchor = anchorObject.transform;
            connectorAnchor.SetParent(doorwayTransform, false);
        }

        port.connectorAnchor = connectorAnchor;
        ConfigureDoorwayConnectorAnchor(block, doorwayTransform, connectorAnchor, record.side, port.height);
    }

    private void CopyDoorwayPlacementState(MemorySpaceBlock sourceBlock, MemorySpaceBlock bakedBlock)
    {
        if (sourceBlock == null || bakedBlock == null)
        {
            return;
        }

        SpaceSegmentPlacementMetadata[] sourcePlacements = sourceBlock.GetComponentsInChildren<SpaceSegmentPlacementMetadata>(true);
        for (int i = 0; i < sourcePlacements.Length; i++)
        {
            SpaceSegmentPlacementMetadata sourcePlacement = sourcePlacements[i];
            if (sourcePlacement == null
                || sourcePlacement.record == null
                || string.IsNullOrWhiteSpace(sourcePlacement.record.overlaySegmentId)
                || sourcePlacement.overlayDefinition == null
                || !IsDoorwayDefinition(sourcePlacement.overlayDefinition))
            {
                continue;
            }

            WallSegmentSlot sourceSlot = sourcePlacement.GetComponent<WallSegmentSlot>();
            if (sourceSlot == null || sourceSlot.overlayRoot == null || sourceSlot.overlayRoot.childCount == 0)
            {
                continue;
            }

            Transform bakedPlacementTransform = FindDescendantByName(bakedBlock.transform, sourcePlacement.record.placementId);
            if (bakedPlacementTransform == null)
            {
                continue;
            }

            WallSegmentSlot bakedSlot = bakedPlacementTransform.GetComponent<WallSegmentSlot>();
            if (bakedSlot == null || bakedSlot.overlayRoot == null || bakedSlot.overlayRoot.childCount == 0)
            {
                continue;
            }

            Transform sourceDoorway = sourceSlot.overlayRoot.GetChild(0);
            Transform bakedDoorway = bakedSlot.overlayRoot.GetChild(0);
            bakedDoorway.localPosition = sourceDoorway.localPosition;
            bakedDoorway.localRotation = sourceDoorway.localRotation;
            bakedDoorway.localScale = sourceDoorway.localScale;
            bakedSlot.SetOverlayTransformOverride(sourceDoorway);

            SpaceOpeningPort sourcePort = sourceDoorway.GetComponent<SpaceOpeningPort>();
            SpaceOpeningPort bakedPort = bakedDoorway.GetComponent<SpaceOpeningPort>();
            if (sourcePort != null && bakedPort != null && sourcePort.connectorAnchor != null && bakedPort.connectorAnchor != null)
            {
                bakedPort.connectorAnchor.localPosition = sourcePort.connectorAnchor.localPosition;
                bakedPort.connectorAnchor.localRotation = sourcePort.connectorAnchor.localRotation;
                bakedPort.connectorAnchor.localScale = sourcePort.connectorAnchor.localScale;
            }

            ConfigureDoorwayConnectorAnchor(
                bakedBlock,
                bakedDoorway,
                bakedPort != null ? bakedPort.connectorAnchor : null,
                sourcePlacement.record.side,
                bakedPort != null ? bakedPort.height : GetDefinitionDisplayHeight(sourcePlacement.overlayDefinition));
        }
    }

    private static void ConfigureDoorwayConnectorAnchor(
        MemorySpaceBlock block,
        Transform doorwayTransform,
        Transform connectorAnchor,
        WallSide wallSide,
        float openingHeight)
    {
        if (doorwayTransform == null || connectorAnchor == null)
        {
            return;
        }

        Vector3 up = block != null ? block.transform.up : Vector3.up;
        Vector3 forward = -GetWallForward(block != null ? block.transform : doorwayTransform, wallSide);
        Vector3 floorOrigin = block != null ? block.transform.position : doorwayTransform.position;
        Vector3 doorwayOffset = doorwayTransform.position - floorOrigin;
        Vector3 doorwayOnFloor = doorwayTransform.position - (up * Vector3.Dot(doorwayOffset, up));
        Vector3 worldCenter = doorwayOnFloor + (up * (openingHeight * 0.5f));

        connectorAnchor.position = worldCenter;
        connectorAnchor.rotation = Quaternion.LookRotation(forward, up);
        connectorAnchor.localScale = Vector3.one;
    }

    private static Vector3 GetWallForward(Transform referenceRoot, WallSide wallSide)
    {
        switch (wallSide)
        {
            case WallSide.East:
                return referenceRoot != null ? referenceRoot.right : Vector3.right;
            case WallSide.South:
                return referenceRoot != null ? -referenceRoot.forward : Vector3.back;
            case WallSide.West:
                return referenceRoot != null ? -referenceRoot.right : Vector3.left;
            default:
                return referenceRoot != null ? referenceRoot.forward : Vector3.forward;
        }
    }

    private static bool TryGetWorldRendererBounds(Transform root, out Bounds worldBounds)
    {
        worldBounds = default;
        if (root == null)
        {
            return false;
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
        {
            return false;
        }

        worldBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            worldBounds.Encapsulate(renderers[i].bounds);
        }

        return true;
    }

    private static Transform FindDescendantByName(Transform root, string targetName)
    {
        if (root == null || string.IsNullOrWhiteSpace(targetName))
        {
            return null;
        }

        Transform[] descendants = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < descendants.Length; i++)
        {
            if (string.Equals(descendants[i].name, targetName, StringComparison.OrdinalIgnoreCase))
            {
                return descendants[i];
            }
        }

        return null;
    }

    private string GetBakePrefabFileName(MemorySpaceBlock block)
    {
        string requestedName = string.IsNullOrWhiteSpace(bakePrefabName)
            ? string.Empty
            : bakePrefabName.Trim();
        if (!string.IsNullOrWhiteSpace(requestedName))
        {
            return $"PF_SB_{requestedName}";
        }

        if (selectedBlockDefinitionAsset != null && !string.IsNullOrWhiteSpace(selectedBlockDefinitionAsset.name))
        {
            return $"PF_SB_{selectedBlockDefinitionAsset.name}";
        }

        string blockId = block != null && !string.IsNullOrWhiteSpace(block.spaceBlockId)
            ? block.spaceBlockId
            : (block != null ? block.name : "SpaceBlock");
        return $"PF_SB_{blockId}";
    }

    private SpaceSegmentPlacementRecord BuildPlacementRecord(SpaceSegmentDefinition definition)
    {
        int normalizedRotation = NormalizeRotation(placeRotationY);
        return new SpaceSegmentPlacementRecord
        {
            placementId = $"PL_{Guid.NewGuid():N}".Substring(0, 11),
            segmentId = definition.segmentId,
            category = GetSupportedPlacementCategory(definition.category),
            gridX = placeGridX,
            gridZ = placeGridZ,
            side = placeWallSide,
            rotationY = normalizedRotation,
            footprint = GetPlacementFootprint(definition, normalizedRotation),
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

        float width = GetDefinitionDisplayWidth(definition);
        float height = GetDefinitionDisplayHeight(definition);

        switch (definition.category)
        {
            case SegmentCategory.Floor:
            case SegmentCategory.Ceiling:
            case SegmentCategory.Threshold:
                return new Vector2Int(
                    Mathf.Max(1, Mathf.RoundToInt(width)),
                    Mathf.Max(1, Mathf.RoundToInt(height)));
            default:
                return new Vector2Int(Mathf.Max(1, Mathf.RoundToInt(width)), 1);
        }
    }

    private static Vector2Int GetPlacementFootprint(SpaceSegmentDefinition definition, int rotationY)
    {
        Vector2Int baseFootprint = GetDefinitionFootprint(definition);
        if (definition == null)
        {
            return baseFootprint;
        }

        int normalizedRotation = NormalizeRotation(rotationY);
        bool swapAxes = normalizedRotation == 90 || normalizedRotation == 270;
        switch (definition.category)
        {
            case SegmentCategory.Floor:
            case SegmentCategory.Ceiling:
            case SegmentCategory.Threshold:
            case SegmentCategory.Beam:
                return swapAxes
                    ? new Vector2Int(baseFootprint.y, baseFootprint.x)
                    : baseFootprint;
            default:
                return baseFootprint;
        }
    }

    private bool TryBuildRecordFromScenePoint(
        Vector3 localPoint,
        SpaceSegmentDefinition definition,
        out SpaceSegmentPlacementRecord record,
        out string message)
    {
        record = default;
        message = string.Empty;
        if (definition == null)
        {
            message = "Select a segment definition first.";
            return false;
        }

        float halfWidth = gridWidth * gridSize * 0.5f;
        float halfDepth = gridDepth * gridSize * 0.5f;
        float normalizedX = (localPoint.x + halfWidth) / gridSize;
        float normalizedZ = (localPoint.z + halfDepth) / gridSize;
        int cellX = Mathf.FloorToInt(normalizedX);
        int cellZ = Mathf.FloorToInt(normalizedZ);

        if (cellX < 0 || cellZ < 0 || cellX >= gridWidth || cellZ >= gridDepth)
        {
            message = "Cursor is outside the authoring grid.";
            return false;
        }

        int normalizedRotation = NormalizeRotation(placeRotationY);
        record = new SpaceSegmentPlacementRecord
        {
            placementId = $"PL_{Guid.NewGuid():N}".Substring(0, 11),
            segmentId = definition.segmentId,
            category = GetSupportedPlacementCategory(definition.category),
            gridX = cellX,
            gridZ = cellZ,
            side = placeWallSide,
            rotationY = normalizedRotation,
            footprint = GetPlacementFootprint(definition, normalizedRotation),
            overlaySegmentId = string.Empty,
            isConnectorCandidate = markConnectorCandidate
        };

        if (record.category == SegmentCategory.Wall)
        {
            record.side = sceneAutoPickWallSide
                ? GetNearestWallSide(localPoint, cellX, cellZ)
                : placeWallSide;

            int wallLength = Mathf.Max(1, record.footprint.x);
            switch (record.side)
            {
                case WallSide.North:
                case WallSide.South:
                    record.gridX = Mathf.Clamp(cellX, 0, Mathf.Max(0, gridWidth - wallLength));
                    record.gridZ = Mathf.Clamp(cellZ, 0, gridDepth - 1);
                    break;
                case WallSide.East:
                case WallSide.West:
                    record.gridX = Mathf.Clamp(cellX, 0, gridWidth - 1);
                    record.gridZ = Mathf.Clamp(cellZ, 0, Mathf.Max(0, gridDepth - wallLength));
                    break;
            }

            return true;
        }

        record.gridX = Mathf.Clamp(cellX, 0, Mathf.Max(0, gridWidth - Mathf.Max(1, record.footprint.x)));
        record.gridZ = Mathf.Clamp(cellZ, 0, Mathf.Max(0, gridDepth - Mathf.Max(1, record.footprint.y)));
        return true;
    }

    private WallSide GetNearestWallSide(Vector3 localPoint, int cellX, int cellZ)
    {
        float halfWidth = gridWidth * gridSize * 0.5f;
        float halfDepth = gridDepth * gridSize * 0.5f;
        float minX = -halfWidth + (cellX * gridSize);
        float maxX = minX + gridSize;
        float minZ = -halfDepth + (cellZ * gridSize);
        float maxZ = minZ + gridSize;

        float westDistance = Mathf.Abs(localPoint.x - minX);
        float eastDistance = Mathf.Abs(localPoint.x - maxX);
        float southDistance = Mathf.Abs(localPoint.z - minZ);
        float northDistance = Mathf.Abs(localPoint.z - maxZ);

        float bestDistance = northDistance;
        WallSide side = WallSide.North;

        if (southDistance < bestDistance)
        {
            bestDistance = southDistance;
            side = WallSide.South;
        }

        if (eastDistance < bestDistance)
        {
            bestDistance = eastDistance;
            side = WallSide.East;
        }

        if (westDistance < bestDistance)
        {
            side = WallSide.West;
        }

        return side;
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
        List<string> aKeys = GetWallEdgeKeys(a);
        List<string> bKeys = GetWallEdgeKeys(b);
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
                CreateWallPlacement(placementRoot, record, definition, kit, metadata, editable);
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
        SpaceSegmentPlacementMetadata metadata,
        bool editable)
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

                if (IsDoorwayDefinition(overlayDefinition))
                {
                    CreateBakedOpeningPort(
                        placementRoot.GetComponentInParent<MemorySpaceBlock>(),
                        slot,
                        record,
                        overlayDefinition);
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
        Quaternion authoringRotation = GetSegmentAuthoringPlacementRotation(definition);
        Vector3 authoringScale = GetSegmentAuthoringPlacementScale(definition);
        instanceTransform.localScale = authoringScale;

        switch (record.category)
        {
            case SegmentCategory.Floor:
                instanceTransform.localRotation = Quaternion.Euler(-90f, record.rotationY, 0f) * authoringRotation;
                AlignInstanceToLocalBounds(instanceTransform, 0f, 0f, 0f);
                break;
            case SegmentCategory.Ceiling:
                instanceTransform.localRotation = Quaternion.Euler(90f, record.rotationY, 0f) * authoringRotation;
                AlignInstanceToBoundsAtHeight(instanceTransform, 0f, GetAuthoringCeilingHeight(), 0f);
                break;
            case SegmentCategory.Threshold:
                instanceTransform.localRotation = Quaternion.Euler(0f, record.rotationY, 0f) * authoringRotation;
                AlignInstanceToLocalBounds(instanceTransform, 0f, 0f, 0f);
                break;
            case SegmentCategory.Beam:
                instanceTransform.localRotation = Quaternion.Euler(0f, record.rotationY, 0f) * authoringRotation;
                AlignInstanceToBoundsAtHeight(instanceTransform, 0f, GetAuthoringCeilingHeight(), 0f);
                break;
            default:
                instanceTransform.localPosition = Vector3.zero;
                instanceTransform.localRotation = authoringRotation;
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

    private static Quaternion GetSegmentAuthoringPlacementRotation(SpaceSegmentDefinition definition)
    {
        if (definition == null || !definition.hasPlacementAuthoringOverride)
        {
            return Quaternion.identity;
        }

        return Quaternion.Euler(definition.placementAuthoringEulerAngles);
    }

    private static Vector3 GetSegmentAuthoringPlacementScale(SpaceSegmentDefinition definition)
    {
        if (definition == null || !definition.hasPlacementAuthoringOverride)
        {
            return Vector3.one;
        }

        Vector3 scale = definition.placementAuthoringScale;
        return new Vector3(
            Mathf.Approximately(scale.x, 0f) ? 1f : scale.x,
            Mathf.Approximately(scale.y, 0f) ? 1f : scale.y,
            Mathf.Approximately(scale.z, 0f) ? 1f : scale.z);
    }

    private Vector3 GetScenePreviewSize(SpaceSegmentPlacementRecord record)
    {
        switch (record.category)
        {
            case SegmentCategory.Wall:
            {
                float length = Mathf.Max(1, record.footprint.x) * gridSize;
                bool horizontal = record.side == WallSide.North || record.side == WallSide.South;
                return horizontal
                    ? new Vector3(length, Mathf.Max(0.2f, GetAuthoringCeilingHeight()), 0.08f)
                    : new Vector3(0.08f, Mathf.Max(0.2f, GetAuthoringCeilingHeight()), length);
            }
            case SegmentCategory.Ceiling:
            case SegmentCategory.Beam:
                return new Vector3(
                    Mathf.Max(1, record.footprint.x) * gridSize,
                    0.08f,
                    Mathf.Max(1, record.footprint.y) * gridSize);
            default:
                return new Vector3(
                    Mathf.Max(1, record.footprint.x) * gridSize,
                    0.04f,
                    Mathf.Max(1, record.footprint.y) * gridSize);
        }
    }

    private Vector3 GetOverlayPreviewSize(SpaceSegmentPlacementRecord wallRecord, SpaceSegmentDefinition overlayDefinition)
    {
        float width = Mathf.Max(1f, GetDefinitionDisplayWidth(overlayDefinition)) * gridSize;
        float height = Mathf.Max(0.2f, GetDefinitionDisplayHeight(overlayDefinition));
        bool horizontal = wallRecord != null && (wallRecord.side == WallSide.North || wallRecord.side == WallSide.South);
        return horizontal
            ? new Vector3(width, height, 0.08f)
            : new Vector3(0.08f, height, width);
    }

    private Vector3 GetScenePreviewLocalCenter(SpaceSegmentPlacementRecord record, SpaceSegmentDefinition definition)
    {
        Vector3 center = GetPlacementLocalPosition(record, definition);
        switch (record.category)
        {
            case SegmentCategory.Wall:
                center.y = GetAuthoringCeilingHeight() * 0.5f;
                break;
            case SegmentCategory.Ceiling:
            case SegmentCategory.Beam:
                center.y = GetAuthoringCeilingHeight();
                break;
            default:
                center.y = 0.02f;
                break;
        }

        return center;
    }

    private Vector3 GetOverlayPreviewLocalCenter(
        MemorySpaceBlock block,
        SpaceSegmentPlacementMetadata metadata,
        SpaceSegmentDefinition overlayDefinition)
    {
        Vector3 center = block != null
            ? block.transform.InverseTransformPoint(metadata.transform.position)
            : metadata.transform.localPosition;
        center += GetWallOverlayPlacementOffset(metadata != null ? metadata.record : null, overlayDefinition);
        center.y = GetDefinitionDisplayHeight(overlayDefinition) * 0.5f;
        return center;
    }

    private Vector3 GetWallOverlayPlacementOffset(
        SpaceSegmentPlacementRecord wallRecord,
        SpaceSegmentDefinition overlayDefinition)
    {
        if (wallRecord == null || overlayDefinition == null)
        {
            return Vector3.zero;
        }

        float wallWidth = Mathf.Max(1f, wallRecord.footprint.x) * gridSize;
        float overlayWidth = Mathf.Max(1f, GetDefinitionDisplayWidth(overlayDefinition)) * gridSize;
        float lateralOffset = (overlayWidth - wallWidth) * 0.5f;

        switch (wallRecord.side)
        {
            case WallSide.North:
            case WallSide.South:
                return new Vector3(lateralOffset, 0f, 0f);
            case WallSide.East:
            case WallSide.West:
                return new Vector3(0f, 0f, lateralOffset);
            default:
                return Vector3.zero;
        }
    }

    private bool TryRaycastAuthoringPlane(Transform blockRoot, Vector2 mousePosition, out Vector3 localPoint)
    {
        localPoint = Vector3.zero;
        if (blockRoot == null)
        {
            return false;
        }

        Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);
        Plane plane = new Plane(blockRoot.up, blockRoot.position);
        if (!plane.Raycast(ray, out float distance))
        {
            return false;
        }

        Vector3 worldPoint = ray.GetPoint(distance);
        localPoint = blockRoot.InverseTransformPoint(worldPoint);
        return true;
    }

    private bool IsLocalPointWithinGrid(Vector3 localPoint)
    {
        float halfWidth = gridWidth * gridSize * 0.5f;
        float halfDepth = gridDepth * gridSize * 0.5f;
        return localPoint.x >= -halfWidth
            && localPoint.x <= halfWidth
            && localPoint.z >= -halfDepth
            && localPoint.z <= halfDepth;
    }

    private bool TryFindNearestPlacementMetadata(
        MemorySpaceBlock block,
        Vector3 localPoint,
        bool wallOnly,
        out SpaceSegmentPlacementMetadata metadata)
    {
        metadata = null;
        if (block == null)
        {
            return false;
        }

        SpaceSegmentPlacementMetadata[] placements = block.GetComponentsInChildren<SpaceSegmentPlacementMetadata>(true);
        float bestDistanceSqr = float.MaxValue;
        float maxDistance = gridSize * 0.75f;
        float maxDistanceSqr = maxDistance * maxDistance;

        for (int i = 0; i < placements.Length; i++)
        {
            SpaceSegmentPlacementMetadata candidate = placements[i];
            if (candidate == null || candidate.record == null)
            {
                continue;
            }

            if (wallOnly && candidate.record.category != SegmentCategory.Wall)
            {
                continue;
            }

            Vector3 candidateLocalCenter;
            Vector3 candidateLocalSize;
            if (!TryGetPlacementPreviewBounds(candidate.gameObject, out candidateLocalCenter, out candidateLocalSize))
            {
                candidateLocalCenter = block.transform.InverseTransformPoint(candidate.transform.position);
                candidateLocalSize = Vector3.one * gridSize;
            }

            Vector2 localPointXZ = new Vector2(localPoint.x, localPoint.z);
            Vector2 candidateXZ = new Vector2(candidateLocalCenter.x, candidateLocalCenter.z);
            Vector2 halfSizeXZ = new Vector2(candidateLocalSize.x * 0.5f, candidateLocalSize.z * 0.5f);

            float clampedX = Mathf.Clamp(localPointXZ.x, candidateXZ.x - halfSizeXZ.x, candidateXZ.x + halfSizeXZ.x);
            float clampedZ = Mathf.Clamp(localPointXZ.y, candidateXZ.y - halfSizeXZ.y, candidateXZ.y + halfSizeXZ.y);
            float distanceSqr = (localPointXZ - new Vector2(clampedX, clampedZ)).sqrMagnitude;
            if (distanceSqr > maxDistanceSqr || distanceSqr >= bestDistanceSqr)
            {
                continue;
            }

            bestDistanceSqr = distanceSqr;
            metadata = candidate;
        }

        return metadata != null;
    }

    private bool TryGetPlacementPreviewBounds(GameObject placementObject, out Vector3 localCenter, out Vector3 localSize)
    {
        localCenter = Vector3.zero;
        localSize = Vector3.one * gridSize;
        if (placementObject == null)
        {
            return false;
        }

        Renderer[] renderers = placementObject.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
        {
            return false;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        MemorySpaceBlock block = placementObject.GetComponentInParent<MemorySpaceBlock>();
        if (block == null)
        {
            return false;
        }

        localCenter = block.transform.InverseTransformPoint(bounds.center);
        Vector3 localSizeVector = block.transform.InverseTransformVector(bounds.size);
        localSize = new Vector3(
            Mathf.Max(0.08f, Mathf.Abs(localSizeVector.x)),
            Mathf.Max(0.08f, Mathf.Abs(localSizeVector.y)),
            Mathf.Max(0.08f, Mathf.Abs(localSizeVector.z)));
        return true;
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
        definition.gridWidth = Mathf.Max(1, block.widthUnits);
        definition.gridDepth = Mathf.Max(1, block.depthUnits);
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
        List<string> wallEdgeKeys = GetWallEdgeKeys(record);
        for (int i = 0; i < wallEdgeKeys.Count; i++)
        {
            string key = wallEdgeKeys[i];
            if (!occupiedEdges.Add(key))
            {
                messages.Add($"Overlapping wall edge at {key}.");
            }
        }
    }

    private static List<string> GetWallEdgeKeys(SpaceSegmentPlacementRecord record)
    {
        List<string> keys = new List<string>();
        if (record == null)
        {
            return keys;
        }

        int segmentLength = Mathf.Max(1, record.footprint.x);
        for (int step = 0; step < segmentLength; step++)
        {
            switch (record.side)
            {
                case WallSide.North:
                    keys.Add($"H:{record.gridX + step}:{record.gridZ + 1}");
                    break;
                case WallSide.South:
                    keys.Add($"H:{record.gridX + step}:{record.gridZ}");
                    break;
                case WallSide.East:
                    keys.Add($"V:{record.gridX + 1}:{record.gridZ + step}");
                    break;
                case WallSide.West:
                    keys.Add($"V:{record.gridX}:{record.gridZ + step}");
                    break;
            }
        }

        return keys;
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
                instance.transform.localScale = GetSegmentAuthoringPlacementScale(floorDefinition);
                instance.transform.localRotation =
                    Quaternion.Euler(-90f, 0f, 0f) * GetSegmentAuthoringPlacementRotation(floorDefinition);
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
