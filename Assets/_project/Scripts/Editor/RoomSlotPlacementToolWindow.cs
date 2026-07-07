#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public class RoomSlotPlacementToolWindow : EditorWindow
{
    private const string WindowTitle = "Room Slot Placement Tool";
    private const string MenuPath = "Memory Garden/Space/Room Slot Placement Tool";
    private const string SlotRootName = "SlotRoot";
    private const string DefaultFurniturePrefabFolder = "Assets/_project/Prefabs/DisplayFurniture";
    private const float PreviewThickness = 0.08f;
    private const float PreviewFloorHeight = 0.04f;
    private const float PreviewWallHeight = 1f;
    private const float PaletteButtonWidth = 92f;
    private const float PaletteButtonHeight = 98f;
    private const float FullWallHeight = 2.5f;
    private const float HalfWallHeight = 1.25f;
    private const float WallHeightTolerance = 0.1f;
    private const int FullWallLayerCount = 3;
    private const int HalfWallLayerCount = 1;
    private const bool EnableWallPlacementDebugLogs = true;
    private static readonly Dictionary<string, DisplayFurnitureBuildProfile> SlotPrefabPlacementProfileCache =
        new Dictionary<string, DisplayFurnitureBuildProfile>(StringComparer.OrdinalIgnoreCase);

    private enum PreviewMode
    {
        None,
        Floor,
        Wall
    }

    private sealed class ValidationResult
    {
        public readonly List<string> Infos = new List<string>();
        public readonly List<string> Warnings = new List<string>();
        public readonly List<string> Errors = new List<string>();

        public bool HasErrors => Errors.Count > 0;
        public bool HasWarnings => Warnings.Count > 0;

        public void AddInfo(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                Infos.Add(message);
            }
        }

        public void AddWarning(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                Warnings.Add(message);
            }
        }

        public void AddError(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                Errors.Add(message);
            }
        }

        public void Merge(ValidationResult other)
        {
            if (other == null)
            {
                return;
            }

            Infos.AddRange(other.Infos);
            Warnings.AddRange(other.Warnings);
            Errors.AddRange(other.Errors);
        }
    }

    private sealed class EditContext : IDisposable
    {
        public Object SourceObject;
        public MemorySpaceBlock WorkingBlock;
        public GameObject LoadedPrefabRoot;
        public string AssetPath;
        public bool IsPrefabAssetEdit;

        public void Dispose()
        {
            if (LoadedPrefabRoot != null)
            {
                PrefabUtility.UnloadPrefabContents(LoadedPrefabRoot);
                LoadedPrefabRoot = null;
            }
        }
    }

    private struct PlacementCandidate
    {
        public RoomSlotSurfaceType SurfaceType;
        public int GridX;
        public int GridZ;
        public int WidthUnits;
        public int DepthUnits;
        public int FloorGridXHalf;
        public int FloorGridZHalf;
        public int FloorWidthHalf;
        public int FloorDepthHalf;
        public float RotationY;
        public WallSide WallSide;
        public int WallGridPosition;
        public float HeightOffset;
        public int WallLayerIndex;
        public int WallLayerCount;
        public float WallSurfaceHeight;
    }

    private struct ScenePlacementPreview
    {
        public bool HasPreview;
        public bool CanPlace;
        public bool IsDeletePreview;
        public PlacementCandidate Candidate;
        public List<RoomSlotPlacementMetadata> Overlaps;
        public Vector3 LocalCenter;
        public Vector3 LocalSize;
        public Quaternion LocalRotation;
        public string Message;
        public RoomSlotPlacementMetadata TargetPlacement;
        public bool ShowWallLayerGuide;
        public int WallLayerCount;
        public int ActiveWallLayerIndex;
        public Vector3 WallGuideLocalCenter;
        public Vector3 WallGuideLocalSize;
        public WallSide WallGuideSide;
    }

    private sealed class WallSurfaceContext
    {
        public SpaceSegmentPlacementMetadata PlacementMetadata;
        public SpaceSegmentDefinition Definition;
        public WallSegmentSlot WallSlot;
        public WallSide Side;
        public int WallGridPosition;
        public int WidthUnits;
        public Vector3 LocalHitPoint;
        public float SurfaceHeight;
        public int LayerCount;
    }

    private sealed class SlotPrefabPaletteEntry
    {
        public GameObject Prefab;
        public string AssetPath;
        public string Label;
    }

    [SerializeField] private Object targetObject;
    [SerializeField] private GameObject slotPrefab;
    [SerializeField] private int floorGridX;
    [SerializeField] private int floorGridZ;
    [SerializeField] private int floorWidthUnits = 1;
    [SerializeField] private int floorDepthUnits = 1;
    [SerializeField] private float floorRotationY;
    [SerializeField] private WallSide wallSide = WallSide.North;
    [SerializeField] private int wallGridPosition;
    [SerializeField] private int wallWidthUnits = 1;
    [SerializeField] private float wallRotationY;
    [SerializeField] private bool resizeHitboxToGridFootprint;
    [SerializeField] private bool replaceOverlappingSlots;
    [SerializeField] private bool scenePlacementEnabled = true;
    [SerializeField] private bool sceneDeleteMode;
    [SerializeField] private bool hideCeilingsWhilePainting;

    private PreviewMode previewMode;
    private Vector2 scrollPosition;
    private Vector2 prefabPaletteScrollPosition;
    private string statusMessage = "Select a MemorySpaceBlock and a slot prefab to begin.";
    private MessageType statusType = MessageType.Info;
    private bool deferredRepaintQueued;
    private bool deferredSceneRepaintQueued;
    private GUIStyle prefabPaletteButtonStyle;
    private readonly List<string> infoMessages = new List<string>();
    private readonly List<string> warningMessages = new List<string>();
    private readonly List<string> errorMessages = new List<string>();
    private readonly List<SlotPrefabPaletteEntry> slotPrefabPaletteEntries = new List<SlotPrefabPaletteEntry>();
    private readonly List<GameObject> temporarilyHiddenCeilingObjects = new List<GameObject>();
    private readonly HashSet<int> preHiddenCeilingObjectIds = new HashSet<int>();
    private MemorySpaceBlock temporarilyHiddenCeilingBlock;
    private int hoveredWallLayerIndex;
    private int hoveredWallLayerCount = 1;
    private float hoveredWallSurfaceHeight;
    private WallSurfaceContext cachedHoveredWallSurfaceContext;
    private bool hasCachedHoveredWallSurfaceContext;
    private string lastWallDebugLogKey = string.Empty;
    private double lastWallDebugLogTime;

    [MenuItem(MenuPath)]
    private static void OpenWindow()
    {
        RoomSlotPlacementToolWindow window = GetWindow<RoomSlotPlacementToolWindow>(WindowTitle);
        window.minSize = new Vector2(420f, 500f);
        window.Show();
    }

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        TryAutoAssignTargetFromSelection();
        RebuildSlotPrefabPalette();
    }

    private void OnDisable()
    {
        RestoreTemporaryCeilingVisibility();
        ClearCachedWallSurfaceContext();
        SceneView.duringSceneGui -= OnSceneGUI;
    }

    private void OnProjectChange()
    {
        SlotPrefabPlacementProfileCache.Clear();
        RepaintPreviewIfNeeded();
    }

    private void OnSelectionChange()
    {
        if (scenePlacementEnabled && previewMode != PreviewMode.None)
        {
            return;
        }

        TryAutoAssignTargetFromSelection();
        Repaint();
    }

    private void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        EditorGUILayout.Space();

        DrawTargetSection();
        EditorGUILayout.Space();
        DrawPrefabSection();
        EditorGUILayout.Space();
        DrawScenePlacementSection();
        EditorGUILayout.Space();
        DrawFloorSection();
        EditorGUILayout.Space();
        DrawWallSection();
        EditorGUILayout.Space();
        DrawHitboxSection();
        EditorGUILayout.Space();
        DrawStatusSection();

        EditorGUILayout.EndScrollView();
    }

    private void DrawTargetSection()
    {
        EditorGUILayout.LabelField("Target", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        targetObject = EditorGUILayout.ObjectField("MemorySpaceBlock", targetObject, typeof(Object), true);
        if (EditorGUI.EndChangeCheck())
        {
            RepaintPreviewIfNeeded();
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Use Current Selection"))
            {
                if (!TryAutoAssignTargetFromSelection())
                {
                    SetStatus("Selection does not contain a MemorySpaceBlock.", MessageType.Warning);
                }
            }

            if (GUILayout.Button("Clear Target"))
            {
                targetObject = null;
                RepaintPreviewIfNeeded();
            }
        }

        if (!TryResolveTargetBlock(targetObject, out MemorySpaceBlock targetBlock, out string targetError))
        {
            EditorGUILayout.HelpBox(targetError, MessageType.Warning);
            return;
        }

        EditorGUILayout.LabelField("Block Type Id", RoomSlotGridUtility.GetBlockTypeId(targetBlock));
        EditorGUILayout.LabelField("Block Instance Id", RoomSlotGridUtility.GetBlockInstanceId(targetBlock));
        EditorGUILayout.LabelField("Grid Width", RoomSlotGridUtility.GetGridWidth(targetBlock).ToString());
        EditorGUILayout.LabelField("Grid Depth", RoomSlotGridUtility.GetGridDepth(targetBlock).ToString());
        EditorGUILayout.LabelField("Grid Size", RoomSlotGridUtility.GetGridSize(targetBlock).ToString("0.###"));

        if (EditorUtility.IsPersistent(targetBlock.gameObject))
        {
            EditorGUILayout.HelpBox(
                "Preview is not drawn for direct prefab-asset targets. Open the prefab in Prefab Mode if you want SceneView preview while authoring.",
                MessageType.Info);
        }
    }

    private void DrawPrefabSection()
    {
        EditorGUILayout.LabelField("Slot Prefab", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        slotPrefab = (GameObject)EditorGUILayout.ObjectField("Prefab", slotPrefab, typeof(GameObject), false);
        if (EditorGUI.EndChangeCheck())
        {
            SlotPrefabPlacementProfileCache.Clear();
            RepaintPreviewIfNeeded();
        }

        ValidationResult prefabValidation = ValidateSlotPrefab(slotPrefab);
        foreach (string info in prefabValidation.Infos)
        {
            EditorGUILayout.HelpBox(info, MessageType.Info);
        }

        foreach (string warning in prefabValidation.Warnings)
        {
            EditorGUILayout.HelpBox(warning, MessageType.Warning);
        }

        foreach (string error in prefabValidation.Errors)
        {
            EditorGUILayout.HelpBox(error, MessageType.Error);
        }

        if (slotPrefab != null)
        {
            EditorGUILayout.LabelField("slotPrefabKey", slotPrefab.name);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Refresh Furniture Palette"))
            {
                SlotPrefabPlacementProfileCache.Clear();
                RebuildSlotPrefabPalette();
            }

            using (new EditorGUI.DisabledScope(slotPrefab == null))
            {
                if (GUILayout.Button("Ping Selected"))
                {
                    EditorGUIUtility.PingObject(slotPrefab);
                }
            }
        }

        DrawPrefabPalette();
    }

    private void DrawScenePlacementSection()
    {
        EditorGUILayout.LabelField("Scene Placement", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        scenePlacementEnabled = EditorGUILayout.Toggle("Enable Scene Placement", scenePlacementEnabled);
        sceneDeleteMode = EditorGUILayout.Toggle("Scene Delete Mode", sceneDeleteMode);
        hideCeilingsWhilePainting = EditorGUILayout.Toggle("Temporarily Hide Ceilings While Painting", hideCeilingsWhilePainting);
        if (EditorGUI.EndChangeCheck())
        {
            if (!hideCeilingsWhilePainting || !scenePlacementEnabled || previewMode == PreviewMode.None)
            {
                RestoreTemporaryCeilingVisibility();
            }

            InvalidateScenePreviewCache();
            RepaintPreviewIfNeeded();
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Paint Floor"))
            {
                previewMode = PreviewMode.Floor;
                InvalidateScenePreviewCache();
                QueueDeferredRepaint();
            }

            if (GUILayout.Button("Paint Wall"))
            {
                previewMode = PreviewMode.Wall;
                InvalidateScenePreviewCache();
                QueueDeferredRepaint();
            }

            if (GUILayout.Button("Clear Scene Preview"))
            {
                previewMode = PreviewMode.None;
                InvalidateScenePreviewCache();
                QueueDeferredRepaint();
            }
        }

        string activeModeLabel = "None";
        switch (previewMode)
        {
            case PreviewMode.Floor:
                activeModeLabel = "Floor";
                break;
            case PreviewMode.Wall:
                activeModeLabel = "Wall";
                break;
        }

        EditorGUILayout.HelpBox(
            $"Active Mode: {activeModeLabel}. Slot placement now uses Scene view only: hover the block grid to preview, left-click to place, press R to rotate, and Shift+Click or enable Scene Delete Mode to remove a placed slot.",
            MessageType.None);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Remove Selected Room Slot"))
            {
                RemoveSelectedRoomSlot();
            }

            if (GUILayout.Button("Validate Current Block Slots"))
            {
                ValidateCurrentBlockSlots();
            }
        }
    }

    private void DrawFloorSection()
    {
        EditorGUILayout.LabelField("Floor Settings", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        floorWidthUnits = Mathf.Max(1, EditorGUILayout.IntField("Width Units", floorWidthUnits));
        floorDepthUnits = Mathf.Max(1, EditorGUILayout.IntField("Depth Units", floorDepthUnits));
        floorRotationY = EditorGUILayout.FloatField("Rotation Y", floorRotationY);
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Rotate 90", GUILayout.Width(110f)))
            {
                floorRotationY = RoomSlotGridUtility.NormalizeRotation(floorRotationY + 90f);
                GUI.changed = true;
            }
        }
        if (EditorGUI.EndChangeCheck())
        {
            RepaintPreviewIfNeeded();
        }

        EditorGUILayout.HelpBox(
            "Floor painting now snaps on a half-grid, so you can place furniture on cell centers and interior grid corners with the same tool.",
            MessageType.None);
    }

    private void DrawWallSection()
    {
        EditorGUILayout.LabelField("Wall Settings", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        wallWidthUnits = Mathf.Max(1, EditorGUILayout.IntField("Width Units", wallWidthUnits));
        wallRotationY = EditorGUILayout.FloatField("Rotation Y", wallRotationY);
        EditorGUILayout.LabelField("Hovered Layer", $"{hoveredWallLayerIndex + 1}/{Mathf.Max(1, hoveredWallLayerCount)}");
        EditorGUILayout.LabelField("Hovered Wall Height", hoveredWallSurfaceHeight > 0f ? hoveredWallSurfaceHeight.ToString("0.###") : "-");
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Rotate 90", GUILayout.Width(110f)))
            {
                wallRotationY = RoomSlotGridUtility.NormalizeRotation(wallRotationY + 90f);
                GUI.changed = true;
            }
        }
        if (EditorGUI.EndChangeCheck())
        {
            RepaintPreviewIfNeeded();
        }

        EditorGUILayout.HelpBox(
            "Wall painting now picks the real wall surface under the cursor. Full-height walls expose 3 paintable layers, half-height walls expose only the bottom layer, and window/opening surfaces are blocked.",
            MessageType.None);
    }

    private void DrawHitboxSection()
    {
        EditorGUILayout.LabelField("Hitbox", EditorStyles.boldLabel);
        resizeHitboxToGridFootprint = EditorGUILayout.Toggle("Resize Hitbox To Grid Footprint", resizeHitboxToGridFootprint);
        replaceOverlappingSlots = EditorGUILayout.Toggle("Replace Overlapping Slots", replaceOverlappingSlots);

        EditorGUILayout.HelpBox(
            "If hitbox resize is enabled, the tool will try the furniture placementBoundsCollider first and then fall back to the root BoxCollider. Mesh scale is never changed.",
            MessageType.Info);
    }

    private void DrawStatusSection()
    {
        EditorGUILayout.LabelField("Status", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(statusMessage, statusType);

        if (infoMessages.Count > 0)
        {
            EditorGUILayout.LabelField("Info", EditorStyles.miniBoldLabel);
            for (int i = 0; i < infoMessages.Count; i++)
            {
                EditorGUILayout.HelpBox(infoMessages[i], MessageType.Info);
            }
        }

        if (warningMessages.Count > 0)
        {
            EditorGUILayout.LabelField("Warnings", EditorStyles.miniBoldLabel);
            for (int i = 0; i < warningMessages.Count; i++)
            {
                EditorGUILayout.HelpBox(warningMessages[i], MessageType.Warning);
            }
        }

        if (errorMessages.Count > 0)
        {
            EditorGUILayout.LabelField("Errors", EditorStyles.miniBoldLabel);
            for (int i = 0; i < errorMessages.Count; i++)
            {
                EditorGUILayout.HelpBox(errorMessages[i], MessageType.Error);
            }
        }
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        if (!TryResolveTargetBlock(targetObject, out MemorySpaceBlock targetBlock, out _))
        {
            RestoreTemporaryCeilingVisibility();
            ClearCachedWallSurfaceContext();
            InvalidateScenePreviewCache();
            return;
        }

        if (targetBlock == null || EditorUtility.IsPersistent(targetBlock.gameObject))
        {
            RestoreTemporaryCeilingVisibility();
            ClearCachedWallSurfaceContext();
            InvalidateScenePreviewCache();
            return;
        }

        UpdateTemporaryCeilingVisibility(targetBlock);

        sceneView.wantsMouseMove = scenePlacementEnabled || sceneDeleteMode;
        Event current = Event.current;
        RequestLiveScenePreviewRefresh(current);
        bool isRepaintEvent = current != null && current.type == EventType.Repaint;

        if (previewMode == PreviewMode.Wall
            && scenePlacementEnabled
            && current != null
            && (current.type == EventType.MouseMove || current.type == EventType.MouseDrag || current.type == EventType.MouseDown))
        {
            DebugWallPlacement(
                "OnSceneGUI",
                $"event={current.type} mouse={current.mousePosition} target={(targetBlock != null ? targetBlock.name : "null")} deleteMode={sceneDeleteMode}");
        }

        if (isRepaintEvent && (scenePlacementEnabled || sceneDeleteMode))
        {
            DrawAuthoringGrid(
                targetBlock.transform,
                RoomSlotGridUtility.GetGridWidth(targetBlock),
                RoomSlotGridUtility.GetGridDepth(targetBlock),
                RoomSlotGridUtility.GetGridSize(targetBlock));
        }

        HandleSceneHotkeys(current);

        ScenePlacementPreview preview;
        if (TryBuildDeletePreview(targetBlock, current, out preview))
        {
            if (isRepaintEvent)
            {
                DrawScenePlacementPreview(targetBlock.transform, preview);
            }

            if (TryHandleDeletePreviewClick(current, targetBlock, preview))
            {
                QueueDeferredRepaint();
            }

            return;
        }

        if (scenePlacementEnabled && previewMode != PreviewMode.None)
        {
            if (TryBuildScenePlacementPreview(targetBlock, current, out preview))
            {
                if (isRepaintEvent)
                {
                    DrawScenePlacementPreview(targetBlock.transform, preview);
                }

                if (TryHandleScenePlacementClick(current, preview))
                {
                    QueueDeferredRepaint();
                }
            }

            return;
        }

        ClearCachedWallSurfaceContext();
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

    private void HandleSceneHotkeys(Event current)
    {
        if (current == null || current.type != EventType.KeyDown || previewMode == PreviewMode.None)
        {
            return;
        }

        if (current.keyCode != KeyCode.R)
        {
            return;
        }

        if (previewMode == PreviewMode.Wall)
        {
            wallRotationY = RoomSlotGridUtility.NormalizeRotation(wallRotationY + 90f);
        }
        else
        {
            floorRotationY = RoomSlotGridUtility.NormalizeRotation(floorRotationY + 90f);
        }

        current.Use();
        QueueDeferredRepaint();
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

    private void InvalidateScenePreviewCache()
    {
        if (previewMode != PreviewMode.Wall)
        {
            hoveredWallLayerIndex = 0;
            hoveredWallLayerCount = 1;
            hoveredWallSurfaceHeight = 0f;
            ClearCachedWallSurfaceContext();
        }
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

    private bool TryBuildScenePlacementPreview(
        MemorySpaceBlock targetBlock,
        Event current,
        out ScenePlacementPreview preview)
    {
        preview = default;
        if (targetBlock == null || current == null || current.alt || previewMode == PreviewMode.None)
        {
            return false;
        }

        RoomSlotSurfaceType surfaceType = previewMode == PreviewMode.Wall
            ? RoomSlotSurfaceType.Wall
            : RoomSlotSurfaceType.Floor;

        if (!TryBuildPlacementCandidateFromScenePoint(targetBlock, surfaceType, current.mousePosition, out PlacementCandidate candidate, out string message))
        {
            if (surfaceType == RoomSlotSurfaceType.Wall)
            {
                DebugWallPlacement("PreviewBlocked", $"TryBuildPlacementCandidateFromScenePoint failed: {message}");
                return false;
            }

            preview.HasPreview = true;
            preview.CanPlace = false;
            preview.Message = message;
            preview.LocalCenter = Vector3.zero;
            preview.LocalSize = Vector3.one * RoomSlotGridUtility.GetGridSize(targetBlock);
            preview.LocalRotation = Quaternion.identity;
            return true;
        }

        if (!TryResolvePlacementCandidate(targetBlock, ref candidate, out Vector3 localPosition, out Quaternion localRotation, out string errorMessage))
        {
            if (candidate.SurfaceType == RoomSlotSurfaceType.Wall)
            {
                DebugWallPlacement(
                    "PreviewResolveFailed",
                    $"side={candidate.WallSide} wallGridPos={candidate.WallGridPosition} layer={candidate.WallLayerIndex + 1}/{Mathf.Max(1, candidate.WallLayerCount)} error={errorMessage}");
            }

            if (candidate.SurfaceType == RoomSlotSurfaceType.Wall)
            {
                localPosition = RoomSlotGridUtility.GetWallLocalPosition(
                    targetBlock,
                    candidate.WallSide,
                    candidate.WallGridPosition,
                    candidate.WidthUnits,
                    candidate.HeightOffset);
                localRotation = RoomSlotGridUtility.GetWallLocalRotation(candidate.WallSide, candidate.RotationY);
            }

            preview.HasPreview = true;
            preview.CanPlace = false;
            preview.Candidate = candidate;
            preview.Message = errorMessage;
            ResolveAuthoredPreviewTransform(slotPrefab, localPosition, localRotation, out Vector3 previewPosition, out Quaternion previewRotation);
            preview.LocalCenter = GetPreviewDisplayCenter(candidate, previewPosition);
            preview.LocalSize = candidate.SurfaceType == RoomSlotSurfaceType.Floor
                ? new Vector3(
                    candidate.FloorWidthHalf * (RoomSlotGridUtility.GetGridSize(targetBlock) / RoomSlotGridUtility.GetFloorGridSubdivision()),
                    PreviewFloorHeight,
                    candidate.FloorDepthHalf * (RoomSlotGridUtility.GetGridSize(targetBlock) / RoomSlotGridUtility.GetFloorGridSubdivision()))
                : GetWallPreviewSize(targetBlock, candidate);
            preview.LocalRotation = previewRotation;
            PopulateWallGuidePreview(ref preview, candidate, previewPosition, targetBlock);
            return true;
        }

        RoomSlotPlacementMetadata snapshot = BuildPreviewSnapshot(candidate);
        List<RoomSlotPlacementMetadata> overlaps = new List<RoomSlotPlacementMetadata>();
        try
        {
            overlaps = RoomSlotGridUtility.FindOverlaps(targetBlock, snapshot);
        }
        finally
        {
            if (snapshot != null)
            {
                DestroyImmediate(snapshot.gameObject);
            }
        }

        preview.HasPreview = true;
        preview.Candidate = candidate;
        preview.Overlaps = overlaps;
        ResolveAuthoredPreviewTransform(slotPrefab, localPosition, localRotation, out Vector3 resolvedPreviewPosition, out Quaternion resolvedPreviewRotation);
        preview.LocalCenter = GetPreviewDisplayCenter(candidate, resolvedPreviewPosition);
        preview.LocalRotation = resolvedPreviewRotation;
        preview.LocalSize = candidate.SurfaceType == RoomSlotSurfaceType.Floor
            ? new Vector3(
                candidate.FloorWidthHalf * (RoomSlotGridUtility.GetGridSize(targetBlock) / RoomSlotGridUtility.GetFloorGridSubdivision()),
                PreviewFloorHeight,
                candidate.FloorDepthHalf * (RoomSlotGridUtility.GetGridSize(targetBlock) / RoomSlotGridUtility.GetFloorGridSubdivision()))
            : GetWallPreviewSize(targetBlock, candidate);
        preview.CanPlace = overlaps.Count == 0 || replaceOverlappingSlots;
        PopulateWallGuidePreview(ref preview, candidate, resolvedPreviewPosition, targetBlock);

        if (candidate.SurfaceType == RoomSlotSurfaceType.Wall)
        {
            DebugWallPlacement(
                "PreviewReady",
                $"side={candidate.WallSide} wallGridPos={candidate.WallGridPosition} grid=({candidate.GridX},{candidate.GridZ}) layer={candidate.WallLayerIndex + 1}/{Mathf.Max(1, candidate.WallLayerCount)} canPlace={preview.CanPlace} overlaps={(overlaps != null ? overlaps.Count : 0)} localPos={localPosition}");
        }

        if (overlaps.Count > 0 && !replaceOverlappingSlots)
        {
            preview.Message = $"Overlap with {overlaps.Count} existing room slot placement(s).";
        }
        else if (overlaps.Count > 0)
        {
            preview.Message = $"Replace {overlaps.Count} existing room slot placement(s).";
        }
        else if (candidate.SurfaceType == RoomSlotSurfaceType.Wall)
        {
            preview.Message = $"{candidate.SurfaceType} {candidate.WallSide} [{candidate.WallGridPosition}] Layer {candidate.WallLayerIndex + 1}/{Mathf.Max(1, candidate.WallLayerCount)}";
        }
        else
        {
            preview.Message = $"{candidate.SurfaceType} @ half-grid ({candidate.FloorGridXHalf}, {candidate.FloorGridZHalf})";
        }

        return true;
    }

    private bool TryBuildDeletePreview(
        MemorySpaceBlock block,
        Event current,
        out ScenePlacementPreview preview)
    {
        preview = default;
        if (block == null || current == null || current.alt || (!sceneDeleteMode && !current.shift))
        {
            return false;
        }

        if (TryRaycastRoomSlotPlacementUnderCursor(block, current.mousePosition, out RoomSlotPlacementMetadata pickedPlacement, out _))
        {
            return TryBuildDeletePreviewFromMetadata(block, pickedPlacement, out preview);
        }

        Vector3 localPoint;
        if (previewMode == PreviewMode.Wall
            && TryPickWallSurfaceContext(block, current.mousePosition, out WallSurfaceContext deleteWallContext, out _))
        {
            localPoint = deleteWallContext.LocalHitPoint;
        }
        else if (!TryRaycastAuthoringPlane(block.transform, current.mousePosition, out localPoint))
        {
            return false;
        }

        if (!TryFindNearestRoomSlotPlacementMetadata(block, localPoint, out RoomSlotPlacementMetadata metadata) || metadata == null)
        {
            return false;
        }

        return TryBuildDeletePreviewFromMetadata(block, metadata, out preview);
    }

    private bool TryBuildDeletePreviewFromMetadata(
        MemorySpaceBlock block,
        RoomSlotPlacementMetadata metadata,
        out ScenePlacementPreview preview)
    {
        preview = default;
        if (block == null || metadata == null)
        {
            return false;
        }

        if (!TryGetRoomSlotPreviewBounds(block, metadata.gameObject, out Vector3 localCenter, out Vector3 localSize))
        {
            PlacementCandidate previewCandidate = new PlacementCandidate
            {
                SurfaceType = metadata.surfaceType,
                WidthUnits = metadata.widthUnits,
                DepthUnits = metadata.depthUnits,
                FloorGridXHalf = RoomSlotGridUtility.GetFloorGridXHalf(metadata),
                FloorGridZHalf = RoomSlotGridUtility.GetFloorGridZHalf(metadata),
                FloorWidthHalf = RoomSlotGridUtility.GetFloorWidthHalf(metadata),
                FloorDepthHalf = RoomSlotGridUtility.GetFloorDepthHalf(metadata),
                WallSide = metadata.wallSide,
                WallGridPosition = metadata.wallGridPosition,
                GridX = metadata.gridX,
                GridZ = metadata.gridZ,
                RotationY = metadata.rotationY,
                HeightOffset = metadata.heightOffset,
                WallLayerIndex = metadata.wallLayerIndex,
                WallLayerCount = Mathf.Max(1, metadata.wallLayerCount),
                WallSurfaceHeight = metadata.wallSurfaceHeight
            };
            localCenter = GetPreviewDisplayCenter(previewCandidate, metadata.localPosition);
            localSize = metadata.surfaceType == RoomSlotSurfaceType.Floor
                ? new Vector3(
                    RoomSlotGridUtility.GetFloorWidthHalf(metadata) * (RoomSlotGridUtility.GetGridSize(block) / RoomSlotGridUtility.GetFloorGridSubdivision()),
                    PreviewFloorHeight,
                    RoomSlotGridUtility.GetFloorDepthHalf(metadata) * (RoomSlotGridUtility.GetGridSize(block) / RoomSlotGridUtility.GetFloorGridSubdivision()))
                : GetWallPreviewSize(
                    block,
                    previewCandidate);
        }

        preview.HasPreview = true;
        preview.IsDeletePreview = true;
        preview.TargetPlacement = metadata;
        preview.LocalCenter = localCenter;
        preview.LocalSize = localSize;
        preview.LocalRotation = Quaternion.Euler(metadata.localEulerAngles);
        preview.Message = $"Delete {metadata.slotPlacementId}";
        return true;
    }

    private static Vector3 GetPreviewDisplayCenter(PlacementCandidate candidate, Vector3 placementLocalPosition)
    {
        if (candidate.SurfaceType == RoomSlotSurfaceType.Wall && candidate.WallLayerCount > 0 && candidate.WallSurfaceHeight > 0f)
        {
            return placementLocalPosition + Vector3.up * ((candidate.WallSurfaceHeight / candidate.WallLayerCount) * 0.5f);
        }

        return placementLocalPosition;
    }

    private void PopulateWallGuidePreview(ref ScenePlacementPreview preview, PlacementCandidate candidate, Vector3 placementLocalPosition, MemorySpaceBlock block)
    {
        if (candidate.SurfaceType != RoomSlotSurfaceType.Wall || block == null || candidate.WallLayerCount <= 0 || candidate.WallSurfaceHeight <= 0f)
        {
            preview.ShowWallLayerGuide = false;
            return;
        }

        float gridSize = RoomSlotGridUtility.GetGridSize(block);
        float width = Mathf.Max(1, candidate.WidthUnits) * gridSize;
        preview.ShowWallLayerGuide = true;
        preview.WallLayerCount = candidate.WallLayerCount;
        preview.ActiveWallLayerIndex = Mathf.Clamp(candidate.WallLayerIndex, 0, candidate.WallLayerCount - 1);
        preview.WallGuideSide = candidate.WallSide;
        Vector3 wallBaseLocalPosition = RoomSlotGridUtility.GetWallLocalPosition(
            block,
            candidate.WallSide,
            candidate.WallGridPosition,
            candidate.WidthUnits,
            0f);
        preview.WallGuideLocalCenter = wallBaseLocalPosition + Vector3.up * (candidate.WallSurfaceHeight * 0.5f);
        preview.WallGuideLocalSize = candidate.WallSide == WallSide.North || candidate.WallSide == WallSide.South
            ? new Vector3(width, candidate.WallSurfaceHeight, PreviewThickness)
            : new Vector3(PreviewThickness, candidate.WallSurfaceHeight, width);
    }

    private void DrawScenePlacementPreview(Transform blockRoot, ScenePlacementPreview preview)
    {
        if (blockRoot == null || !preview.HasPreview)
        {
            return;
        }

        if (preview.ShowWallLayerGuide)
        {
            DrawWallLayerGuide(blockRoot, preview);
        }

        using (new Handles.DrawingScope(
                   Matrix4x4.TRS(
                       blockRoot.TransformPoint(preview.LocalCenter),
                       blockRoot.rotation * preview.LocalRotation,
                       Vector3.one)))
        {
            Handles.color = GetScenePreviewColor(preview);
            Handles.DrawWireCube(Vector3.zero, preview.LocalSize);
            Handles.ArrowHandleCap(0, Vector3.zero, Quaternion.identity, Mathf.Max(preview.LocalSize.x, preview.LocalSize.z) * 0.6f, EventType.Repaint);
        }

        Vector3 worldLabelPosition = blockRoot.TransformPoint(preview.LocalCenter + Vector3.up * 0.2f);
        Handles.Label(worldLabelPosition, preview.Message);
    }

    private static void DrawWallLayerGuide(Transform blockRoot, ScenePlacementPreview preview)
    {
        if (blockRoot == null || !preview.ShowWallLayerGuide || preview.WallLayerCount <= 0)
        {
            return;
        }

        using (new Handles.DrawingScope(
                   Matrix4x4.TRS(
                       blockRoot.TransformPoint(preview.WallGuideLocalCenter),
                       blockRoot.rotation,
                       Vector3.one)))
        {
            float layerHeight = preview.WallGuideLocalSize.y / preview.WallLayerCount;
            Vector3 layerSize = new Vector3(
                preview.WallGuideLocalSize.x,
                layerHeight,
                preview.WallGuideLocalSize.z);

            Handles.color = new Color(0.35f, 0.75f, 1f, 0.4f);
            Handles.DrawWireCube(Vector3.zero, preview.WallGuideLocalSize);

            for (int layer = 0; layer < preview.WallLayerCount; layer++)
            {
                float centerY = -preview.WallGuideLocalSize.y * 0.5f + (layerHeight * (layer + 0.5f));
                Handles.color = layer == preview.ActiveWallLayerIndex
                    ? new Color(0.15f, 1f, 0.75f, 0.9f)
                    : new Color(0.35f, 0.75f, 1f, 0.25f);
                Handles.DrawWireCube(new Vector3(0f, centerY, 0f), layerSize);
            }
        }
    }

    private static Color GetScenePreviewColor(ScenePlacementPreview preview)
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

    private bool TryHandleScenePlacementClick(Event current, ScenePlacementPreview preview)
    {
        if (current == null
            || current.type != EventType.MouseDown
            || current.button != 0
            || current.alt
            || !preview.HasPreview)
        {
            return false;
        }

        if (preview.CanPlace)
        {
            if (preview.Candidate.SurfaceType == RoomSlotSurfaceType.Wall)
            {
                DebugWallPlacement(
                    "ClickPlace",
                    $"side={preview.Candidate.WallSide} wallGridPos={preview.Candidate.WallGridPosition} layer={preview.Candidate.WallLayerIndex + 1}/{Mathf.Max(1, preview.Candidate.WallLayerCount)} localCenter={preview.LocalCenter}");
            }

            PlaceRoomSlot(preview.Candidate);
        }
        else if (preview.Candidate.SurfaceType == RoomSlotSurfaceType.Wall)
        {
            DebugWallPlacement("ClickBlocked", $"message={preview.Message}");
        }

        current.Use();
        return true;
    }

    private bool TryHandleDeletePreviewClick(Event current, MemorySpaceBlock block, ScenePlacementPreview preview)
    {
        if (current == null
            || current.type != EventType.MouseDown
            || current.button != 0
            || current.alt
            || !preview.IsDeletePreview
            || preview.TargetPlacement == null
            || block == null)
        {
            return false;
        }

        if (preview.TargetPlacement.GetComponentInParent<MemorySpaceBlock>() == block)
        {
            DeleteRoomSlotPlacement(preview.TargetPlacement);
        }

        current.Use();
        return true;
    }

    private void PlaceRoomSlot(PlacementCandidate candidate)
    {
        try
        {
            if (candidate.SurfaceType == RoomSlotSurfaceType.Wall)
            {
                DebugWallPlacement(
                    "PlaceRoomSlot",
                    $"start side={candidate.WallSide} wallGridPos={candidate.WallGridPosition} grid=({candidate.GridX},{candidate.GridZ}) layer={candidate.WallLayerIndex + 1}/{Mathf.Max(1, candidate.WallLayerCount)}");
            }

            ValidationResult prefabValidation = ValidateSlotPrefab(slotPrefab);
            if (prefabValidation.HasErrors)
            {
                PresentValidation("Slot prefab validation failed.", prefabValidation, MessageType.Error);
                return;
            }

            using (EditContext context = OpenEditContext())
            {
                if (context == null || context.WorkingBlock == null)
                {
                    return;
                }

                if (!TryResolvePlacementCandidate(context.WorkingBlock, ref candidate, out Vector3 localPosition, out Quaternion localRotation, out string errorMessage))
                {
                    if (candidate.SurfaceType == RoomSlotSurfaceType.Wall)
                    {
                        DebugWallPlacement("PlaceResolveFailed", errorMessage);
                    }

                    SetStatus(errorMessage, MessageType.Error);
                    return;
                }

                if (candidate.SurfaceType == RoomSlotSurfaceType.Wall)
                {
                    DebugWallPlacement("PlaceResolved", $"localPosition={localPosition} localRotation={localRotation.eulerAngles}");
                }

                RoomSlotPlacementMetadata previewSnapshot = BuildPreviewSnapshot(candidate);
                List<RoomSlotPlacementMetadata> overlaps = RoomSlotGridUtility.FindOverlaps(context.WorkingBlock, previewSnapshot);
                if (previewSnapshot != null)
                {
                    DestroyImmediate(previewSnapshot.gameObject);
                }

                if (overlaps.Count > 0 && !replaceOverlappingSlots)
                {
                    SetStatus($"Placement overlaps {overlaps.Count} existing room slot placement(s). Enable Replace Overlapping Slots to replace them.", MessageType.Warning);
                    return;
                }

                Transform slotRoot = GetOrCreateChild(context.WorkingBlock.transform, SlotRootName, !context.IsPrefabAssetEdit);
                if (slotRoot == null)
                {
                    SetStatus("Failed to create SlotRoot.", MessageType.Error);
                    return;
                }

                if (!context.IsPrefabAssetEdit)
                {
                    Undo.RegisterFullObjectHierarchyUndo(context.WorkingBlock.gameObject, "Place Room Slot");
                }

                for (int i = 0; i < overlaps.Count; i++)
                {
                    if (context.IsPrefabAssetEdit)
                    {
                        DestroyImmediate(overlaps[i].gameObject);
                    }
                    else
                    {
                        Undo.DestroyObjectImmediate(overlaps[i].gameObject);
                    }
                }

                GameObject instance = InstantiateSlotPrefab(slotPrefab, slotRoot, context.IsPrefabAssetEdit);
                if (instance == null)
                {
                    SetStatus("Failed to instantiate slot prefab.", MessageType.Error);
                    return;
                }

                string slotPlacementId = GenerateSlotPlacementId();
                instance.name = slotPlacementId;
                ApplyAuthoredPlacementTransform(instance.transform, slotPrefab, localPosition, localRotation);

                ValidationResult setupValidation = EnsureFurnitureAndSlots(instance, context, slotPlacementId, candidate);
                if (setupValidation.HasErrors)
                {
                    if (context.IsPrefabAssetEdit)
                    {
                        DestroyImmediate(instance);
                    }
                    else
                    {
                        Undo.DestroyObjectImmediate(instance);
                    }

                    PresentValidation("Room slot placement failed.", setupValidation, MessageType.Error);
                    return;
                }

                MemoryDisplayFurniture furniture = instance.GetComponent<MemoryDisplayFurniture>();
                MemoryDisplaySlot[] slots = instance.GetComponentsInChildren<MemoryDisplaySlot>(true);
                ApplyStableIds(context.WorkingBlock, instance, furniture, slots, slotPlacementId);

                RoomSlotPlacementMetadata metadata = instance.GetComponent<RoomSlotPlacementMetadata>();
                if (metadata == null)
                {
                    metadata = context.IsPrefabAssetEdit
                        ? instance.AddComponent<RoomSlotPlacementMetadata>()
                        : Undo.AddComponent<RoomSlotPlacementMetadata>(instance);
                }

                PopulateMetadata(
                    metadata,
                    context.WorkingBlock,
                    slotPrefab,
                    candidate,
                    slotPlacementId,
                    furniture,
                    slots);

                if (resizeHitboxToGridFootprint)
                {
                    ResizeHitbox(context, instance, furniture, candidate);
                }

                EditorUtility.SetDirty(instance);
                EditorUtility.SetDirty(context.WorkingBlock);
                SaveEditedTarget(context);

                if (candidate.SurfaceType == RoomSlotSurfaceType.Wall)
                {
                    DebugWallPlacement("PlaceCompleted", $"slotPlacementId={slotPlacementId}");
                }

                ValidationResult placementValidation = ValidateCurrentBlockSlotsInternal(context.WorkingBlock);
                placementValidation.Merge(setupValidation);
                PresentValidation(
                    $"Placed {slotPlacementId} on {RoomSlotGridUtility.GetBlockInstanceId(context.WorkingBlock)}.",
                    placementValidation,
                    placementValidation.HasWarnings ? MessageType.Warning : MessageType.Info);

                Selection.activeGameObject = context.IsPrefabAssetEdit ? null : instance;
            }
        }
        catch (Exception exception)
        {
            ReportException("Place Room Slot", exception);
        }
    }

    private void RemoveSelectedRoomSlot()
    {
        try
        {
            if (!TryResolveTargetBlock(targetObject, out MemorySpaceBlock targetBlock, out string targetError))
            {
                SetStatus(targetError, MessageType.Warning);
                return;
            }

            if (Selection.activeGameObject == null)
            {
                SetStatus("Select a placed room slot instance to remove.", MessageType.Warning);
                return;
            }

            RoomSlotPlacementMetadata metadata = Selection.activeGameObject.GetComponentInParent<RoomSlotPlacementMetadata>();
            if (metadata == null)
            {
                SetStatus("Selection does not contain RoomSlotPlacementMetadata.", MessageType.Warning);
                return;
            }

            if (metadata.GetComponentInParent<MemorySpaceBlock>() != targetBlock)
            {
                SetStatus("Selected room slot does not belong to the current target block.", MessageType.Warning);
                return;
            }

            DeleteRoomSlotPlacement(metadata);
        }
        catch (Exception exception)
        {
            ReportException("Remove Selected Room Slot", exception);
        }
    }

    private void DeleteRoomSlotPlacement(RoomSlotPlacementMetadata metadata)
    {
        if (metadata == null)
        {
            return;
        }

        MemorySpaceBlock targetBlock = metadata.GetComponentInParent<MemorySpaceBlock>();
        if (targetBlock == null)
        {
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(targetBlock.gameObject, "Remove Room Slot");
        Undo.DestroyObjectImmediate(metadata.gameObject);
        EditorUtility.SetDirty(targetBlock);
        MarkTargetDirty(targetBlock.gameObject);
        SetStatus("Removed selected room slot.", MessageType.Info);
    }

    private void ValidateCurrentBlockSlots()
    {
        try
        {
            using (EditContext context = OpenEditContext())
            {
                if (context == null || context.WorkingBlock == null)
                {
                    return;
                }

                ValidationResult validation = ValidateCurrentBlockSlotsInternal(context.WorkingBlock);
                PresentValidation(
                    validation.HasErrors
                        ? "Room slot validation found errors."
                        : validation.HasWarnings
                            ? "Room slot validation found warnings."
                            : $"Room slot validation passed for {context.WorkingBlock.name}.",
                    validation,
                    validation.HasErrors ? MessageType.Error : validation.HasWarnings ? MessageType.Warning : MessageType.Info);
            }
        }
        catch (Exception exception)
        {
            ReportException("Validate Current Block Slots", exception);
        }
    }

    private ValidationResult ValidateCurrentBlockSlotsInternal(MemorySpaceBlock block)
    {
        ValidationResult result = new ValidationResult();
        if (block == null)
        {
            result.AddError("Target MemorySpaceBlock is missing.");
            return result;
        }

        HashSet<string> placementIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        HashSet<string> furnitureIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        HashSet<string> slotIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        HashSet<string> occupiedFloorCells = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        HashSet<string> occupiedWallEdges = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        RoomSlotPlacementMetadata[] placements = block.GetComponentsInChildren<RoomSlotPlacementMetadata>(true);
        Array.Sort(placements, CompareRoomSlotMetadata);
        for (int i = 0; i < placements.Length; i++)
        {
            RoomSlotPlacementMetadata metadata = placements[i];
            if (metadata == null)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(metadata.slotPlacementId) || !placementIds.Add(metadata.slotPlacementId))
            {
                result.AddError($"Duplicate or empty slotPlacementId on {metadata.name}: {metadata.slotPlacementId}");
            }

            if (string.IsNullOrWhiteSpace(metadata.blockTypeId))
            {
                result.AddError($"blockTypeId is empty on {metadata.name}.");
            }

            MemoryDisplayFurniture furniture = metadata.GetComponent<MemoryDisplayFurniture>();
            MemoryDisplaySlot[] displaySlots = metadata.GetComponentsInChildren<MemoryDisplaySlot>(true);

            if (furniture == null)
            {
                result.AddWarning($"Missing MemoryDisplayFurniture on {metadata.name}.");
            }
            else
            {
                string furnitureId = GetSerializedString(furniture, "furnitureId");
                if (string.IsNullOrWhiteSpace(furnitureId) || !furnitureIds.Add(furnitureId))
                {
                    result.AddError($"Duplicate or empty furnitureId on {metadata.name}: {furnitureId}");
                }
            }

            if (displaySlots.Length == 0)
            {
                if (furniture == null)
                {
                    result.AddError($"Missing MemoryDisplayFurniture and MemoryDisplaySlot on {metadata.name}.");
                }
                else
                {
                    result.AddInfo($"No MemoryDisplaySlot found on {metadata.name}; treating it as non-slot furniture.");
                }
            }

            metadata.slotIds = new List<string>(displaySlots.Length);
            for (int slotIndex = 0; slotIndex < displaySlots.Length; slotIndex++)
            {
                string slotId = GetSerializedString(displaySlots[slotIndex], "slotId");
                metadata.slotIds.Add(slotId);
                if (string.IsNullOrWhiteSpace(slotId) || !slotIds.Add(slotId))
                {
                    result.AddError($"Duplicate or empty slotId on {metadata.name}: {slotId}");
                }
            }

            metadata.CaptureTransformData();

            switch (metadata.surfaceType)
            {
                case RoomSlotSurfaceType.Floor:
                    if (!RoomSlotGridUtility.IsFloorPlacementInBoundsHalf(
                        block,
                        RoomSlotGridUtility.GetFloorGridXHalf(metadata),
                        RoomSlotGridUtility.GetFloorGridZHalf(metadata),
                        RoomSlotGridUtility.GetFloorWidthHalf(metadata),
                        RoomSlotGridUtility.GetFloorDepthHalf(metadata)))
                    {
                        result.AddError($"Floor footprint out of bounds on {metadata.slotPlacementId}.");
                    }

                    List<Vector2Int> floorCells = RoomSlotGridUtility.GetFloorFootprintHalfCells(
                        RoomSlotGridUtility.GetFloorGridXHalf(metadata),
                        RoomSlotGridUtility.GetFloorGridZHalf(metadata),
                        RoomSlotGridUtility.GetFloorWidthHalf(metadata),
                        RoomSlotGridUtility.GetFloorDepthHalf(metadata));
                    for (int cellIndex = 0; cellIndex < floorCells.Count; cellIndex++)
                    {
                        string key = $"H:{floorCells[cellIndex].x}:{floorCells[cellIndex].y}";
                        if (!occupiedFloorCells.Add(key))
                        {
                            result.AddError($"Overlapping floor footprint at {key}.");
                        }
                    }
                    break;

                case RoomSlotSurfaceType.Wall:
                    if (!RoomSlotGridUtility.IsWallPlacementInBounds(block, metadata.wallSide, metadata.wallGridPosition, metadata.widthUnits))
                    {
                        result.AddError($"Invalid wallGridPosition on {metadata.slotPlacementId}: {metadata.wallSide} [{metadata.wallGridPosition}]");
                    }

                    PlacementCandidate validationCandidate = new PlacementCandidate
                    {
                        SurfaceType = RoomSlotSurfaceType.Wall,
                        GridX = metadata.gridX,
                        GridZ = metadata.gridZ,
                        WidthUnits = Mathf.Max(1, metadata.widthUnits),
                        DepthUnits = 1,
                        RotationY = metadata.rotationY,
                        WallSide = metadata.wallSide,
                        WallGridPosition = metadata.wallGridPosition,
                        HeightOffset = metadata.heightOffset,
                        WallLayerIndex = metadata.wallLayerIndex,
                        WallLayerCount = Mathf.Max(1, metadata.wallLayerCount),
                        WallSurfaceHeight = metadata.wallSurfaceHeight
                    };
                    if (!TryResolveWallPlacementCandidate(block, ref validationCandidate, out string wallValidationMessage))
                    {
                        result.AddError($"Invalid wall placement on {metadata.slotPlacementId}: {wallValidationMessage}");
                    }

                    List<string> wallEdgeKeys = RoomSlotGridUtility.GetWallEdgeKeys(metadata.gridX, metadata.gridZ, metadata.wallSide, metadata.widthUnits);
                    for (int edgeIndex = 0; edgeIndex < wallEdgeKeys.Count; edgeIndex++)
                    {
                        string layeredKey = $"{wallEdgeKeys[edgeIndex]}:L{RoomSlotGridUtility.GetWallLayerIndex(metadata)}";
                        if (!occupiedWallEdges.Add(layeredKey))
                        {
                            result.AddError($"Overlapping wall footprint at {layeredKey}.");
                        }
                    }
                    break;
            }
        }

        return result;
    }

    private ValidationResult ValidateSlotPrefab(GameObject prefab)
    {
        ValidationResult result = new ValidationResult();
        if (prefab == null)
        {
            result.AddError("Slot prefab is not assigned.");
            return result;
        }

        MemoryDisplayFurniture rootFurniture = prefab.GetComponent<MemoryDisplayFurniture>();
        MemoryDisplaySlot[] slots = prefab.GetComponentsInChildren<MemoryDisplaySlot>(true);
        if (rootFurniture == null)
        {
            if (slots.Length > 0)
            {
                result.AddWarning("Slot prefab has MemoryDisplaySlot children but no root MemoryDisplayFurniture. The tool will add one to the placed instance root.");
            }
            else
            {
                result.AddError("Slot prefab is missing MemoryDisplayFurniture and MemoryDisplaySlot.");
            }
        }
        else
        {
            if (slots.Length == 0)
            {
                result.AddInfo("Found root MemoryDisplayFurniture with no MemoryDisplaySlot children. This prefab will be treated as non-slot furniture.");
            }
            else
            {
                result.AddInfo($"Found root MemoryDisplayFurniture with {slots.Length} slot(s).");
            }
        }

        return result;
    }

    private ValidationResult EnsureFurnitureAndSlots(
        GameObject instance,
        EditContext context,
        string slotPlacementId,
        PlacementCandidate candidate)
    {
        ValidationResult result = new ValidationResult();
        if (instance == null)
        {
            result.AddError("Placed instance is missing.");
            return result;
        }

        MemoryDisplayFurniture furniture = instance.GetComponent<MemoryDisplayFurniture>();
        MemoryDisplaySlot[] slots = instance.GetComponentsInChildren<MemoryDisplaySlot>(true);
        if (furniture == null && slots.Length == 0)
        {
            result.AddError("Placed instance must contain a root MemoryDisplayFurniture or child MemoryDisplaySlot components.");
            return result;
        }

        if (furniture == null)
        {
            furniture = context != null && !context.IsPrefabAssetEdit
                ? Undo.AddComponent<MemoryDisplayFurniture>(instance)
                : instance.AddComponent<MemoryDisplayFurniture>();
            result.AddWarning($"Created MemoryDisplayFurniture on {instance.name} because the prefab only exposed child MemoryDisplaySlot components.");
        }

        furniture.AutoCollectFeatures();
        if (slots.Length == 0)
        {
            result.AddInfo($"Placed {instance.name} as non-slot furniture.");
        }

        if (candidate.SurfaceType == RoomSlotSurfaceType.Wall)
        {
            result.AddInfo($"Wall placement resolved to {candidate.WallSide} [{candidate.WallGridPosition}] on block {RoomSlotGridUtility.GetBlockInstanceId(context != null ? context.WorkingBlock : null)}.");
        }

        return result;
    }

    private void ApplyStableIds(
        MemorySpaceBlock block,
        GameObject instance,
        MemoryDisplayFurniture furniture,
        MemoryDisplaySlot[] slots,
        string slotPlacementId)
    {
        HashSet<string> usedFurnitureIds = CollectExistingFurnitureIds(block, instance);
        HashSet<string> usedSlotIds = CollectExistingSlotIds(block, instance);
        string blockTypeId = RoomSlotGridUtility.GetBlockTypeId(block);

        if (furniture != null)
        {
            string currentFurnitureId = GetSerializedString(furniture, "furnitureId");
            if (string.IsNullOrWhiteSpace(currentFurnitureId) || usedFurnitureIds.Contains(currentFurnitureId))
            {
                string generatedFurnitureId = $"DF_{SanitizeIdToken(blockTypeId)}_{slotPlacementId}";
                SetSerializedString(furniture, "furnitureId", generatedFurnitureId);
                currentFurnitureId = generatedFurnitureId;
            }

            usedFurnitureIds.Add(currentFurnitureId);
            furniture.AutoCollectFeatures();
        }

        for (int i = 0; i < slots.Length; i++)
        {
            string currentSlotId = GetSerializedString(slots[i], "slotId");
            if (string.IsNullOrWhiteSpace(currentSlotId) || usedSlotIds.Contains(currentSlotId))
            {
                currentSlotId = $"Slot_{slotPlacementId}_{(i + 1).ToString("00")}";
                SetSerializedString(slots[i], "slotId", currentSlotId);
            }

            usedSlotIds.Add(currentSlotId);
            EditorUtility.SetDirty(slots[i]);
        }
    }

    private void PopulateMetadata(
        RoomSlotPlacementMetadata metadata,
        MemorySpaceBlock block,
        GameObject prefab,
        PlacementCandidate candidate,
        string slotPlacementId,
        MemoryDisplayFurniture furniture,
        MemoryDisplaySlot[] slots)
    {
        metadata.slotPlacementId = slotPlacementId;
        metadata.blockTypeId = RoomSlotGridUtility.GetBlockTypeId(block);
        metadata.blockInstanceId = RoomSlotGridUtility.GetBlockInstanceId(block);
        metadata.slotPrefabKey = prefab != null ? prefab.name : string.Empty;
        metadata.surfaceType = candidate.SurfaceType;
        metadata.gridX = candidate.GridX;
        metadata.gridZ = candidate.GridZ;
        metadata.widthUnits = Mathf.Max(1, candidate.WidthUnits);
        metadata.depthUnits = candidate.SurfaceType == RoomSlotSurfaceType.Floor ? Mathf.Max(1, candidate.DepthUnits) : 1;
        metadata.floorGridXHalf = candidate.SurfaceType == RoomSlotSurfaceType.Floor ? candidate.FloorGridXHalf : metadata.gridX * RoomSlotGridUtility.GetFloorGridSubdivision();
        metadata.floorGridZHalf = candidate.SurfaceType == RoomSlotSurfaceType.Floor ? candidate.FloorGridZHalf : metadata.gridZ * RoomSlotGridUtility.GetFloorGridSubdivision();
        metadata.floorWidthHalf = candidate.SurfaceType == RoomSlotSurfaceType.Floor ? Mathf.Max(1, candidate.FloorWidthHalf) : Mathf.Max(1, metadata.widthUnits) * RoomSlotGridUtility.GetFloorGridSubdivision();
        metadata.floorDepthHalf = candidate.SurfaceType == RoomSlotSurfaceType.Floor ? Mathf.Max(1, candidate.FloorDepthHalf) : Mathf.Max(1, metadata.depthUnits) * RoomSlotGridUtility.GetFloorGridSubdivision();
        metadata.rotationY = RoomSlotGridUtility.NormalizeRotation(candidate.RotationY);
        metadata.wallSide = candidate.WallSide;
        metadata.wallGridPosition = candidate.WallGridPosition;
        metadata.heightOffset = candidate.SurfaceType == RoomSlotSurfaceType.Wall ? candidate.HeightOffset : 0f;
        metadata.wallLayerIndex = candidate.SurfaceType == RoomSlotSurfaceType.Wall ? Mathf.Max(0, candidate.WallLayerIndex) : 0;
        metadata.wallLayerCount = candidate.SurfaceType == RoomSlotSurfaceType.Wall ? Mathf.Max(1, candidate.WallLayerCount) : 1;
        metadata.wallSurfaceHeight = candidate.SurfaceType == RoomSlotSurfaceType.Wall ? Mathf.Max(0f, candidate.WallSurfaceHeight) : 0f;
        metadata.furnitureId = furniture != null ? GetSerializedString(furniture, "furnitureId") : string.Empty;
        metadata.slotIds = new List<string>(slots.Length);
        metadata.hasDisplaySlots = slots.Length > 0;
        metadata.lightFeatureCount = furniture != null ? furniture.GetComponentsInChildren<MemoryDisplayLight>(true).Length : 0;
        metadata.frameSurfaceCount = furniture != null ? furniture.GetComponentsInChildren<MemoryDisplayFrameSurface>(true).Length : 0;
        for (int i = 0; i < slots.Length; i++)
        {
            metadata.slotIds.Add(GetSerializedString(slots[i], "slotId"));
        }

        metadata.CaptureTransformData();
        EditorUtility.SetDirty(metadata);
    }

    private void ResizeHitbox(
        EditContext context,
        GameObject instance,
        MemoryDisplayFurniture furniture,
        PlacementCandidate candidate)
    {
        if (instance == null)
        {
            return;
        }

        float gridSize = RoomSlotGridUtility.GetGridSize(context.WorkingBlock);
        Vector3 desiredSize;
        switch (candidate.SurfaceType)
        {
            case RoomSlotSurfaceType.Wall:
                if (candidate.WallSide == WallSide.North || candidate.WallSide == WallSide.South)
                {
                    desiredSize = new Vector3(Mathf.Max(1, candidate.WidthUnits) * gridSize, PreviewWallHeight, Mathf.Max(PreviewThickness, gridSize * 0.15f));
                }
                else
                {
                    desiredSize = new Vector3(Mathf.Max(PreviewThickness, gridSize * 0.15f), PreviewWallHeight, Mathf.Max(1, candidate.WidthUnits) * gridSize);
                }
                break;

            default:
                desiredSize = new Vector3(
                    Mathf.Max(1, candidate.WidthUnits) * gridSize,
                    Mathf.Max(0.1f, PreviewWallHeight),
                    Mathf.Max(1, candidate.DepthUnits) * gridSize);
                break;
        }

        BoxCollider targetCollider = null;
        if (furniture != null)
        {
            furniture.AutoAssignPlacementBounds();
            targetCollider = furniture.PlacementBoundsCollider;
        }

        if (targetCollider == null)
        {
            targetCollider = instance.GetComponent<BoxCollider>();
            if (targetCollider == null)
            {
                targetCollider = context.IsPrefabAssetEdit
                    ? instance.AddComponent<BoxCollider>()
                    : Undo.AddComponent<BoxCollider>(instance);
            }

            SetSerializedObjectReference(furniture, "placementBoundsCollider", targetCollider);
        }

        Vector3 currentCenter = targetCollider.center;
        float currentHeight = Mathf.Max(0.1f, targetCollider.size.y);
        targetCollider.center = new Vector3(currentCenter.x, currentCenter.y, currentCenter.z);
        targetCollider.size = new Vector3(
            Mathf.Max(0.05f, desiredSize.x),
            currentHeight,
            Mathf.Max(0.05f, desiredSize.z));
        targetCollider.isTrigger = true;
        EditorUtility.SetDirty(targetCollider);
        if (furniture != null)
        {
            furniture.AutoCollectSlots();
            EditorUtility.SetDirty(furniture);
        }
    }

    private EditContext OpenEditContext()
    {
        if (!TryResolveTargetBlock(targetObject, out MemorySpaceBlock targetBlock, out string targetError))
        {
            SetStatus(targetError, MessageType.Warning);
            return null;
        }

        EditContext context = new EditContext
        {
            SourceObject = targetObject
        };

        if (EditorUtility.IsPersistent(targetBlock.gameObject))
        {
            context.AssetPath = AssetDatabase.GetAssetPath(targetBlock.gameObject);
            if (string.IsNullOrWhiteSpace(context.AssetPath))
            {
                SetStatus("Unable to resolve prefab asset path for the selected target.", MessageType.Error);
                context.Dispose();
                return null;
            }

            context.LoadedPrefabRoot = PrefabUtility.LoadPrefabContents(context.AssetPath);
            context.WorkingBlock = context.LoadedPrefabRoot.GetComponent<MemorySpaceBlock>();
            if (context.WorkingBlock == null)
            {
                context.WorkingBlock = context.LoadedPrefabRoot.GetComponentInChildren<MemorySpaceBlock>(true);
            }

            if (context.WorkingBlock == null)
            {
                SetStatus("Loaded prefab asset does not contain a MemorySpaceBlock.", MessageType.Error);
                context.Dispose();
                return null;
            }

            context.IsPrefabAssetEdit = true;
            return context;
        }

        context.WorkingBlock = targetBlock;
        return context;
    }

    private void SaveEditedTarget(EditContext context)
    {
        if (context == null || context.WorkingBlock == null)
        {
            return;
        }

        if (context.IsPrefabAssetEdit)
        {
            PrefabUtility.SaveAsPrefabAsset(context.LoadedPrefabRoot, context.AssetPath);
            AssetDatabase.SaveAssets();
            return;
        }

        MarkTargetDirty(context.WorkingBlock.gameObject);
    }

    private void MarkTargetDirty(GameObject targetGameObject)
    {
        if (targetGameObject == null)
        {
            return;
        }

        EditorUtility.SetDirty(targetGameObject);

        if (TrySavePrefabStage(targetGameObject))
        {
            return;
        }

        if (targetGameObject.scene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(targetGameObject.scene);
        }
    }

    private PlacementCandidate BuildCurrentPlacementCandidate(RoomSlotSurfaceType placementSurface)
    {
        return new PlacementCandidate
        {
            SurfaceType = placementSurface,
            GridX = floorGridX,
            GridZ = floorGridZ,
            WidthUnits = placementSurface == RoomSlotSurfaceType.Floor ? Mathf.Max(1, floorWidthUnits) : Mathf.Max(1, wallWidthUnits),
            DepthUnits = placementSurface == RoomSlotSurfaceType.Floor ? Mathf.Max(1, floorDepthUnits) : 1,
            FloorGridXHalf = floorGridX * RoomSlotGridUtility.GetFloorGridSubdivision(),
            FloorGridZHalf = floorGridZ * RoomSlotGridUtility.GetFloorGridSubdivision(),
            FloorWidthHalf = Mathf.Max(1, floorWidthUnits) * RoomSlotGridUtility.GetFloorGridSubdivision(),
            FloorDepthHalf = Mathf.Max(1, floorDepthUnits) * RoomSlotGridUtility.GetFloorGridSubdivision(),
            RotationY = placementSurface == RoomSlotSurfaceType.Floor
                ? RoomSlotGridUtility.NormalizeRotation(floorRotationY)
                : RoomSlotGridUtility.NormalizeRotation(wallRotationY),
            WallSide = wallSide,
            WallGridPosition = wallGridPosition,
            HeightOffset = 0f,
            WallLayerCount = 1,
            WallSurfaceHeight = placementSurface == RoomSlotSurfaceType.Wall ? FullWallHeight : 0f
        };
    }

    private bool TryResolvePlacementCandidate(
        MemorySpaceBlock targetBlock,
        ref PlacementCandidate candidate,
        out Vector3 localPosition,
        out Quaternion localRotation,
        out string errorMessage)
    {
        localPosition = Vector3.zero;
        localRotation = Quaternion.identity;
        errorMessage = string.Empty;

        if (candidate.SurfaceType == RoomSlotSurfaceType.Wall)
        {
            RoomSlotGridUtility.TryGetWallAnchorGrid(
                targetBlock,
                candidate.WallSide,
                candidate.WallGridPosition,
                candidate.WidthUnits,
                out candidate.GridX,
                out candidate.GridZ);
        }

        return RoomSlotGridUtility.TryGetPlacementTransform(
            targetBlock,
            candidate.SurfaceType,
            candidate.GridX,
            candidate.GridZ,
            candidate.WidthUnits,
            candidate.DepthUnits,
            candidate.FloorGridXHalf,
            candidate.FloorGridZHalf,
            candidate.FloorWidthHalf,
            candidate.FloorDepthHalf,
            candidate.RotationY,
            candidate.WallSide,
            candidate.WallGridPosition,
            candidate.HeightOffset,
            out localPosition,
            out localRotation,
            out errorMessage);
    }

    private RoomSlotPlacementMetadata BuildPreviewSnapshot(PlacementCandidate candidate)
    {
        GameObject previewObject = new GameObject("RoomSlotPreviewSnapshot");
        RoomSlotPlacementMetadata metadata = previewObject.AddComponent<RoomSlotPlacementMetadata>();
        metadata.surfaceType = candidate.SurfaceType;
        metadata.gridX = candidate.GridX;
        metadata.gridZ = candidate.GridZ;
        metadata.widthUnits = Mathf.Max(1, candidate.WidthUnits);
        metadata.depthUnits = candidate.SurfaceType == RoomSlotSurfaceType.Floor ? Mathf.Max(1, candidate.DepthUnits) : 1;
        metadata.floorGridXHalf = candidate.SurfaceType == RoomSlotSurfaceType.Floor ? candidate.FloorGridXHalf : metadata.gridX * RoomSlotGridUtility.GetFloorGridSubdivision();
        metadata.floorGridZHalf = candidate.SurfaceType == RoomSlotSurfaceType.Floor ? candidate.FloorGridZHalf : metadata.gridZ * RoomSlotGridUtility.GetFloorGridSubdivision();
        metadata.floorWidthHalf = candidate.SurfaceType == RoomSlotSurfaceType.Floor ? Mathf.Max(1, candidate.FloorWidthHalf) : Mathf.Max(1, metadata.widthUnits) * RoomSlotGridUtility.GetFloorGridSubdivision();
        metadata.floorDepthHalf = candidate.SurfaceType == RoomSlotSurfaceType.Floor ? Mathf.Max(1, candidate.FloorDepthHalf) : Mathf.Max(1, metadata.depthUnits) * RoomSlotGridUtility.GetFloorGridSubdivision();
        metadata.rotationY = RoomSlotGridUtility.NormalizeRotation(candidate.RotationY);
        metadata.wallSide = candidate.WallSide;
        metadata.wallGridPosition = candidate.WallGridPosition;
        metadata.heightOffset = candidate.SurfaceType == RoomSlotSurfaceType.Wall ? candidate.HeightOffset : 0f;
        metadata.wallLayerIndex = candidate.SurfaceType == RoomSlotSurfaceType.Wall ? Mathf.Max(0, candidate.WallLayerIndex) : 0;
        metadata.wallLayerCount = candidate.SurfaceType == RoomSlotSurfaceType.Wall ? Mathf.Max(1, candidate.WallLayerCount) : 1;
        metadata.wallSurfaceHeight = candidate.SurfaceType == RoomSlotSurfaceType.Wall ? Mathf.Max(0f, candidate.WallSurfaceHeight) : 0f;
        return metadata;
    }

    private Vector3 GetWallPreviewSize(MemorySpaceBlock block)
    {
        return GetWallPreviewSize(block, BuildCurrentPlacementCandidate(RoomSlotSurfaceType.Wall));
    }

    private Vector3 GetWallPreviewSize(MemorySpaceBlock block, PlacementCandidate candidate)
    {
        float gridSize = RoomSlotGridUtility.GetGridSize(block);
        float width = Mathf.Max(1, candidate.WidthUnits) * gridSize;
        float height = GetWallPreviewHeight(candidate);
        if (candidate.WallSide == WallSide.North || candidate.WallSide == WallSide.South)
        {
            return new Vector3(width, height, PreviewThickness);
        }

        return new Vector3(PreviewThickness, height, width);
    }

    private bool TryBuildPlacementCandidateFromScenePoint(
        MemorySpaceBlock block,
        RoomSlotSurfaceType surfaceType,
        Vector2 mousePosition,
        out PlacementCandidate candidate,
        out string message)
    {
        candidate = BuildCurrentPlacementCandidate(surfaceType);
        message = string.Empty;
        if (block == null)
        {
            message = "Select a MemorySpaceBlock target first.";
            return false;
        }

        if (surfaceType == RoomSlotSurfaceType.Wall)
        {
            return TryBuildWallPlacementCandidateFromScenePoint(block, mousePosition, ref candidate, out message);
        }

        return TryBuildFloorPlacementCandidateFromScenePoint(block, mousePosition, ref candidate, out message);
    }

    private bool TryBuildFloorPlacementCandidateFromScenePoint(
        MemorySpaceBlock block,
        Vector2 mousePosition,
        ref PlacementCandidate candidate,
        out string message)
    {
        message = string.Empty;
        if (!TryRaycastAuthoringPlane(block.transform, mousePosition, out Vector3 localPoint))
        {
            message = "Could not raycast onto the block authoring plane.";
            return false;
        }

        int gridWidth = RoomSlotGridUtility.GetGridWidth(block);
        int gridDepth = RoomSlotGridUtility.GetGridDepth(block);
        float gridSize = RoomSlotGridUtility.GetGridSize(block);
        float halfGridSize = gridSize / RoomSlotGridUtility.GetFloorGridSubdivision();
        float halfWidth = gridWidth * gridSize * 0.5f;
        float halfDepth = gridDepth * gridSize * 0.5f;
        int halfGridWidth = gridWidth * RoomSlotGridUtility.GetFloorGridSubdivision();
        int halfGridDepth = gridDepth * RoomSlotGridUtility.GetFloorGridSubdivision();
        int snappedCenterXHalf = Mathf.RoundToInt((localPoint.x + halfWidth) / halfGridSize);
        int snappedCenterZHalf = Mathf.RoundToInt((localPoint.z + halfDepth) / halfGridSize);
        int footprintWidthHalf = Mathf.Max(1, candidate.WidthUnits) * RoomSlotGridUtility.GetFloorGridSubdivision();
        int footprintDepthHalf = Mathf.Max(1, candidate.DepthUnits) * RoomSlotGridUtility.GetFloorGridSubdivision();
        int minHalfX = snappedCenterXHalf - (footprintWidthHalf / 2);
        int minHalfZ = snappedCenterZHalf - (footprintDepthHalf / 2);
        int maxHalfX = Mathf.Max(0, halfGridWidth - footprintWidthHalf);
        int maxHalfZ = Mathf.Max(0, halfGridDepth - footprintDepthHalf);

        if (snappedCenterXHalf < 0 || snappedCenterZHalf < 0 || snappedCenterXHalf > halfGridWidth || snappedCenterZHalf > halfGridDepth)
        {
            message = "Cursor is outside the block grid.";
            return false;
        }

        candidate.FloorWidthHalf = footprintWidthHalf;
        candidate.FloorDepthHalf = footprintDepthHalf;
        candidate.FloorGridXHalf = Mathf.Clamp(minHalfX, 0, maxHalfX);
        candidate.FloorGridZHalf = Mathf.Clamp(minHalfZ, 0, maxHalfZ);
        candidate.GridX = Mathf.FloorToInt(candidate.FloorGridXHalf * 0.5f);
        candidate.GridZ = Mathf.FloorToInt(candidate.FloorGridZHalf * 0.5f);
        return true;
    }

    private bool TryBuildWallPlacementCandidateFromScenePoint(
        MemorySpaceBlock block,
        Vector2 mousePosition,
        ref PlacementCandidate candidate,
        out string message)
    {
        message = string.Empty;
        hoveredWallLayerIndex = 0;
        hoveredWallLayerCount = 1;
        hoveredWallSurfaceHeight = 0f;

        if (!TryPickWallSurfaceContext(block, mousePosition, out WallSurfaceContext surfaceContext, out message))
        {
            DebugWallPlacement("WallCandidateFailed", message);
            return false;
        }

        hoveredWallSurfaceHeight = surfaceContext.SurfaceHeight;
        hoveredWallLayerCount = surfaceContext.LayerCount;

        int gridWidth = RoomSlotGridUtility.GetGridWidth(block);
        int gridDepth = RoomSlotGridUtility.GetGridDepth(block);
        float gridSize = RoomSlotGridUtility.GetGridSize(block);
        float halfWidth = gridWidth * gridSize * 0.5f;
        float halfDepth = gridDepth * gridSize * 0.5f;
        int wallSpan = RoomSlotGridUtility.GetWallSlotCount(block, surfaceContext.Side);
        int maxAnchor = Mathf.Max(0, wallSpan - Mathf.Max(1, candidate.WidthUnits));

        candidate.WallSide = surfaceContext.Side;
        if (surfaceContext.WidthUnits > 0)
        {
            int placementStart = surfaceContext.WallGridPosition;
            int placementWidth = surfaceContext.WidthUnits;
            int rawWallGridPosition = GetWallGridPositionFromLocalHitPoint(block, surfaceContext.Side, surfaceContext.LocalHitPoint);
            int placementMaxAnchor = placementStart + Mathf.Max(0, placementWidth - Mathf.Max(1, candidate.WidthUnits));
            candidate.WallGridPosition = Mathf.Clamp(
                rawWallGridPosition,
                placementStart,
                Mathf.Min(maxAnchor, placementMaxAnchor));
        }
        else
        {
            switch (surfaceContext.Side)
            {
                case WallSide.North:
                case WallSide.South:
                    candidate.WallGridPosition = Mathf.Clamp(
                        Mathf.FloorToInt((surfaceContext.LocalHitPoint.x + halfWidth) / gridSize),
                        0,
                        maxAnchor);
                    break;
                case WallSide.East:
                case WallSide.West:
                    candidate.WallGridPosition = Mathf.Clamp(
                        Mathf.FloorToInt((surfaceContext.LocalHitPoint.z + halfDepth) / gridSize),
                        0,
                        maxAnchor);
                    break;
            }
        }

        float layerHeight = surfaceContext.LayerCount > 0
            ? surfaceContext.SurfaceHeight / surfaceContext.LayerCount
            : surfaceContext.SurfaceHeight;
        candidate.WallLayerIndex = Mathf.Clamp(
            Mathf.FloorToInt(Mathf.Clamp(surfaceContext.LocalHitPoint.y, 0f, Mathf.Max(0f, surfaceContext.SurfaceHeight - 0.0001f)) / Mathf.Max(0.0001f, layerHeight)),
            0,
            Mathf.Max(0, surfaceContext.LayerCount - 1));
        candidate.WallLayerCount = surfaceContext.LayerCount;
        candidate.WallSurfaceHeight = surfaceContext.SurfaceHeight;
        hoveredWallLayerIndex = candidate.WallLayerIndex;

        DebugWallPlacement(
            "WallSurfacePicked",
            $"side={surfaceContext.Side} hit={surfaceContext.LocalHitPoint} surfaceHeight={surfaceContext.SurfaceHeight:0.###} layers={surfaceContext.LayerCount} pickedLayer={candidate.WallLayerIndex + 1}");

        if (!TryResolveWallPlacementCandidate(block, ref candidate, out message))
        {
            DebugWallPlacement("WallResolveFailed", message);
            return false;
        }

        DebugWallPlacement(
            "WallCandidateReady",
            $"side={candidate.WallSide} wallGridPos={candidate.WallGridPosition} grid=({candidate.GridX},{candidate.GridZ}) layer={candidate.WallLayerIndex + 1}/{Mathf.Max(1, candidate.WallLayerCount)} heightOffset={candidate.HeightOffset:0.###}");

        return true;
    }

    private bool TryResolveWallPlacementCandidate(
        MemorySpaceBlock block,
        ref PlacementCandidate candidate,
        out string message)
    {
        message = string.Empty;
        if (block == null)
        {
            message = "Target MemorySpaceBlock is missing.";
            return false;
        }

        if (!RoomSlotGridUtility.TryGetWallAnchorGrid(
            block,
            candidate.WallSide,
            candidate.WallGridPosition,
            candidate.WidthUnits,
            out int resolvedGridX,
            out int resolvedGridZ))
        {
            message = $"Wall placement is out of bounds: {candidate.WallSide} [{candidate.WallGridPosition}] span {candidate.WidthUnits}.";
            return false;
        }

        List<string> wallEdgeKeys = RoomSlotGridUtility.GetWallEdgeKeys(resolvedGridX, resolvedGridZ, candidate.WallSide, candidate.WidthUnits);
        float resolvedHeight = 0f;
        int resolvedLayerCount = 0;
        for (int i = 0; i < wallEdgeKeys.Count; i++)
        {
            if (!TryGetWallSurfaceContextForEdge(block, wallEdgeKeys[i], out WallSurfaceContext edgeContext, out string edgeMessage))
            {
                message = edgeMessage;
                return false;
            }

            if (resolvedLayerCount == 0)
            {
                resolvedHeight = edgeContext.SurfaceHeight;
                resolvedLayerCount = edgeContext.LayerCount;
            }
            else if (!ApproximatelyWallHeight(resolvedHeight, edgeContext.SurfaceHeight) || resolvedLayerCount != edgeContext.LayerCount)
            {
                message = "The selected wall span crosses different wall heights, so this slot cannot be painted as one placement.";
                return false;
            }
        }

        if (resolvedLayerCount <= 0)
        {
            message = "The selected wall surface does not expose a valid placement layer.";
            return false;
        }

        if (candidate.WallLayerIndex >= resolvedLayerCount)
        {
            message = $"This wall only supports {resolvedLayerCount} layer(s).";
            return false;
        }

        candidate.GridX = resolvedGridX;
        candidate.GridZ = resolvedGridZ;
        candidate.WallLayerCount = resolvedLayerCount;
        candidate.WallSurfaceHeight = resolvedHeight;
        candidate.HeightOffset = resolvedLayerCount > 0
            ? candidate.WallLayerIndex * (resolvedHeight / resolvedLayerCount)
            : 0f;
        hoveredWallLayerCount = resolvedLayerCount;
        hoveredWallSurfaceHeight = resolvedHeight;
        return true;
    }

    private bool TryPickWallSurfaceContext(
        MemorySpaceBlock block,
        Vector2 mousePosition,
        out WallSurfaceContext context,
        out string message)
    {
        context = null;
        message = string.Empty;
        if (block == null)
        {
            message = "Select a MemorySpaceBlock target first.";
            return false;
        }

        if (TryFindWallSurfaceContextFromPlacements(block, mousePosition, out context, out message))
        {
            DebugWallPlacement(
                "WallContext",
                $"direct placement hit side={context.Side} surfaceHeight={context.SurfaceHeight:0.###} layers={context.LayerCount} localHit={context.LocalHitPoint}");
            return true;
        }

        bool fallbackResult = TryFindWallSurfaceContextFromProjectedPlanePoint(block, mousePosition, out context, out message);
        if (fallbackResult)
        {
            DebugWallPlacement(
                "WallContext",
                $"fallback projected hit side={context.Side} surfaceHeight={context.SurfaceHeight:0.###} layers={context.LayerCount} localHit={context.LocalHitPoint}");
        }
        else
        {
            DebugWallPlacement("WallContextFailed", message);
        }

        return fallbackResult;
    }

    private bool TryFindWallSurfaceContextFromPlacements(
        MemorySpaceBlock block,
        Vector2 mousePosition,
        out WallSurfaceContext context,
        out string message)
    {
        context = null;
        message = "Hover a painted wall segment to place a wall slot.";
        if (block == null)
        {
            return false;
        }

        Event currentEvent = Event.current;
        bool canUseScenePick = IsScenePointerEvent(currentEvent);

        if (!canUseScenePick
            && hasCachedHoveredWallSurfaceContext
            && TryGetCachedWallSurfaceContext(block, out context))
        {
            message = string.Empty;
            return true;
        }

        if (canUseScenePick && TryPickSegmentPlacementGameObjectUnderCursor(block, mousePosition, out SpaceSegmentPlacementMetadata pickedPlacement))
        {
            if (TryBuildWallSurfaceContextFromPlacement(
                    block,
                    pickedPlacement,
                    mousePosition,
                    out context,
                    out message))
            {
                CacheWallSurfaceContext(context);
                DebugWallPlacement(
                    "PlacementScenePickHit",
                    $"placementId={pickedPlacement.record.placementId} side={pickedPlacement.record.side} source=HandleUtility.PickGameObject");
                return true;
            }

            ClearCachedWallSurfaceContext();
            DebugWallPlacement("PlacementScenePickRejected", message);
        }

        if (canUseScenePick && TryPickWallSegmentSlotGameObjectUnderCursor(block, mousePosition, out WallSegmentSlot pickedWallSlot))
        {
            if (TryBuildWallSurfaceContextFromWallSlot(
                    block,
                    pickedWallSlot,
                    mousePosition,
                    out context,
                    out message))
            {
                CacheWallSurfaceContext(context);
                DebugWallPlacement(
                    "WallSlotScenePickHit",
                    $"side={pickedWallSlot.side} segmentId={pickedWallSlot.segmentId} source=HandleUtility.PickGameObject");
                return true;
            }

            ClearCachedWallSurfaceContext();
            DebugWallPlacement("WallSlotScenePickRejected", message);
        }

        if (TryRaycastSegmentPlacementUnderCursor(block, mousePosition, out SpaceSegmentPlacementMetadata hoveredPlacement, out RaycastHit hoveredHit))
        {
            Vector3 explicitLocalHitPoint = block.transform.InverseTransformPoint(hoveredHit.point);
            if (TryBuildWallSurfaceContextFromPlacement(
                    block,
                    hoveredPlacement,
                    mousePosition,
                    out context,
                    out message,
                    true,
                    explicitLocalHitPoint))
            {
                CacheWallSurfaceContext(context);
                DebugWallPlacement(
                    "PlacementRaycastHit",
                    $"placementId={hoveredPlacement.record.placementId} side={hoveredPlacement.record.side} worldHit={hoveredHit.point} localHit={explicitLocalHitPoint}");
                return true;
            }

            ClearCachedWallSurfaceContext();
            DebugWallPlacement("PlacementRaycastRejected", message);
        }

        if (TryRaycastWallSegmentSlotUnderCursor(block, mousePosition, out WallSegmentSlot hoveredWallSlot, out RaycastHit hoveredWallHit))
        {
            Vector3 explicitLocalHitPoint = block.transform.InverseTransformPoint(hoveredWallHit.point);
            if (TryBuildWallSurfaceContextFromWallSlot(
                    block,
                    hoveredWallSlot,
                    mousePosition,
                    out context,
                    out message,
                    true,
                    explicitLocalHitPoint))
            {
                CacheWallSurfaceContext(context);
                DebugWallPlacement(
                    "WallSlotRaycastHit",
                    $"side={hoveredWallSlot.side} segmentId={hoveredWallSlot.segmentId} worldHit={hoveredWallHit.point} localHit={explicitLocalHitPoint}");
                return true;
            }

            ClearCachedWallSurfaceContext();
            DebugWallPlacement("WallSlotRaycastRejected", message);
        }

        SpaceSegmentPlacementMetadata[] placements = block.GetComponentsInChildren<SpaceSegmentPlacementMetadata>(true);
        if (placements == null || placements.Length == 0)
        {
            return TryFindWallSurfaceContextFromWallSlots(block, mousePosition, out context, out message);
        }

        Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);
        float bestDistance = float.MaxValue;
        bool foundWallHit = false;

        for (int i = 0; i < placements.Length; i++)
        {
            SpaceSegmentPlacementMetadata placement = placements[i];
            if (placement == null || placement.record == null || placement.record.category != SegmentCategory.Wall)
            {
                continue;
            }

            SpaceSegmentDefinition definition = placement.definition;
            if (definition == null && block.segmentKit != null && !string.IsNullOrWhiteSpace(placement.record.segmentId))
            {
                definition = block.segmentKit.GetSegment(placement.record.segmentId);
            }

            if (definition == null || definition.category != SegmentCategory.Wall)
            {
                continue;
            }

            int widthUnits = Mathf.Max(1, placement.record.footprint.x);
            int wallGridPosition = GetWallGridPosition(placement.record);
            Vector3 wallBaseLocalPosition = RoomSlotGridUtility.GetWallLocalPosition(
                block,
                placement.record.side,
                wallGridPosition,
                widthUnits,
                0f);

            if (!TryIntersectWallPlacementSurface(
                    block,
                    placement.record.side,
                    wallBaseLocalPosition,
                    widthUnits,
                    ray,
                    out Vector3 localHitPoint,
                    out float hitDistance))
            {
                continue;
            }

            float surfaceHeight = ResolveWallSurfaceHeight(definition);
            if (localHitPoint.y < 0f || localHitPoint.y > surfaceHeight)
            {
                continue;
            }

            foundWallHit = true;
            if (hitDistance >= bestDistance)
            {
                continue;
            }

            bestDistance = hitDistance;

            WallSegmentSlot wallSlot = placement.GetComponent<WallSegmentSlot>();
            bool hasOverlay = placement.overlayDefinition != null
                || (wallSlot != null && !string.IsNullOrWhiteSpace(wallSlot.overlayId));
            if (hasOverlay)
            {
                context = null;
                message = "Window/opening wall segments cannot host room slots.";
                continue;
            }

            int layerCount = GetWallLayerCountForHeight(surfaceHeight);
            if (layerCount <= 0)
            {
                context = null;
                message = $"Unsupported wall height {surfaceHeight:0.###}. Only {HalfWallHeight:0.##} and {FullWallHeight:0.##} are paintable.";
                continue;
            }

            context = new WallSurfaceContext
            {
                PlacementMetadata = placement,
                Definition = definition,
                WallSlot = wallSlot,
                Side = placement.record.side,
                WallGridPosition = wallGridPosition,
                WidthUnits = widthUnits,
                LocalHitPoint = localHitPoint,
                SurfaceHeight = surfaceHeight,
                LayerCount = layerCount
            };
            message = string.Empty;
        }

        if (context != null)
        {
            CacheWallSurfaceContext(context);
            DebugWallPlacement(
                "PlacementScanHit",
                $"side={context.Side} surfaceHeight={context.SurfaceHeight:0.###} layers={context.LayerCount} localHit={context.LocalHitPoint}");
            return true;
        }

        if (TryFindWallSurfaceContextFromWallSlots(block, mousePosition, out context, out message))
        {
            CacheWallSurfaceContext(context);
            DebugWallPlacement(
                "WallSlotScanHit",
                $"side={context.Side} wallGridPos={context.WallGridPosition} width={context.WidthUnits} surfaceHeight={context.SurfaceHeight:0.###} layers={context.LayerCount} localHit={context.LocalHitPoint}");
            return true;
        }

        if (!foundWallHit)
        {
            message = "Hover a painted wall segment to place a wall slot.";
        }
        else
        {
            DebugWallPlacement("PlacementScanRejected", message);
        }

        return false;
    }

    private static bool IsScenePointerEvent(Event current)
    {
        if (current == null)
        {
            return false;
        }

        switch (current.type)
        {
            case EventType.MouseMove:
            case EventType.MouseDrag:
            case EventType.MouseDown:
            case EventType.MouseUp:
                return true;
            default:
                return false;
        }
    }

    private bool TryGetCachedWallSurfaceContext(MemorySpaceBlock block, out WallSurfaceContext context)
    {
        context = null;
        if (!hasCachedHoveredWallSurfaceContext || cachedHoveredWallSurfaceContext == null)
        {
            return false;
        }

        SpaceSegmentPlacementMetadata placement = cachedHoveredWallSurfaceContext.PlacementMetadata;
        if (placement == null || placement.record == null || placement.GetComponentInParent<MemorySpaceBlock>() != block)
        {
            ClearCachedWallSurfaceContext();
            return false;
        }

        context = CloneWallSurfaceContext(cachedHoveredWallSurfaceContext);
        return context != null;
    }

    private void CacheWallSurfaceContext(WallSurfaceContext context)
    {
        cachedHoveredWallSurfaceContext = CloneWallSurfaceContext(context);
        hasCachedHoveredWallSurfaceContext = cachedHoveredWallSurfaceContext != null;
    }

    private void ClearCachedWallSurfaceContext()
    {
        cachedHoveredWallSurfaceContext = null;
        hasCachedHoveredWallSurfaceContext = false;
    }

    private static WallSurfaceContext CloneWallSurfaceContext(WallSurfaceContext source)
    {
        if (source == null)
        {
            return null;
        }

        return new WallSurfaceContext
        {
            PlacementMetadata = source.PlacementMetadata,
            Definition = source.Definition,
            WallSlot = source.WallSlot,
            Side = source.Side,
            WallGridPosition = source.WallGridPosition,
            WidthUnits = source.WidthUnits,
            LocalHitPoint = source.LocalHitPoint,
            SurfaceHeight = source.SurfaceHeight,
            LayerCount = source.LayerCount
        };
    }

    private bool TryFindWallSurfaceContextFromProjectedPlanePoint(
        MemorySpaceBlock block,
        Vector2 mousePosition,
        out WallSurfaceContext context,
        out string message)
    {
        context = null;
        message = "Hover a painted wall segment to place a wall slot.";
        if (block == null)
        {
            return false;
        }

        if (!TryRaycastAuthoringPlane(block.transform, mousePosition, out Vector3 localPlanePoint))
        {
            return false;
        }

        Vector3 clampedPlanePoint = ClampLocalPointToBlockBounds(block, localPlanePoint);
        DebugWallPlacement("ProjectedPlanePoint", $"raw={localPlanePoint} clamped={clampedPlanePoint}");

        if (!TryFindNearestWallPlacementMetadata(block, clampedPlanePoint, out SpaceSegmentPlacementMetadata placement))
        {
            DebugWallPlacement("ProjectedPlaneNoWall", $"planePoint={localPlanePoint} clamped={clampedPlanePoint}");
            return false;
        }

        DebugWallPlacement(
            "ProjectedPlaneNearestWall",
            $"planePoint={localPlanePoint} clamped={clampedPlanePoint} placementId={placement.record.placementId} side={placement.record.side} grid=({placement.record.gridX},{placement.record.gridZ})");
        return TryBuildWallSurfaceContextFromPlacement(block, placement, mousePosition, out context, out message);
    }

    private bool TryBuildWallSurfaceContextFromPlacement(
        MemorySpaceBlock block,
        SpaceSegmentPlacementMetadata placementMetadata,
        Vector2 mousePosition,
        out WallSurfaceContext context,
        out string message,
        bool hasExplicitLocalHitPoint = false,
        Vector3 explicitLocalHitPoint = default)
    {
        context = null;
        message = string.Empty;
        if (block == null || placementMetadata == null || placementMetadata.record == null || placementMetadata.record.category != SegmentCategory.Wall)
        {
            message = "Wall slot painting only works on real wall segments inside the selected block.";
            return false;
        }

        SpaceSegmentDefinition definition = placementMetadata.definition;
        if (definition == null && block.segmentKit != null && !string.IsNullOrWhiteSpace(placementMetadata.record.segmentId))
        {
            definition = block.segmentKit.GetSegment(placementMetadata.record.segmentId);
        }

        if (definition == null || definition.category != SegmentCategory.Wall)
        {
            message = "The hovered surface is not backed by a valid wall segment definition.";
            return false;
        }

        WallSegmentSlot wallSlot = placementMetadata.GetComponent<WallSegmentSlot>();
        bool hasOverlay = placementMetadata.overlayDefinition != null
            || (wallSlot != null && !string.IsNullOrWhiteSpace(wallSlot.overlayId));
        if (hasOverlay)
        {
            message = "Window/opening wall segments cannot host room slots.";
            return false;
        }

        float surfaceHeight = ResolveWallSurfaceHeight(definition);
        int layerCount = GetWallLayerCountForHeight(surfaceHeight);
        if (layerCount <= 0)
        {
            message = $"Unsupported wall height {surfaceHeight:0.###}. Only {HalfWallHeight:0.##} and {FullWallHeight:0.##} are paintable.";
            return false;
        }

        int widthUnits = Mathf.Max(1, placementMetadata.record.footprint.x);
        int wallGridPosition = GetWallGridPosition(placementMetadata.record);
        Vector3 wallBaseLocalPosition = RoomSlotGridUtility.GetWallLocalPosition(
            block,
            placementMetadata.record.side,
            wallGridPosition,
            widthUnits,
            0f);

        Vector3 localHitPoint;
        if (hasExplicitLocalHitPoint)
        {
            localHitPoint = explicitLocalHitPoint;
        }
        else if (!TryRaycastWallSurfaceLocalPoint(
                     block,
                     placementMetadata.record.side,
                     block.transform.TransformPoint(wallBaseLocalPosition),
                     mousePosition,
                     out localHitPoint))
        {
            message = "Could not raycast onto the hovered wall surface.";
            return false;
        }

        localHitPoint = ConstrainLocalHitPointToWallSurface(
            block,
            placementMetadata.record.side,
            wallBaseLocalPosition,
            widthUnits,
            surfaceHeight,
            localHitPoint);

        context = new WallSurfaceContext
        {
            PlacementMetadata = placementMetadata,
            Definition = definition,
            WallSlot = wallSlot,
            Side = placementMetadata.record.side,
            WallGridPosition = wallGridPosition,
            WidthUnits = widthUnits,
            LocalHitPoint = localHitPoint,
            SurfaceHeight = surfaceHeight,
            LayerCount = layerCount
        };
        message = string.Empty;
        DebugWallPlacement(
            "BuildContextFromPlacement",
            $"placementId={placementMetadata.record.placementId} side={placementMetadata.record.side} localHit={localHitPoint} surfaceHeight={surfaceHeight:0.###} layers={layerCount}");
        return true;
    }

    private bool TryFindWallSurfaceContextFromWallSlots(
        MemorySpaceBlock block,
        Vector2 mousePosition,
        out WallSurfaceContext context,
        out string message)
    {
        context = null;
        message = "Hover a painted wall segment to place a wall slot.";
        if (block == null || block.wallSegments == null || block.wallSegments.Count == 0)
        {
            return false;
        }

        Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);
        float bestDistance = float.MaxValue;

        for (int i = 0; i < block.wallSegments.Count; i++)
        {
            WallSegmentSlot wallSlot = block.wallSegments[i];
            if (!TryResolveWallSlotPlacement(block, wallSlot, out SpaceSegmentDefinition definition, out int wallGridPosition, out int widthUnits, out float surfaceHeight, out int layerCount, out string resolveMessage))
            {
                if (!string.IsNullOrWhiteSpace(resolveMessage))
                {
                    message = resolveMessage;
                }

                continue;
            }

            Vector3 wallBaseLocalPosition = RoomSlotGridUtility.GetWallLocalPosition(
                block,
                wallSlot.side,
                wallGridPosition,
                widthUnits,
                0f);

            if (!TryIntersectWallPlacementSurface(
                    block,
                    wallSlot.side,
                    wallBaseLocalPosition,
                    widthUnits,
                    ray,
                    out Vector3 localHitPoint,
                    out float hitDistance))
            {
                continue;
            }

            if (localHitPoint.y < 0f || localHitPoint.y > surfaceHeight || hitDistance >= bestDistance)
            {
                continue;
            }

            bestDistance = hitDistance;
            context = new WallSurfaceContext
            {
                PlacementMetadata = null,
                Definition = definition,
                WallSlot = wallSlot,
                Side = wallSlot.side,
                WallGridPosition = wallGridPosition,
                WidthUnits = widthUnits,
                LocalHitPoint = localHitPoint,
                SurfaceHeight = surfaceHeight,
                LayerCount = layerCount
            };
            message = string.Empty;
        }

        return context != null;
    }

    private bool TryBuildWallSurfaceContextFromWallSlot(
        MemorySpaceBlock block,
        WallSegmentSlot wallSlot,
        Vector2 mousePosition,
        out WallSurfaceContext context,
        out string message,
        bool hasExplicitLocalHitPoint = false,
        Vector3 explicitLocalHitPoint = default)
    {
        context = null;
        message = string.Empty;
        if (!TryResolveWallSlotPlacement(block, wallSlot, out SpaceSegmentDefinition definition, out int wallGridPosition, out int widthUnits, out float surfaceHeight, out int layerCount, out message))
        {
            return false;
        }

        Vector3 wallBaseLocalPosition = RoomSlotGridUtility.GetWallLocalPosition(
            block,
            wallSlot.side,
            wallGridPosition,
            widthUnits,
            0f);

        Vector3 localHitPoint;
        if (hasExplicitLocalHitPoint)
        {
            localHitPoint = explicitLocalHitPoint;
        }
        else if (!TryRaycastWallSurfaceLocalPoint(
                     block,
                     wallSlot.side,
                     block.transform.TransformPoint(wallBaseLocalPosition),
                     mousePosition,
                     out localHitPoint))
        {
            message = "Could not raycast onto the hovered baked wall surface.";
            return false;
        }

        localHitPoint = ConstrainLocalHitPointToWallSurface(
            block,
            wallSlot.side,
            wallBaseLocalPosition,
            widthUnits,
            surfaceHeight,
            localHitPoint);

        context = new WallSurfaceContext
        {
            PlacementMetadata = null,
            Definition = definition,
            WallSlot = wallSlot,
            Side = wallSlot.side,
            WallGridPosition = wallGridPosition,
            WidthUnits = widthUnits,
            LocalHitPoint = localHitPoint,
            SurfaceHeight = surfaceHeight,
            LayerCount = layerCount
        };
        message = string.Empty;
        DebugWallPlacement(
            "BuildContextFromWallSlot",
            $"side={wallSlot.side} wallGridPos={wallGridPosition} width={widthUnits} localHit={localHitPoint} surfaceHeight={surfaceHeight:0.###} layers={layerCount} segmentId={wallSlot.segmentId}");
        return true;
    }

    private bool TryResolveWallSlotPlacement(
        MemorySpaceBlock block,
        WallSegmentSlot wallSlot,
        out SpaceSegmentDefinition definition,
        out int wallGridPosition,
        out int widthUnits,
        out float surfaceHeight,
        out int layerCount,
        out string message)
    {
        definition = null;
        wallGridPosition = 0;
        widthUnits = 0;
        surfaceHeight = 0f;
        layerCount = 0;
        message = string.Empty;

        if (block == null || wallSlot == null)
        {
            message = "Wall slot data is missing.";
            return false;
        }

        if (block.segmentKit == null || string.IsNullOrWhiteSpace(wallSlot.segmentId))
        {
            message = "The baked wall slot is missing its segment definition.";
            return false;
        }

        definition = block.segmentKit.GetSegment(wallSlot.segmentId);
        if (definition == null || definition.category != SegmentCategory.Wall)
        {
            message = $"Missing valid wall definition for baked slot: {wallSlot.segmentId}.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(wallSlot.overlayId))
        {
            message = "Window/opening wall segments cannot host room slots.";
            return false;
        }

        surfaceHeight = ResolveWallSurfaceHeight(definition);
        layerCount = GetWallLayerCountForHeight(surfaceHeight);
        if (layerCount <= 0)
        {
            message = $"Unsupported wall height {surfaceHeight:0.###}. Only {HalfWallHeight:0.##} and {FullWallHeight:0.##} are paintable.";
            return false;
        }

        widthUnits = ResolveWallDefinitionWidthUnits(definition);
        if (!TryResolveBakedWallGridPosition(block, wallSlot, widthUnits, out wallGridPosition))
        {
            message = $"Could not resolve a grid position for baked wall slot {wallSlot.segmentId}.";
            return false;
        }

        return true;
    }

    private static Vector3 ConstrainLocalHitPointToWallSurface(
        MemorySpaceBlock block,
        WallSide wallSide,
        Vector3 wallBaseLocalPosition,
        int widthUnits,
        float surfaceHeight,
        Vector3 localHitPoint)
    {
        if (block == null)
        {
            return localHitPoint;
        }

        float gridSize = RoomSlotGridUtility.GetGridSize(block);
        float halfLength = Mathf.Max(1, widthUnits) * gridSize * 0.5f;

        switch (wallSide)
        {
            case WallSide.North:
            case WallSide.South:
                localHitPoint.x = Mathf.Clamp(localHitPoint.x, wallBaseLocalPosition.x - halfLength, wallBaseLocalPosition.x + halfLength);
                localHitPoint.z = wallBaseLocalPosition.z;
                break;

            case WallSide.East:
            case WallSide.West:
                localHitPoint.x = wallBaseLocalPosition.x;
                localHitPoint.z = Mathf.Clamp(localHitPoint.z, wallBaseLocalPosition.z - halfLength, wallBaseLocalPosition.z + halfLength);
                break;
        }

        localHitPoint.y = Mathf.Clamp(localHitPoint.y, 0f, Mathf.Max(0f, surfaceHeight));
        return localHitPoint;
    }

    private static int GetWallGridPosition(SpaceSegmentPlacementRecord record)
    {
        if (record == null)
        {
            return 0;
        }

        return record.side == WallSide.North || record.side == WallSide.South
            ? record.gridX
            : record.gridZ;
    }

    private bool TryFindNearestWallPlacementMetadata(
        MemorySpaceBlock block,
        Vector3 localPoint,
        out SpaceSegmentPlacementMetadata metadata)
    {
        metadata = null;
        if (block == null)
        {
            return false;
        }

        float gridSize = RoomSlotGridUtility.GetGridSize(block);
        float bestDistanceSqr = float.MaxValue;
        float maxDistance = gridSize * 3f;
        float maxDistanceSqr = maxDistance * maxDistance;
        SpaceSegmentPlacementMetadata[] placements = block.GetComponentsInChildren<SpaceSegmentPlacementMetadata>(true);

        for (int i = 0; i < placements.Length; i++)
        {
            SpaceSegmentPlacementMetadata candidate = placements[i];
            if (candidate == null || candidate.record == null || candidate.record.category != SegmentCategory.Wall)
            {
                continue;
            }

            int widthUnits = Mathf.Max(1, candidate.record.footprint.x);
            int wallGridPosition = GetWallGridPosition(candidate.record);
            Vector3 wallBaseLocalPosition = RoomSlotGridUtility.GetWallLocalPosition(
                block,
                candidate.record.side,
                wallGridPosition,
                widthUnits,
                0f);
            float halfLength = widthUnits * gridSize * 0.5f;

            float distanceSqr;
            switch (candidate.record.side)
            {
                case WallSide.North:
                case WallSide.South:
                {
                    float clampedX = Mathf.Clamp(localPoint.x, wallBaseLocalPosition.x - halfLength, wallBaseLocalPosition.x + halfLength);
                    Vector2 nearest = new Vector2(clampedX, wallBaseLocalPosition.z);
                    distanceSqr = (new Vector2(localPoint.x, localPoint.z) - nearest).sqrMagnitude;
                    break;
                }
                case WallSide.East:
                case WallSide.West:
                {
                    float clampedZ = Mathf.Clamp(localPoint.z, wallBaseLocalPosition.z - halfLength, wallBaseLocalPosition.z + halfLength);
                    Vector2 nearest = new Vector2(wallBaseLocalPosition.x, clampedZ);
                    distanceSqr = (new Vector2(localPoint.x, localPoint.z) - nearest).sqrMagnitude;
                    break;
                }
                default:
                    continue;
            }

            if (distanceSqr > maxDistanceSqr || distanceSqr >= bestDistanceSqr)
            {
                continue;
            }

            bestDistanceSqr = distanceSqr;
            metadata = candidate;
        }

        return metadata != null;
    }

    private static int GetWallGridPositionFromLocalHitPoint(
        MemorySpaceBlock block,
        WallSide side,
        Vector3 localHitPoint)
    {
        if (block == null)
        {
            return 0;
        }

        int gridWidth = RoomSlotGridUtility.GetGridWidth(block);
        int gridDepth = RoomSlotGridUtility.GetGridDepth(block);
        float gridSize = RoomSlotGridUtility.GetGridSize(block);
        float halfWidth = gridWidth * gridSize * 0.5f;
        float halfDepth = gridDepth * gridSize * 0.5f;

        switch (side)
        {
            case WallSide.North:
            case WallSide.South:
                return Mathf.FloorToInt((localHitPoint.x + halfWidth) / gridSize);
            case WallSide.East:
            case WallSide.West:
                return Mathf.FloorToInt((localHitPoint.z + halfDepth) / gridSize);
            default:
                return 0;
        }
    }

    private static Vector3 ClampLocalPointToBlockBounds(MemorySpaceBlock block, Vector3 localPoint)
    {
        if (block == null)
        {
            return localPoint;
        }

        int gridWidth = RoomSlotGridUtility.GetGridWidth(block);
        int gridDepth = RoomSlotGridUtility.GetGridDepth(block);
        float gridSize = RoomSlotGridUtility.GetGridSize(block);
        float halfWidth = gridWidth * gridSize * 0.5f;
        float halfDepth = gridDepth * gridSize * 0.5f;

        localPoint.x = Mathf.Clamp(localPoint.x, -halfWidth, halfWidth);
        localPoint.z = Mathf.Clamp(localPoint.z, -halfDepth, halfDepth);
        return localPoint;
    }

    private static bool TryIntersectWallPlacementSurface(
        MemorySpaceBlock block,
        WallSide wallSide,
        Vector3 wallBaseLocalPosition,
        int widthUnits,
        Ray ray,
        out Vector3 localHitPoint,
        out float hitDistance)
    {
        localHitPoint = Vector3.zero;
        hitDistance = 0f;
        if (block == null)
        {
            return false;
        }

        Vector3 planePointWorld = block.transform.TransformPoint(wallBaseLocalPosition);
        Vector3 worldNormal = block.transform.TransformDirection(GetWallPlaneLocalNormal(wallSide));
        Plane plane = new Plane(worldNormal, planePointWorld);
        if (!plane.Raycast(ray, out hitDistance) || hitDistance < 0f)
        {
            return false;
        }

        localHitPoint = block.transform.InverseTransformPoint(ray.GetPoint(hitDistance));

        float gridSize = RoomSlotGridUtility.GetGridSize(block);
        float halfLength = Mathf.Max(1, widthUnits) * gridSize * 0.5f;
        float tolerance = Mathf.Max(0.02f, gridSize * 0.08f);

        switch (wallSide)
        {
            case WallSide.North:
            case WallSide.South:
                return Mathf.Abs(localHitPoint.z - wallBaseLocalPosition.z) <= tolerance
                    && localHitPoint.x >= wallBaseLocalPosition.x - halfLength - tolerance
                    && localHitPoint.x <= wallBaseLocalPosition.x + halfLength + tolerance;

            case WallSide.East:
            case WallSide.West:
                return Mathf.Abs(localHitPoint.x - wallBaseLocalPosition.x) <= tolerance
                    && localHitPoint.z >= wallBaseLocalPosition.z - halfLength - tolerance
                    && localHitPoint.z <= wallBaseLocalPosition.z + halfLength + tolerance;

            default:
                return false;
        }
    }

    private static int CompareRaycastHitDistance(RaycastHit a, RaycastHit b)
    {
        return a.distance.CompareTo(b.distance);
    }

    private static bool TryRaycastRoomSlotPlacementUnderCursor(
        MemorySpaceBlock block,
        Vector2 mousePosition,
        out RoomSlotPlacementMetadata metadata,
        out RaycastHit hit)
    {
        metadata = null;
        hit = default;
        if (block == null)
        {
            return false;
        }

        Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, 1000f, ~0, QueryTriggerInteraction.Collide);
        if (hits == null || hits.Length == 0)
        {
            return false;
        }

        Array.Sort(hits, CompareRaycastHitDistance);
        for (int i = 0; i < hits.Length; i++)
        {
            RoomSlotPlacementMetadata candidate = hits[i].collider != null
                ? hits[i].collider.GetComponentInParent<RoomSlotPlacementMetadata>()
                : null;
            if (candidate == null || candidate.GetComponentInParent<MemorySpaceBlock>() != block)
            {
                continue;
            }

            metadata = candidate;
            hit = hits[i];
            return true;
        }

        return false;
    }

    private static bool TryPickSegmentPlacementGameObjectUnderCursor(
        MemorySpaceBlock block,
        Vector2 mousePosition,
        out SpaceSegmentPlacementMetadata metadata)
    {
        metadata = null;
        if (block == null)
        {
            return false;
        }

        GameObject pickedObject = HandleUtility.PickGameObject(mousePosition, false);
        if (pickedObject == null)
        {
            return false;
        }

        SpaceSegmentPlacementMetadata candidate = pickedObject.GetComponentInParent<SpaceSegmentPlacementMetadata>();
        if (candidate == null || candidate.GetComponentInParent<MemorySpaceBlock>() != block)
        {
            return false;
        }

        metadata = candidate;
        return true;
    }

    private static bool TryPickWallSegmentSlotGameObjectUnderCursor(
        MemorySpaceBlock block,
        Vector2 mousePosition,
        out WallSegmentSlot wallSlot)
    {
        wallSlot = null;
        if (block == null)
        {
            return false;
        }

        GameObject pickedObject = HandleUtility.PickGameObject(mousePosition, false);
        if (pickedObject == null)
        {
            return false;
        }

        WallSegmentSlot candidate = pickedObject.GetComponentInParent<WallSegmentSlot>();
        if (candidate == null || candidate.GetComponentInParent<MemorySpaceBlock>() != block)
        {
            return false;
        }

        wallSlot = candidate;
        return true;
    }

    private static bool TryRaycastSegmentPlacementUnderCursor(
        MemorySpaceBlock block,
        Vector2 mousePosition,
        out SpaceSegmentPlacementMetadata metadata,
        out RaycastHit hit)
    {
        metadata = null;
        hit = default;
        if (block == null)
        {
            return false;
        }

        Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, 1000f, ~0, QueryTriggerInteraction.Collide);
        if (hits == null || hits.Length == 0)
        {
            return false;
        }

        Array.Sort(hits, CompareRaycastHitDistance);
        for (int i = 0; i < hits.Length; i++)
        {
            SpaceSegmentPlacementMetadata candidate = hits[i].collider != null
                ? hits[i].collider.GetComponentInParent<SpaceSegmentPlacementMetadata>()
                : null;
            if (candidate == null || candidate.GetComponentInParent<MemorySpaceBlock>() != block)
            {
                continue;
            }

            metadata = candidate;
            hit = hits[i];
            return true;
        }

        return false;
    }

    private static bool TryRaycastWallSegmentSlotUnderCursor(
        MemorySpaceBlock block,
        Vector2 mousePosition,
        out WallSegmentSlot wallSlot,
        out RaycastHit hit)
    {
        wallSlot = null;
        hit = default;
        if (block == null)
        {
            return false;
        }

        Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, 1000f, ~0, QueryTriggerInteraction.Collide);
        if (hits == null || hits.Length == 0)
        {
            return false;
        }

        Array.Sort(hits, CompareRaycastHitDistance);
        for (int i = 0; i < hits.Length; i++)
        {
            WallSegmentSlot candidate = hits[i].collider != null
                ? hits[i].collider.GetComponentInParent<WallSegmentSlot>()
                : null;
            if (candidate == null || candidate.GetComponentInParent<MemorySpaceBlock>() != block)
            {
                continue;
            }

            wallSlot = candidate;
            hit = hits[i];
            return true;
        }

        return false;
    }

    private static int ResolveWallDefinitionWidthUnits(SpaceSegmentDefinition definition)
    {
        if (definition == null)
        {
            return 1;
        }

        if (definition.sizeXZ.x > 0f)
        {
            return Mathf.Max(1, Mathf.RoundToInt(definition.sizeXZ.x));
        }

        if (TryExtractDefinitionSize(definition, out float parsedWidth, out _))
        {
            return Mathf.Max(1, Mathf.RoundToInt(parsedWidth));
        }

        return 1;
    }

    private static bool TryResolveBakedWallGridPosition(
        MemorySpaceBlock block,
        WallSegmentSlot wallSlot,
        int widthUnits,
        out int wallGridPosition)
    {
        wallGridPosition = 0;
        if (block == null || wallSlot == null)
        {
            return false;
        }

        Transform referenceTransform = wallSlot.segmentRoot != null ? wallSlot.segmentRoot : wallSlot.transform;
        if (referenceTransform == null)
        {
            return false;
        }

        int gridWidth = RoomSlotGridUtility.GetGridWidth(block);
        int gridDepth = RoomSlotGridUtility.GetGridDepth(block);
        float gridSize = RoomSlotGridUtility.GetGridSize(block);
        float halfWidth = gridWidth * gridSize * 0.5f;
        float halfDepth = gridDepth * gridSize * 0.5f;
        Vector3 localCenter = referenceTransform.localPosition;

        switch (wallSlot.side)
        {
            case WallSide.North:
            case WallSide.South:
                wallGridPosition = Mathf.RoundToInt(((localCenter.x + halfWidth) / gridSize) - (Mathf.Max(1, widthUnits) * 0.5f));
                break;

            case WallSide.East:
            case WallSide.West:
                wallGridPosition = Mathf.RoundToInt(((localCenter.z + halfDepth) / gridSize) - (Mathf.Max(1, widthUnits) * 0.5f));
                break;

            default:
                return false;
        }

        int maxAnchor = Mathf.Max(0, RoomSlotGridUtility.GetWallSlotCount(block, wallSlot.side) - Mathf.Max(1, widthUnits));
        wallGridPosition = Mathf.Clamp(wallGridPosition, 0, maxAnchor);
        return true;
    }

    private bool TryGetWallSurfaceContextForEdge(
        MemorySpaceBlock block,
        string wallEdgeKey,
        out WallSurfaceContext context,
        out string message)
    {
        context = null;
        message = string.Empty;
        if (block == null || string.IsNullOrWhiteSpace(wallEdgeKey))
        {
            message = "Wall surface data is missing.";
            return false;
        }

        SpaceSegmentPlacementMetadata[] placements = block.GetComponentsInChildren<SpaceSegmentPlacementMetadata>(true);
        for (int i = 0; i < placements.Length; i++)
        {
            SpaceSegmentPlacementMetadata placement = placements[i];
            if (placement == null || placement.record == null || placement.record.category != SegmentCategory.Wall)
            {
                continue;
            }

            List<string> placementKeys = RoomSlotGridUtility.GetWallEdgeKeys(
                placement.record.gridX,
                placement.record.gridZ,
                placement.record.side,
                Mathf.Max(1, placement.record.footprint.x));
            bool matches = false;
            for (int keyIndex = 0; keyIndex < placementKeys.Count; keyIndex++)
            {
                if (string.Equals(placementKeys[keyIndex], wallEdgeKey, StringComparison.OrdinalIgnoreCase))
                {
                    matches = true;
                    break;
                }
            }

            if (!matches)
            {
                continue;
            }

            SpaceSegmentDefinition definition = placement.definition;
            if (definition == null && block.segmentKit != null && !string.IsNullOrWhiteSpace(placement.record.segmentId))
            {
                definition = block.segmentKit.GetSegment(placement.record.segmentId);
            }

            if (definition == null || definition.category != SegmentCategory.Wall)
            {
                message = $"Wall edge {wallEdgeKey} is missing a valid wall definition.";
                return false;
            }

            WallSegmentSlot wallSlot = placement.GetComponent<WallSegmentSlot>();
            bool hasOverlay = placement.overlayDefinition != null
                || (wallSlot != null && !string.IsNullOrWhiteSpace(wallSlot.overlayId));
            if (hasOverlay)
            {
                message = $"Wall edge {wallEdgeKey} belongs to a window/opening segment and cannot host room slots.";
                return false;
            }

            float surfaceHeight = ResolveWallSurfaceHeight(definition);
            int layerCount = GetWallLayerCountForHeight(surfaceHeight);
            if (layerCount <= 0)
            {
                message = $"Wall edge {wallEdgeKey} has unsupported height {surfaceHeight:0.###}.";
                return false;
            }

            context = new WallSurfaceContext
            {
                PlacementMetadata = placement,
                Definition = definition,
                WallSlot = wallSlot,
                Side = placement.record.side,
                WallGridPosition = GetWallGridPosition(placement.record),
                WidthUnits = Mathf.Max(1, placement.record.footprint.x),
                SurfaceHeight = surfaceHeight,
                LayerCount = layerCount
            };
            return true;
        }

        if (block.wallSegments != null)
        {
            for (int i = 0; i < block.wallSegments.Count; i++)
            {
                WallSegmentSlot wallSlot = block.wallSegments[i];
                if (!TryResolveWallSlotPlacement(block, wallSlot, out SpaceSegmentDefinition definition, out int wallGridPosition, out int widthUnits, out float surfaceHeight, out int layerCount, out string resolveMessage))
                {
                    if (!string.IsNullOrWhiteSpace(resolveMessage))
                    {
                        message = resolveMessage;
                    }

                    continue;
                }

                if (!RoomSlotGridUtility.TryGetWallAnchorGrid(block, wallSlot.side, wallGridPosition, widthUnits, out int resolvedGridX, out int resolvedGridZ))
                {
                    continue;
                }

                List<string> slotKeys = RoomSlotGridUtility.GetWallEdgeKeys(
                    resolvedGridX,
                    resolvedGridZ,
                    wallSlot.side,
                    widthUnits);
                bool matches = false;
                for (int keyIndex = 0; keyIndex < slotKeys.Count; keyIndex++)
                {
                    if (string.Equals(slotKeys[keyIndex], wallEdgeKey, StringComparison.OrdinalIgnoreCase))
                    {
                        matches = true;
                        break;
                    }
                }

                if (!matches)
                {
                    continue;
                }

                context = new WallSurfaceContext
                {
                    PlacementMetadata = null,
                    Definition = definition,
                    WallSlot = wallSlot,
                    Side = wallSlot.side,
                    WallGridPosition = wallGridPosition,
                    WidthUnits = widthUnits,
                    SurfaceHeight = surfaceHeight,
                    LayerCount = layerCount
                };
                return true;
            }
        }

        message = $"No painted wall segment was found for wall edge {wallEdgeKey}.";
        return false;
    }

    private static bool TryRaycastAuthoringPlane(Transform blockRoot, Vector2 mousePosition, out Vector3 localPoint)
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

    private static bool TryRaycastWallSurfaceLocalPoint(
        MemorySpaceBlock block,
        WallSide wallSide,
        Vector3 wallOriginWorld,
        Vector2 mousePosition,
        out Vector3 localPoint)
    {
        localPoint = Vector3.zero;
        if (block == null)
        {
            return false;
        }

        Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);
        Vector3 worldNormal = block.transform.TransformDirection(GetWallPlaneLocalNormal(wallSide));
        Plane plane = new Plane(worldNormal, wallOriginWorld);
        if (!plane.Raycast(ray, out float distance))
        {
            return false;
        }

        localPoint = block.transform.InverseTransformPoint(ray.GetPoint(distance));
        return true;
    }

    private static Vector3 GetWallPlaneLocalNormal(WallSide wallSide)
    {
        switch (wallSide)
        {
            case WallSide.North:
                return Vector3.forward;
            case WallSide.South:
                return Vector3.back;
            case WallSide.East:
                return Vector3.right;
            case WallSide.West:
                return Vector3.left;
            default:
                return Vector3.forward;
        }
    }

    private static float ResolveWallSurfaceHeight(SpaceSegmentDefinition definition)
    {
        if (definition == null)
        {
            return 0f;
        }

        if (definition.height > 0f)
        {
            return definition.height;
        }

        if (TryExtractDefinitionSize(definition, out _, out float parsedHeight))
        {
            return parsedHeight;
        }

        return 0f;
    }

    private static int GetWallLayerCountForHeight(float wallHeight)
    {
        if (ApproximatelyWallHeight(wallHeight, FullWallHeight))
        {
            return FullWallLayerCount;
        }

        if (ApproximatelyWallHeight(wallHeight, HalfWallHeight))
        {
            return HalfWallLayerCount;
        }

        return 0;
    }

    private static bool ApproximatelyWallHeight(float a, float b)
    {
        return Mathf.Abs(a - b) <= WallHeightTolerance;
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
            if (TryParseSizeToken(tokens[i], out sizeX, out sizeY))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryParseSizeToken(string token, out float sizeX, out float sizeY)
    {
        sizeX = 1f;
        sizeY = 1f;
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        string normalized = token.Trim().ToLowerInvariant().Replace("x", "X");
        string[] parts = normalized.Split(new[] { 'X' }, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 2
            && float.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out sizeX)
            && float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out sizeY);
    }

    private static float GetWallPreviewHeight(PlacementCandidate candidate)
    {
        if (candidate.SurfaceType == RoomSlotSurfaceType.Wall
            && candidate.WallSurfaceHeight > 0f
            && candidate.WallLayerCount > 0)
        {
            return Mathf.Max(0.1f, candidate.WallSurfaceHeight / candidate.WallLayerCount);
        }

        return PreviewWallHeight;
    }

    private bool TryFindNearestRoomSlotPlacementMetadata(
        MemorySpaceBlock block,
        Vector3 localPoint,
        out RoomSlotPlacementMetadata metadata)
    {
        metadata = null;
        if (block == null)
        {
            return false;
        }

        RoomSlotPlacementMetadata[] placements = block.GetComponentsInChildren<RoomSlotPlacementMetadata>(true);
        float gridSize = RoomSlotGridUtility.GetGridSize(block);
        float maxDistanceSqr = gridSize * gridSize;
        float bestDistanceSqr = float.MaxValue;
        for (int i = 0; i < placements.Length; i++)
        {
            RoomSlotPlacementMetadata candidate = placements[i];
            if (candidate == null)
            {
                continue;
            }

            if (!TryGetRoomSlotPreviewBounds(block, candidate.gameObject, out Vector3 candidateLocalCenter, out Vector3 candidateLocalSize))
            {
                PlacementCandidate previewCandidate = new PlacementCandidate
                {
                    SurfaceType = candidate.surfaceType,
                    WidthUnits = candidate.widthUnits,
                    DepthUnits = candidate.depthUnits,
                    FloorGridXHalf = RoomSlotGridUtility.GetFloorGridXHalf(candidate),
                    FloorGridZHalf = RoomSlotGridUtility.GetFloorGridZHalf(candidate),
                    FloorWidthHalf = RoomSlotGridUtility.GetFloorWidthHalf(candidate),
                    FloorDepthHalf = RoomSlotGridUtility.GetFloorDepthHalf(candidate),
                    WallSide = candidate.wallSide,
                    WallLayerIndex = candidate.wallLayerIndex,
                    WallLayerCount = Mathf.Max(1, candidate.wallLayerCount),
                    WallSurfaceHeight = candidate.wallSurfaceHeight
                };
                candidateLocalCenter = GetPreviewDisplayCenter(previewCandidate, candidate.localPosition);
                candidateLocalSize = candidate.surfaceType == RoomSlotSurfaceType.Floor
                    ? new Vector3(
                        RoomSlotGridUtility.GetFloorWidthHalf(candidate) * (gridSize / RoomSlotGridUtility.GetFloorGridSubdivision()),
                        PreviewFloorHeight,
                        RoomSlotGridUtility.GetFloorDepthHalf(candidate) * (gridSize / RoomSlotGridUtility.GetFloorGridSubdivision()))
                    : GetWallPreviewSize(
                        block,
                        previewCandidate);
            }

            Vector3 halfSize = candidateLocalSize * 0.5f;
            float clampedX = Mathf.Clamp(localPoint.x, candidateLocalCenter.x - halfSize.x, candidateLocalCenter.x + halfSize.x);
            float clampedY = Mathf.Clamp(localPoint.y, candidateLocalCenter.y - halfSize.y, candidateLocalCenter.y + halfSize.y);
            float clampedZ = Mathf.Clamp(localPoint.z, candidateLocalCenter.z - halfSize.z, candidateLocalCenter.z + halfSize.z);
            float distanceSqr = (localPoint - new Vector3(clampedX, clampedY, clampedZ)).sqrMagnitude;
            if (distanceSqr > maxDistanceSqr || distanceSqr >= bestDistanceSqr)
            {
                continue;
            }

            bestDistanceSqr = distanceSqr;
            metadata = candidate;
        }

        return metadata != null;
    }

    private static bool TryGetRoomSlotPreviewBounds(
        MemorySpaceBlock block,
        GameObject placementObject,
        out Vector3 localCenter,
        out Vector3 localSize)
    {
        localCenter = Vector3.zero;
        localSize = Vector3.one;
        if (block == null || placementObject == null)
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

        localCenter = block.transform.InverseTransformPoint(bounds.center);
        Vector3 localSizeVector = block.transform.InverseTransformVector(bounds.size);
        localSize = new Vector3(
            Mathf.Max(0.08f, Mathf.Abs(localSizeVector.x)),
            Mathf.Max(0.08f, Mathf.Abs(localSizeVector.y)),
            Mathf.Max(0.08f, Mathf.Abs(localSizeVector.z)));
        return true;
    }

    private void DrawPrefabPalette()
    {
        EditorGUILayout.LabelField("Furniture Palette", EditorStyles.boldLabel);
        if (slotPrefabPaletteEntries.Count == 0)
        {
            EditorGUILayout.HelpBox(
                $"No room-slot furniture prefabs were found under {DefaultFurniturePrefabFolder}. You can still assign a prefab manually above.",
                MessageType.Info);
            return;
        }

        prefabPaletteScrollPosition = EditorGUILayout.BeginScrollView(prefabPaletteScrollPosition, GUILayout.MinHeight(120f), GUILayout.MaxHeight(280f));
        int columns = Mathf.Max(1, Mathf.FloorToInt((position.width - 32f) / PaletteButtonWidth));
        for (int index = 0; index < slotPrefabPaletteEntries.Count; index += columns)
        {
            EditorGUILayout.BeginHorizontal();
            for (int column = 0; column < columns; column++)
            {
                int entryIndex = index + column;
                if (entryIndex >= slotPrefabPaletteEntries.Count)
                {
                    GUILayout.FlexibleSpace();
                    continue;
                }

                DrawPrefabPaletteButton(slotPrefabPaletteEntries[entryIndex]);
            }

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawPrefabPaletteButton(SlotPrefabPaletteEntry entry)
    {
        if (entry == null || entry.Prefab == null)
        {
            return;
        }

        Texture previewTexture = AssetPreview.GetAssetPreview(entry.Prefab);
        if (previewTexture == null)
        {
            previewTexture = AssetPreview.GetMiniThumbnail(entry.Prefab);
        }

        bool isSelected = slotPrefab == entry.Prefab;
        Color previousColor = GUI.backgroundColor;
        if (isSelected)
        {
            GUI.backgroundColor = new Color(0.35f, 0.85f, 1f, 1f);
        }

        GUIContent content = new GUIContent(entry.Label, previewTexture, entry.AssetPath);
        if (GUILayout.Button(content, GetPrefabPaletteButtonStyle(), GUILayout.Width(PaletteButtonWidth), GUILayout.Height(PaletteButtonHeight)))
        {
            slotPrefab = entry.Prefab;
            RepaintPreviewIfNeeded();
            GUI.FocusControl(null);
        }

        GUI.backgroundColor = previousColor;
    }

    private GUIStyle GetPrefabPaletteButtonStyle()
    {
        if (prefabPaletteButtonStyle != null)
        {
            return prefabPaletteButtonStyle;
        }

        prefabPaletteButtonStyle = new GUIStyle(GUI.skin.button)
        {
            alignment = TextAnchor.UpperCenter,
            imagePosition = ImagePosition.ImageAbove,
            wordWrap = true,
            fixedWidth = PaletteButtonWidth,
            fixedHeight = PaletteButtonHeight,
            padding = new RectOffset(6, 6, 8, 6)
        };
        return prefabPaletteButtonStyle;
    }

    private void RebuildSlotPrefabPalette()
    {
        slotPrefabPaletteEntries.Clear();
        string[] searchFolders = AssetDatabase.IsValidFolder(DefaultFurniturePrefabFolder)
            ? new[] { DefaultFurniturePrefabFolder }
            : new[] { "Assets/_project/Prefabs" };
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", searchFolders);
        for (int i = 0; i < prefabGuids.Length; i++)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefab == null)
            {
                continue;
            }

            if (prefab.GetComponent<MemoryDisplayFurniture>() == null && prefab.GetComponentInChildren<MemoryDisplaySlot>(true) == null)
            {
                continue;
            }

            slotPrefabPaletteEntries.Add(new SlotPrefabPaletteEntry
            {
                Prefab = prefab,
                AssetPath = assetPath,
                Label = prefab.name
            });
        }

        slotPrefabPaletteEntries.Sort((left, right) => string.Compare(left.Label, right.Label, StringComparison.OrdinalIgnoreCase));

        if (slotPrefab == null && slotPrefabPaletteEntries.Count > 0)
        {
            slotPrefab = slotPrefabPaletteEntries[0].Prefab;
        }
    }

    private static bool TryResolveTargetBlock(Object candidate, out MemorySpaceBlock block, out string errorMessage)
    {
        block = null;
        errorMessage = "Assign a MemorySpaceBlock target.";
        if (candidate == null)
        {
            return false;
        }

        switch (candidate)
        {
            case MemorySpaceBlock memorySpaceBlock:
                block = memorySpaceBlock;
                break;
            case GameObject gameObject:
                block = gameObject.GetComponent<MemorySpaceBlock>();
                if (block == null)
                {
                    block = gameObject.GetComponentInChildren<MemorySpaceBlock>(true);
                }
                break;
            case Component component:
                block = component.GetComponent<MemorySpaceBlock>();
                if (block == null)
                {
                    block = component.GetComponentInChildren<MemorySpaceBlock>(true);
                }
                break;
        }

        if (block == null)
        {
            errorMessage = "The selected object does not contain a MemorySpaceBlock component.";
            return false;
        }

        return true;
    }

    private bool TryAutoAssignTargetFromSelection()
    {
        if (Selection.activeGameObject == null)
        {
            return false;
        }

        MemorySpaceBlock block = Selection.activeGameObject.GetComponentInParent<MemorySpaceBlock>();
        if (block == null)
        {
            return false;
        }

        targetObject = block;
        RepaintPreviewIfNeeded();
        return true;
    }

    private static Transform GetOrCreateChild(Transform parent, string childName, bool useUndo)
    {
        if (parent == null)
        {
            return null;
        }

        Transform child = parent.Find(childName);
        if (child != null)
        {
            child.localPosition = Vector3.zero;
            child.localRotation = Quaternion.identity;
            child.localScale = Vector3.one;
            return child;
        }

        GameObject childObject = new GameObject(childName);
        if (useUndo)
        {
            Undo.RegisterCreatedObjectUndo(childObject, $"Create {childName}");
            Undo.SetTransformParent(childObject.transform, parent, $"Create {childName}");
        }
        else
        {
            childObject.transform.SetParent(parent, false);
        }

        childObject.transform.localPosition = Vector3.zero;
        childObject.transform.localRotation = Quaternion.identity;
        childObject.transform.localScale = Vector3.one;
        return childObject.transform;
    }

    private static GameObject InstantiateSlotPrefab(GameObject prefab, Transform parent, bool prefabAssetEdit)
    {
        if (prefab == null || parent == null)
        {
            return null;
        }

        GameObject instance = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
        if (instance == null)
        {
            instance = Object.Instantiate(prefab, parent, false);
        }

        if (!prefabAssetEdit)
        {
            Undo.RegisterCreatedObjectUndo(instance, "Instantiate Room Slot Prefab");
        }

        return instance;
    }

    private static void ApplyAuthoredPlacementTransform(
        Transform instanceTransform,
        GameObject prefab,
        Vector3 placementLocalPosition,
        Quaternion placementLocalRotation)
    {
        if (instanceTransform == null)
        {
            return;
        }

        Vector3 authoredLocalPosition = instanceTransform.localPosition;
        Quaternion authoredLocalRotation = instanceTransform.localRotation;
        Vector3 authoredLocalScale = EnsureNonZeroScale(instanceTransform.localScale);
        Vector3 authoredPlacementOffset = GetSlotPrefabPlacementOffset(prefab);

        instanceTransform.localPosition = placementLocalPosition + (placementLocalRotation * (authoredLocalPosition + authoredPlacementOffset));
        instanceTransform.localRotation = placementLocalRotation * authoredLocalRotation;
        instanceTransform.localScale = authoredLocalScale;
    }

    private static void ResolveAuthoredPreviewTransform(
        GameObject prefab,
        Vector3 placementLocalPosition,
        Quaternion placementLocalRotation,
        out Vector3 resolvedLocalPosition,
        out Quaternion resolvedLocalRotation)
    {
        if (prefab == null)
        {
            resolvedLocalPosition = placementLocalPosition;
            resolvedLocalRotation = placementLocalRotation;
            return;
        }

        Vector3 authoredPlacementOffset = GetSlotPrefabPlacementOffset(prefab);
        resolvedLocalPosition = placementLocalPosition + (placementLocalRotation * (prefab.transform.localPosition + authoredPlacementOffset));
        resolvedLocalRotation = placementLocalRotation * prefab.transform.localRotation;
    }

    private static Vector3 GetSlotPrefabPlacementOffset(GameObject prefab)
    {
        DisplayFurnitureBuildProfile profile = GetSlotPrefabPlacementProfile(prefab);
        return profile != null ? profile.placementLocalOffset : Vector3.zero;
    }

    private static DisplayFurnitureBuildProfile GetSlotPrefabPlacementProfile(GameObject prefab)
    {
        if (prefab == null)
        {
            return null;
        }

        string prefabAssetPath = AssetDatabase.GetAssetPath(prefab);
        if (string.IsNullOrWhiteSpace(prefabAssetPath))
        {
            return null;
        }

        if (SlotPrefabPlacementProfileCache.TryGetValue(prefabAssetPath, out DisplayFurnitureBuildProfile cachedProfile))
        {
            return cachedProfile;
        }

        DisplayFurnitureBuildProfile resolvedProfile = ResolveSlotPrefabPlacementProfile(prefab, prefabAssetPath);
        SlotPrefabPlacementProfileCache[prefabAssetPath] = resolvedProfile;
        return resolvedProfile;
    }

    private static DisplayFurnitureBuildProfile ResolveSlotPrefabPlacementProfile(GameObject prefab, string prefabAssetPath)
    {
        string prefabName = prefab != null ? prefab.name : System.IO.Path.GetFileNameWithoutExtension(prefabAssetPath);
        string modelId = prefabName.StartsWith("PF_", StringComparison.OrdinalIgnoreCase)
            ? prefabName.Substring(3)
            : prefabName;
        string[] profileGuids = AssetDatabase.FindAssets($"{modelId}_BuildProfile t:DisplayFurnitureBuildProfile");
        for (int i = 0; i < profileGuids.Length; i++)
        {
            string profileAssetPath = AssetDatabase.GUIDToAssetPath(profileGuids[i]);
            if (string.IsNullOrWhiteSpace(profileAssetPath))
            {
                continue;
            }

            if (!string.Equals(
                    System.IO.Path.GetFileNameWithoutExtension(profileAssetPath),
                    $"{modelId}_BuildProfile",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            DisplayFurnitureBuildProfile profile = AssetDatabase.LoadAssetAtPath<DisplayFurnitureBuildProfile>(profileAssetPath);
            if (profile != null)
            {
                return profile;
            }
        }

        return null;
    }

    private static Vector3 EnsureNonZeroScale(Vector3 scale)
    {
        return new Vector3(
            Mathf.Approximately(scale.x, 0f) ? 1f : scale.x,
            Mathf.Approximately(scale.y, 0f) ? 1f : scale.y,
            Mathf.Approximately(scale.z, 0f) ? 1f : scale.z);
    }

    private static string GenerateSlotPlacementId()
    {
        return $"RSP_{Guid.NewGuid().ToString("N").Substring(0, 8).ToUpperInvariant()}";
    }

    private static HashSet<string> CollectExistingFurnitureIds(MemorySpaceBlock block, GameObject ignoreRoot)
    {
        HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (block == null)
        {
            return ids;
        }

        MemoryDisplayFurniture[] furniture = block.GetComponentsInChildren<MemoryDisplayFurniture>(true);
        for (int i = 0; i < furniture.Length; i++)
        {
            if (furniture[i] == null || (ignoreRoot != null && furniture[i].transform.IsChildOf(ignoreRoot.transform)))
            {
                continue;
            }

            string id = GetSerializedString(furniture[i], "furnitureId");
            if (!string.IsNullOrWhiteSpace(id))
            {
                ids.Add(id);
            }
        }

        return ids;
    }

    private static HashSet<string> CollectExistingSlotIds(MemorySpaceBlock block, GameObject ignoreRoot)
    {
        HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (block == null)
        {
            return ids;
        }

        MemoryDisplaySlot[] slots = block.GetComponentsInChildren<MemoryDisplaySlot>(true);
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null || (ignoreRoot != null && slots[i].transform.IsChildOf(ignoreRoot.transform)))
            {
                continue;
            }

            string id = GetSerializedString(slots[i], "slotId");
            if (!string.IsNullOrWhiteSpace(id))
            {
                ids.Add(id);
            }
        }

        return ids;
    }

    private static string GetSerializedString(Object target, string propertyName)
    {
        if (target == null)
        {
            return string.Empty;
        }

        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        return property != null ? property.stringValue : string.Empty;
    }

    private static void SetSerializedString(Object target, string propertyName, string value)
    {
        if (target == null)
        {
            return;
        }

        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
        {
            return;
        }

        property.stringValue = value;
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(target);
    }

    private static void SetSerializedObjectReference(Object target, string propertyName, Object reference)
    {
        if (target == null)
        {
            return;
        }

        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
        {
            return;
        }

        property.objectReferenceValue = reference;
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(target);
    }

    private static string SanitizeIdToken(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return "Block";
        }

        char[] chars = input.Trim().ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '_')
            {
                chars[i] = '_';
            }
        }

        return new string(chars);
    }

    private static int CompareRoomSlotMetadata(RoomSlotPlacementMetadata a, RoomSlotPlacementMetadata b)
    {
        string aId = a != null ? a.slotPlacementId : string.Empty;
        string bId = b != null ? b.slotPlacementId : string.Empty;
        return string.Compare(aId, bId, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TrySavePrefabStage(GameObject targetGameObject)
    {
        if (targetGameObject == null)
        {
            return false;
        }

        Type utilityType =
            Type.GetType("UnityEditor.SceneManagement.PrefabStageUtility, UnityEditor")
            ?? Type.GetType("UnityEditor.Experimental.SceneManagement.PrefabStageUtility, UnityEditor");
        if (utilityType == null)
        {
            return false;
        }

        MethodInfo getPrefabStageMethod =
            utilityType.GetMethod("GetPrefabStage", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(GameObject) }, null)
            ?? utilityType.GetMethod("GetCurrentPrefabStage", BindingFlags.Public | BindingFlags.Static);
        if (getPrefabStageMethod == null)
        {
            return false;
        }

        object stage = getPrefabStageMethod.GetParameters().Length == 1
            ? getPrefabStageMethod.Invoke(null, new object[] { targetGameObject })
            : getPrefabStageMethod.Invoke(null, null);
        if (stage == null)
        {
            return false;
        }

        PropertyInfo prefabContentsRootProperty = stage.GetType().GetProperty("prefabContentsRoot", BindingFlags.Public | BindingFlags.Instance);
        PropertyInfo assetPathProperty = stage.GetType().GetProperty("assetPath", BindingFlags.Public | BindingFlags.Instance);
        GameObject prefabContentsRoot = prefabContentsRootProperty != null ? prefabContentsRootProperty.GetValue(stage, null) as GameObject : null;
        string assetPath = assetPathProperty != null ? assetPathProperty.GetValue(stage, null) as string : null;
        if (prefabContentsRoot == null || string.IsNullOrWhiteSpace(assetPath))
        {
            return false;
        }

        PrefabUtility.SaveAsPrefabAsset(prefabContentsRoot, assetPath);
        AssetDatabase.SaveAssets();
        return true;
    }

    private void PresentValidation(string summary, ValidationResult validation, MessageType fallbackType)
    {
        infoMessages.Clear();
        warningMessages.Clear();
        errorMessages.Clear();

        if (validation != null)
        {
            infoMessages.AddRange(validation.Infos);
            warningMessages.AddRange(validation.Warnings);
            errorMessages.AddRange(validation.Errors);
        }

        MessageType type = validation != null && validation.HasErrors
            ? MessageType.Error
            : validation != null && validation.HasWarnings
                ? MessageType.Warning
                : fallbackType;
        SetStatus(summary, type);

        if (validation != null)
        {
            foreach (string message in validation.Errors)
            {
                Debug.LogError($"[RoomSlotPlacementTool] {message}");
            }

            foreach (string message in validation.Warnings)
            {
                Debug.LogWarning($"[RoomSlotPlacementTool] {message}");
            }

            foreach (string message in validation.Infos)
            {
                Debug.Log($"[RoomSlotPlacementTool] {message}");
            }
        }
    }

    private void SetStatus(string message, MessageType type)
    {
        statusMessage = string.IsNullOrWhiteSpace(message) ? "Ready." : message;
        statusType = type;
        Repaint();
    }

    private void RepaintPreviewIfNeeded()
    {
        if (previewMode != PreviewMode.None)
        {
            InvalidateScenePreviewCache();
            QueueDeferredRepaint();
        }
    }

    private void UpdateTemporaryCeilingVisibility(MemorySpaceBlock targetBlock)
    {
        if (!ShouldTemporarilyHideCeilings(targetBlock))
        {
            RestoreTemporaryCeilingVisibility();
            return;
        }

        if (temporarilyHiddenCeilingBlock == targetBlock && temporarilyHiddenCeilingObjects.Count > 0)
        {
            return;
        }

        RestoreTemporaryCeilingVisibility();

        List<GameObject> ceilingTargets = CollectCeilingVisibilityTargets(targetBlock);
        if (ceilingTargets.Count == 0)
        {
            return;
        }

        SceneVisibilityManager visibilityManager = SceneVisibilityManager.instance;
        for (int i = 0; i < ceilingTargets.Count; i++)
        {
            GameObject ceilingTarget = ceilingTargets[i];
            if (ceilingTarget == null)
            {
                continue;
            }

            int instanceId = ceilingTarget.GetInstanceID();
            if (visibilityManager.IsHidden(ceilingTarget, true))
            {
                preHiddenCeilingObjectIds.Add(instanceId);
            }

            visibilityManager.Hide(ceilingTarget, true);
            temporarilyHiddenCeilingObjects.Add(ceilingTarget);
        }

        temporarilyHiddenCeilingBlock = targetBlock;
    }

    private void RestoreTemporaryCeilingVisibility()
    {
        if (temporarilyHiddenCeilingObjects.Count == 0)
        {
            temporarilyHiddenCeilingBlock = null;
            preHiddenCeilingObjectIds.Clear();
            return;
        }

        SceneVisibilityManager visibilityManager = SceneVisibilityManager.instance;
        for (int i = 0; i < temporarilyHiddenCeilingObjects.Count; i++)
        {
            GameObject ceilingTarget = temporarilyHiddenCeilingObjects[i];
            if (ceilingTarget == null)
            {
                continue;
            }

            if (preHiddenCeilingObjectIds.Contains(ceilingTarget.GetInstanceID()))
            {
                continue;
            }

            visibilityManager.Show(ceilingTarget, true);
        }

        temporarilyHiddenCeilingObjects.Clear();
        preHiddenCeilingObjectIds.Clear();
        temporarilyHiddenCeilingBlock = null;
    }

    private bool ShouldTemporarilyHideCeilings(MemorySpaceBlock targetBlock)
    {
        return hideCeilingsWhilePainting
            && scenePlacementEnabled
            && previewMode != PreviewMode.None
            && targetBlock != null
            && !EditorUtility.IsPersistent(targetBlock.gameObject);
    }

    private static List<GameObject> CollectCeilingVisibilityTargets(MemorySpaceBlock block)
    {
        List<GameObject> results = new List<GameObject>();
        if (block == null)
        {
            return results;
        }

        HashSet<int> seenInstanceIds = new HashSet<int>();
        string[] preferredRootNames = { "CeilingSegments", "CeilingRoot", "Ceilings", "Ceiling" };
        for (int i = 0; i < preferredRootNames.Length; i++)
        {
            Transform child = block.transform.Find(preferredRootNames[i]);
            if (child == null)
            {
                continue;
            }

            int instanceId = child.gameObject.GetInstanceID();
            if (seenInstanceIds.Add(instanceId))
            {
                results.Add(child.gameObject);
            }
        }

        if (results.Count > 0)
        {
            return results;
        }

        SpaceSegmentPlacementMetadata[] placements = block.GetComponentsInChildren<SpaceSegmentPlacementMetadata>(true);
        for (int i = 0; i < placements.Length; i++)
        {
            SpaceSegmentPlacementMetadata placement = placements[i];
            if (placement == null || placement.record == null || placement.record.category != SegmentCategory.Ceiling)
            {
                continue;
            }

            int instanceId = placement.gameObject.GetInstanceID();
            if (seenInstanceIds.Add(instanceId))
            {
                results.Add(placement.gameObject);
            }
        }

        return results;
    }

    private void ReportException(string actionLabel, Exception exception)
    {
        Debug.LogError($"[RoomSlotPlacementTool] {actionLabel} failed: {exception}");
        SetStatus($"{actionLabel} failed: {exception.Message}", MessageType.Error);
    }

    private void DebugWallPlacement(string stage, string message)
    {
        if (!EnableWallPlacementDebugLogs || string.IsNullOrWhiteSpace(stage))
        {
            return;
        }

        string finalMessage = string.IsNullOrWhiteSpace(message) ? stage : $"{stage}: {message}";
        string key = $"{stage}|{message}";
        double now = EditorApplication.timeSinceStartup;
        if (string.Equals(lastWallDebugLogKey, key, StringComparison.Ordinal)
            && (now - lastWallDebugLogTime) < 0.2d)
        {
            return;
        }

        lastWallDebugLogKey = key;
        lastWallDebugLogTime = now;
        Debug.Log($"[RoomSlotPlacementTool][WallDebug] {finalMessage}");
    }
}
#endif
