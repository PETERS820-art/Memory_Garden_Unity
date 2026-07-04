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
        public float RotationY;
        public WallSide WallSide;
        public int WallGridPosition;
        public float HeightOffset;
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
    [SerializeField] private float wallHeightOffset;
    [SerializeField] private float wallRotationY;
    [SerializeField] private bool resizeHitboxToGridFootprint;
    [SerializeField] private bool replaceOverlappingSlots;
    [SerializeField] private bool scenePlacementEnabled = true;
    [SerializeField] private bool sceneAutoPickWallSide = true;
    [SerializeField] private bool sceneDeleteMode;

    private PreviewMode previewMode;
    private Vector2 scrollPosition;
    private Vector2 prefabPaletteScrollPosition;
    private string statusMessage = "Select a MemorySpaceBlock and a slot prefab to begin.";
    private MessageType statusType = MessageType.Info;
    private bool deferredRepaintQueued;
    private GUIStyle prefabPaletteButtonStyle;
    private readonly List<string> infoMessages = new List<string>();
    private readonly List<string> warningMessages = new List<string>();
    private readonly List<string> errorMessages = new List<string>();
    private readonly List<SlotPrefabPaletteEntry> slotPrefabPaletteEntries = new List<SlotPrefabPaletteEntry>();

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
        SceneView.duringSceneGui -= OnSceneGUI;
    }

    private void OnSelectionChange()
    {
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

        scenePlacementEnabled = EditorGUILayout.Toggle("Enable Scene Placement", scenePlacementEnabled);
        sceneAutoPickWallSide = EditorGUILayout.Toggle("Auto Pick Wall Side", sceneAutoPickWallSide);
        sceneDeleteMode = EditorGUILayout.Toggle("Scene Delete Mode", sceneDeleteMode);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Paint Floor"))
            {
                previewMode = PreviewMode.Floor;
                SceneView.RepaintAll();
            }

            if (GUILayout.Button("Paint Wall"))
            {
                previewMode = PreviewMode.Wall;
                SceneView.RepaintAll();
            }

            if (GUILayout.Button("Clear Scene Preview"))
            {
                previewMode = PreviewMode.None;
                SceneView.RepaintAll();
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
        if (EditorGUI.EndChangeCheck())
        {
            RepaintPreviewIfNeeded();
        }

        EditorGUILayout.HelpBox(
            "These settings control the footprint and rotation used when you paint floor furniture directly in Scene view.",
            MessageType.None);
    }

    private void DrawWallSection()
    {
        EditorGUILayout.LabelField("Wall Settings", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        if (!sceneAutoPickWallSide)
        {
            wallSide = (WallSide)EditorGUILayout.EnumPopup("Wall Side", wallSide);
        }

        wallWidthUnits = Mathf.Max(1, EditorGUILayout.IntField("Width Units", wallWidthUnits));
        wallHeightOffset = EditorGUILayout.FloatField("Height Offset", wallHeightOffset);
        wallRotationY = EditorGUILayout.FloatField("Rotation Y", wallRotationY);
        if (EditorGUI.EndChangeCheck())
        {
            RepaintPreviewIfNeeded();
        }

        EditorGUILayout.HelpBox(
            sceneAutoPickWallSide
                ? "Wall side is picked from the cursor position in Scene view. Width, height offset, and rotation still apply to the painted slot."
                : "These settings control the wall placement used when painting directly in Scene view.",
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
            return;
        }

        if (targetBlock == null || EditorUtility.IsPersistent(targetBlock.gameObject))
        {
            return;
        }

        sceneView.wantsMouseMove = scenePlacementEnabled || sceneDeleteMode;
        Event current = Event.current;
        RequestLiveScenePreviewRefresh(current);

        if (current != null && current.type == EventType.Repaint && (scenePlacementEnabled || sceneDeleteMode))
        {
            DrawAuthoringGrid(
                targetBlock.transform,
                RoomSlotGridUtility.GetGridWidth(targetBlock),
                RoomSlotGridUtility.GetGridDepth(targetBlock),
                RoomSlotGridUtility.GetGridSize(targetBlock));
        }

        HandleSceneHotkeys(current);

        if (TryBuildDeletePreview(targetBlock, current, out ScenePlacementPreview deletePreview))
        {
            if (current != null && current.type == EventType.Repaint)
            {
                DrawScenePlacementPreview(targetBlock.transform, deletePreview);
            }

            if (TryHandleDeletePreviewClick(current, targetBlock, deletePreview))
            {
                QueueDeferredRepaint();
            }

            return;
        }

        if (scenePlacementEnabled && previewMode != PreviewMode.None)
        {
            if (TryBuildScenePlacementPreview(targetBlock, current, out ScenePlacementPreview preview))
            {
                if (current != null && current.type == EventType.Repaint)
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
    }

    private void RequestLiveScenePreviewRefresh(Event current)
    {
        if (current == null)
        {
            return;
        }

        if (current.type == EventType.MouseMove || current.type == EventType.MouseDrag)
        {
            QueueDeferredRepaint();
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
            preview.HasPreview = true;
            preview.CanPlace = false;
            preview.Candidate = candidate;
            preview.Message = errorMessage;
            preview.LocalCenter = localPosition;
            preview.LocalSize = candidate.SurfaceType == RoomSlotSurfaceType.Floor
                ? new Vector3(
                    Mathf.Max(1, candidate.WidthUnits) * RoomSlotGridUtility.GetGridSize(targetBlock),
                    PreviewFloorHeight,
                    Mathf.Max(1, candidate.DepthUnits) * RoomSlotGridUtility.GetGridSize(targetBlock))
                : GetWallPreviewSize(targetBlock, candidate);
            preview.LocalRotation = localRotation;
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
        preview.LocalCenter = localPosition;
        preview.LocalRotation = localRotation;
        preview.LocalSize = candidate.SurfaceType == RoomSlotSurfaceType.Floor
            ? new Vector3(
                Mathf.Max(1, candidate.WidthUnits) * RoomSlotGridUtility.GetGridSize(targetBlock),
                PreviewFloorHeight,
                Mathf.Max(1, candidate.DepthUnits) * RoomSlotGridUtility.GetGridSize(targetBlock))
            : GetWallPreviewSize(targetBlock, candidate);
        preview.CanPlace = overlaps.Count == 0 || replaceOverlappingSlots;

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
            preview.Message = $"{candidate.SurfaceType} {candidate.WallSide} [{candidate.WallGridPosition}]";
        }
        else
        {
            preview.Message = $"{candidate.SurfaceType} @ ({candidate.GridX}, {candidate.GridZ})";
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

        if (!TryRaycastAuthoringPlane(block.transform, current.mousePosition, out Vector3 localPoint))
        {
            return false;
        }

        if (!TryFindNearestRoomSlotPlacementMetadata(block, localPoint, out RoomSlotPlacementMetadata metadata) || metadata == null)
        {
            return false;
        }

        if (!TryGetRoomSlotPreviewBounds(block, metadata.gameObject, out Vector3 localCenter, out Vector3 localSize))
        {
            localCenter = metadata.localPosition;
            localSize = metadata.surfaceType == RoomSlotSurfaceType.Floor
                ? new Vector3(
                    Mathf.Max(1, metadata.widthUnits) * RoomSlotGridUtility.GetGridSize(block),
                    PreviewFloorHeight,
                    Mathf.Max(1, metadata.depthUnits) * RoomSlotGridUtility.GetGridSize(block))
                : GetWallPreviewSize(
                    block,
                    new PlacementCandidate
                    {
                        SurfaceType = metadata.surfaceType,
                        WidthUnits = metadata.widthUnits,
                        DepthUnits = metadata.depthUnits,
                        WallSide = metadata.wallSide,
                        WallGridPosition = metadata.wallGridPosition,
                        GridX = metadata.gridX,
                        GridZ = metadata.gridZ,
                        RotationY = metadata.rotationY,
                        HeightOffset = metadata.heightOffset
                    });
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

    private void DrawScenePlacementPreview(Transform blockRoot, ScenePlacementPreview preview)
    {
        if (blockRoot == null || !preview.HasPreview)
        {
            return;
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
            PlaceRoomSlot(preview.Candidate);
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
                    SetStatus(errorMessage, MessageType.Error);
                    return;
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
                instance.transform.localPosition = localPosition;
                instance.transform.localRotation = localRotation;

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
                result.AddError($"Missing MemoryDisplaySlot on {metadata.name}.");
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
                    if (!RoomSlotGridUtility.IsFloorPlacementInBounds(block, metadata.gridX, metadata.gridZ, metadata.widthUnits, metadata.depthUnits))
                    {
                        result.AddError($"Floor footprint out of bounds on {metadata.slotPlacementId}.");
                    }

                    List<Vector2Int> floorCells = RoomSlotGridUtility.GetFloorFootprintCells(metadata.gridX, metadata.gridZ, metadata.widthUnits, metadata.depthUnits);
                    for (int cellIndex = 0; cellIndex < floorCells.Count; cellIndex++)
                    {
                        string key = $"{floorCells[cellIndex].x}:{floorCells[cellIndex].y}";
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

                    List<string> wallEdgeKeys = RoomSlotGridUtility.GetWallEdgeKeys(metadata.gridX, metadata.gridZ, metadata.wallSide, metadata.widthUnits);
                    for (int edgeIndex = 0; edgeIndex < wallEdgeKeys.Count; edgeIndex++)
                    {
                        if (!occupiedWallEdges.Add(wallEdgeKeys[edgeIndex]))
                        {
                            result.AddError($"Overlapping wall footprint at {wallEdgeKeys[edgeIndex]}.");
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
        if (slots.Length == 0)
        {
            result.AddError("Slot prefab must contain at least one MemoryDisplaySlot.");
        }

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
            result.AddInfo($"Found root MemoryDisplayFurniture with {slots.Length} slot(s).");
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

        MemoryDisplaySlot[] slots = instance.GetComponentsInChildren<MemoryDisplaySlot>(true);
        if (slots.Length == 0)
        {
            result.AddError("Placed instance does not contain any MemoryDisplaySlot.");
            return result;
        }

        MemoryDisplayFurniture furniture = instance.GetComponent<MemoryDisplayFurniture>();
        if (furniture == null)
        {
            furniture = context != null && !context.IsPrefabAssetEdit
                ? Undo.AddComponent<MemoryDisplayFurniture>(instance)
                : instance.AddComponent<MemoryDisplayFurniture>();
            result.AddWarning($"Created MemoryDisplayFurniture on {instance.name} because the prefab only exposed child MemoryDisplaySlot components.");
        }

        furniture.AutoCollectSlots();

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
            furniture.AutoCollectSlots();
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
        metadata.rotationY = RoomSlotGridUtility.NormalizeRotation(candidate.RotationY);
        metadata.wallSide = candidate.WallSide;
        metadata.wallGridPosition = candidate.WallGridPosition;
        metadata.heightOffset = candidate.SurfaceType == RoomSlotSurfaceType.Wall ? candidate.HeightOffset : 0f;
        metadata.furnitureId = furniture != null ? GetSerializedString(furniture, "furnitureId") : string.Empty;
        metadata.slotIds = new List<string>(slots.Length);
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
            RotationY = placementSurface == RoomSlotSurfaceType.Floor
                ? RoomSlotGridUtility.NormalizeRotation(floorRotationY)
                : RoomSlotGridUtility.NormalizeRotation(wallRotationY),
            WallSide = wallSide,
            WallGridPosition = wallGridPosition,
            HeightOffset = placementSurface == RoomSlotSurfaceType.Wall ? wallHeightOffset : 0f
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
        metadata.rotationY = RoomSlotGridUtility.NormalizeRotation(candidate.RotationY);
        metadata.wallSide = candidate.WallSide;
        metadata.wallGridPosition = candidate.WallGridPosition;
        metadata.heightOffset = candidate.SurfaceType == RoomSlotSurfaceType.Wall ? candidate.HeightOffset : 0f;
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
        if (candidate.WallSide == WallSide.North || candidate.WallSide == WallSide.South)
        {
            return new Vector3(width, PreviewWallHeight, PreviewThickness);
        }

        return new Vector3(PreviewThickness, PreviewWallHeight, width);
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

        if (!TryRaycastAuthoringPlane(block.transform, mousePosition, out Vector3 localPoint))
        {
            message = "Could not raycast onto the block authoring plane.";
            return false;
        }

        int gridWidth = RoomSlotGridUtility.GetGridWidth(block);
        int gridDepth = RoomSlotGridUtility.GetGridDepth(block);
        float gridSize = RoomSlotGridUtility.GetGridSize(block);
        float halfWidth = gridWidth * gridSize * 0.5f;
        float halfDepth = gridDepth * gridSize * 0.5f;
        int cellX = Mathf.FloorToInt((localPoint.x + halfWidth) / gridSize);
        int cellZ = Mathf.FloorToInt((localPoint.z + halfDepth) / gridSize);
        if (cellX < 0 || cellZ < 0 || cellX >= gridWidth || cellZ >= gridDepth)
        {
            message = "Cursor is outside the block grid.";
            return false;
        }

        if (surfaceType == RoomSlotSurfaceType.Wall)
        {
            candidate.WallSide = sceneAutoPickWallSide
                ? GetNearestWallSide(localPoint, cellX, cellZ, gridWidth, gridDepth, gridSize)
                : wallSide;

            int wallSpan = RoomSlotGridUtility.GetWallSlotCount(block, candidate.WallSide);
            int maxAnchor = Mathf.Max(0, wallSpan - Mathf.Max(1, candidate.WidthUnits));
            candidate.WallGridPosition = candidate.WallSide == WallSide.North || candidate.WallSide == WallSide.South
                ? Mathf.Clamp(cellX, 0, maxAnchor)
                : Mathf.Clamp(cellZ, 0, maxAnchor);
            candidate.GridX = 0;
            candidate.GridZ = 0;
            return true;
        }

        candidate.GridX = Mathf.Clamp(cellX, 0, Mathf.Max(0, gridWidth - Mathf.Max(1, candidate.WidthUnits)));
        candidate.GridZ = Mathf.Clamp(cellZ, 0, Mathf.Max(0, gridDepth - Mathf.Max(1, candidate.DepthUnits)));
        return true;
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

    private static WallSide GetNearestWallSide(Vector3 localPoint, int cellX, int cellZ, int gridWidth, int gridDepth, float gridSize)
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
                candidateLocalCenter = candidate.localPosition;
                candidateLocalSize = candidate.surfaceType == RoomSlotSurfaceType.Floor
                    ? new Vector3(
                        Mathf.Max(1, candidate.widthUnits) * gridSize,
                        PreviewFloorHeight,
                        Mathf.Max(1, candidate.depthUnits) * gridSize)
                    : GetWallPreviewSize(
                        block,
                        new PlacementCandidate
                        {
                            SurfaceType = candidate.surfaceType,
                            WidthUnits = candidate.widthUnits,
                            DepthUnits = candidate.depthUnits,
                            WallSide = candidate.wallSide
                        });
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
            SceneView.RepaintAll();
        }
    }

    private void ReportException(string actionLabel, Exception exception)
    {
        Debug.LogError($"[RoomSlotPlacementTool] {actionLabel} failed: {exception}");
        SetStatus($"{actionLabel} failed: {exception.Message}", MessageType.Error);
    }
}
#endif
