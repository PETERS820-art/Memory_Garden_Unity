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
    private const float PreviewThickness = 0.08f;
    private const float PreviewFloorHeight = 0.04f;
    private const float PreviewWallHeight = 1f;

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

    private PreviewMode previewMode;
    private Vector2 scrollPosition;
    private string statusMessage = "Select a MemorySpaceBlock and a slot prefab to begin.";
    private MessageType statusType = MessageType.Info;
    private readonly List<string> infoMessages = new List<string>();
    private readonly List<string> warningMessages = new List<string>();
    private readonly List<string> errorMessages = new List<string>();

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
    }

    private void DrawFloorSection()
    {
        EditorGUILayout.LabelField("Floor Mode", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        floorGridX = EditorGUILayout.IntField("Grid X", floorGridX);
        floorGridZ = EditorGUILayout.IntField("Grid Z", floorGridZ);
        floorWidthUnits = Mathf.Max(1, EditorGUILayout.IntField("Width Units", floorWidthUnits));
        floorDepthUnits = Mathf.Max(1, EditorGUILayout.IntField("Depth Units", floorDepthUnits));
        floorRotationY = EditorGUILayout.FloatField("Rotation Y", floorRotationY);
        if (EditorGUI.EndChangeCheck())
        {
            RepaintPreviewIfNeeded();
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Preview Floor Slot"))
            {
                previewMode = PreviewMode.Floor;
                SceneView.RepaintAll();
            }

            if (GUILayout.Button("Place Floor Slot"))
            {
                PlaceRoomSlot(RoomSlotSurfaceType.Floor);
            }
        }

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

    private void DrawWallSection()
    {
        EditorGUILayout.LabelField("Wall Mode", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        wallSide = (WallSide)EditorGUILayout.EnumPopup("Wall Side", wallSide);
        wallGridPosition = EditorGUILayout.IntField("Wall Grid Position", wallGridPosition);
        wallWidthUnits = Mathf.Max(1, EditorGUILayout.IntField("Width Units", wallWidthUnits));
        wallHeightOffset = EditorGUILayout.FloatField("Height Offset", wallHeightOffset);
        wallRotationY = EditorGUILayout.FloatField("Rotation Y", wallRotationY);
        if (EditorGUI.EndChangeCheck())
        {
            RepaintPreviewIfNeeded();
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Preview Wall Slot"))
            {
                previewMode = PreviewMode.Wall;
                SceneView.RepaintAll();
            }

            if (GUILayout.Button("Place Wall Slot"))
            {
                PlaceRoomSlot(RoomSlotSurfaceType.Wall);
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Clear Preview"))
            {
                previewMode = PreviewMode.None;
                SceneView.RepaintAll();
            }

            if (GUILayout.Button("Validate Current Block Slots"))
            {
                ValidateCurrentBlockSlots();
            }
        }
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
        if (previewMode == PreviewMode.None)
        {
            return;
        }

        if (!TryResolveTargetBlock(targetObject, out MemorySpaceBlock targetBlock, out _))
        {
            return;
        }

        if (targetBlock == null || EditorUtility.IsPersistent(targetBlock.gameObject))
        {
            return;
        }

        if (!TryBuildPreview(targetBlock, previewMode, out Vector3 localPosition, out Quaternion localRotation, out Vector3 size, out ValidationResult previewValidation))
        {
            return;
        }

        Color previewColor = previewValidation.HasErrors
            ? new Color(1f, 0.35f, 0.35f, 1f)
            : previewValidation.HasWarnings
                ? new Color(1f, 0.75f, 0.25f, 1f)
                : new Color(0.2f, 1f, 0.75f, 1f);

        using (new Handles.DrawingScope(Matrix4x4.TRS(targetBlock.transform.TransformPoint(localPosition), targetBlock.transform.rotation * localRotation, Vector3.one)))
        {
            Handles.color = previewColor;
            Handles.DrawWireCube(Vector3.zero, size);
            Handles.ArrowHandleCap(0, Vector3.zero, Quaternion.identity, Mathf.Max(size.x, size.z) * 0.6f, EventType.Repaint);
        }
    }

    private bool TryBuildPreview(
        MemorySpaceBlock targetBlock,
        PreviewMode mode,
        out Vector3 localPosition,
        out Quaternion localRotation,
        out Vector3 previewSize,
        out ValidationResult validation)
    {
        validation = new ValidationResult();
        previewSize = Vector3.one;

        RoomSlotSurfaceType previewSurface = mode == PreviewMode.Wall ? RoomSlotSurfaceType.Wall : RoomSlotSurfaceType.Floor;
        if (!TryGetPlacementTransform(targetBlock, previewSurface, out localPosition, out localRotation, out int resolvedGridX, out int resolvedGridZ, out string errorMessage))
        {
            validation.AddError(errorMessage);
            return false;
        }

        RoomSlotPlacementMetadata snapshot = BuildPreviewSnapshot(previewSurface, resolvedGridX, resolvedGridZ);
        try
        {
            List<RoomSlotPlacementMetadata> overlaps = RoomSlotGridUtility.FindOverlaps(targetBlock, snapshot);
            if (overlaps.Count > 0)
            {
                validation.AddWarning($"Preview overlaps {overlaps.Count} existing room slot placement(s).");
            }
        }
        finally
        {
            if (snapshot != null)
            {
                DestroyImmediate(snapshot.gameObject);
            }
        }

        previewSize = previewSurface == RoomSlotSurfaceType.Floor
            ? new Vector3(
                Mathf.Max(1, floorWidthUnits) * RoomSlotGridUtility.GetGridSize(targetBlock),
                PreviewFloorHeight,
                Mathf.Max(1, floorDepthUnits) * RoomSlotGridUtility.GetGridSize(targetBlock))
            : GetWallPreviewSize(targetBlock);

        return true;
    }

    private void PlaceRoomSlot(RoomSlotSurfaceType placementSurface)
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

                if (!TryGetPlacementTransform(context.WorkingBlock, placementSurface, out Vector3 localPosition, out Quaternion localRotation, out int resolvedGridX, out int resolvedGridZ, out string errorMessage))
                {
                    SetStatus(errorMessage, MessageType.Error);
                    return;
                }

                RoomSlotPlacementMetadata previewSnapshot = BuildPreviewSnapshot(placementSurface, resolvedGridX, resolvedGridZ);
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

                ValidationResult setupValidation = EnsureFurnitureAndSlots(instance, context, slotPlacementId, placementSurface);
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
                    placementSurface,
                    slotPlacementId,
                    resolvedGridX,
                    resolvedGridZ,
                    furniture,
                    slots);

                if (resizeHitboxToGridFootprint)
                {
                    ResizeHitbox(context, instance, furniture, placementSurface);
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

            Undo.RegisterFullObjectHierarchyUndo(targetBlock.gameObject, "Remove Room Slot");
            Undo.DestroyObjectImmediate(metadata.gameObject);
            EditorUtility.SetDirty(targetBlock);
            MarkTargetDirty(targetBlock.gameObject);
            SetStatus("Removed selected room slot.", MessageType.Info);
        }
        catch (Exception exception)
        {
            ReportException("Remove Selected Room Slot", exception);
        }
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
        RoomSlotSurfaceType placementSurface)
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

        if (placementSurface == RoomSlotSurfaceType.Wall)
        {
            result.AddInfo($"Wall placement resolved to {wallSide} [{wallGridPosition}] on block {RoomSlotGridUtility.GetBlockInstanceId(context != null ? context.WorkingBlock : null)}.");
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
        RoomSlotSurfaceType placementSurface,
        string slotPlacementId,
        int resolvedGridX,
        int resolvedGridZ,
        MemoryDisplayFurniture furniture,
        MemoryDisplaySlot[] slots)
    {
        metadata.slotPlacementId = slotPlacementId;
        metadata.blockTypeId = RoomSlotGridUtility.GetBlockTypeId(block);
        metadata.blockInstanceId = RoomSlotGridUtility.GetBlockInstanceId(block);
        metadata.slotPrefabKey = prefab != null ? prefab.name : string.Empty;
        metadata.surfaceType = placementSurface;
        metadata.gridX = resolvedGridX;
        metadata.gridZ = resolvedGridZ;
        metadata.widthUnits = placementSurface == RoomSlotSurfaceType.Floor ? Mathf.Max(1, floorWidthUnits) : Mathf.Max(1, wallWidthUnits);
        metadata.depthUnits = placementSurface == RoomSlotSurfaceType.Floor ? Mathf.Max(1, floorDepthUnits) : 1;
        metadata.rotationY = placementSurface == RoomSlotSurfaceType.Floor
            ? RoomSlotGridUtility.NormalizeRotation(floorRotationY)
            : RoomSlotGridUtility.NormalizeRotation(wallRotationY);
        metadata.wallSide = wallSide;
        metadata.wallGridPosition = wallGridPosition;
        metadata.heightOffset = placementSurface == RoomSlotSurfaceType.Wall ? wallHeightOffset : 0f;
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
        RoomSlotSurfaceType placementSurface)
    {
        if (instance == null)
        {
            return;
        }

        float gridSize = RoomSlotGridUtility.GetGridSize(context.WorkingBlock);
        Vector3 desiredSize;
        switch (placementSurface)
        {
            case RoomSlotSurfaceType.Wall:
                if (wallSide == WallSide.North || wallSide == WallSide.South)
                {
                    desiredSize = new Vector3(Mathf.Max(1, wallWidthUnits) * gridSize, PreviewWallHeight, Mathf.Max(PreviewThickness, gridSize * 0.15f));
                }
                else
                {
                    desiredSize = new Vector3(Mathf.Max(PreviewThickness, gridSize * 0.15f), PreviewWallHeight, Mathf.Max(1, wallWidthUnits) * gridSize);
                }
                break;

            default:
                desiredSize = new Vector3(
                    Mathf.Max(1, floorWidthUnits) * gridSize,
                    Mathf.Max(0.1f, PreviewWallHeight),
                    Mathf.Max(1, floorDepthUnits) * gridSize);
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

    private bool TryGetPlacementTransform(
        MemorySpaceBlock targetBlock,
        RoomSlotSurfaceType placementSurface,
        out Vector3 localPosition,
        out Quaternion localRotation,
        out int resolvedGridX,
        out int resolvedGridZ,
        out string errorMessage)
    {
        resolvedGridX = 0;
        resolvedGridZ = 0;

        if (placementSurface == RoomSlotSurfaceType.Wall)
        {
            RoomSlotGridUtility.TryGetWallAnchorGrid(targetBlock, wallSide, wallGridPosition, wallWidthUnits, out resolvedGridX, out resolvedGridZ);
            return RoomSlotGridUtility.TryGetPlacementTransform(
                targetBlock,
                RoomSlotSurfaceType.Wall,
                resolvedGridX,
                resolvedGridZ,
                wallWidthUnits,
                1,
                wallRotationY,
                wallSide,
                wallGridPosition,
                wallHeightOffset,
                out localPosition,
                out localRotation,
                out errorMessage);
        }

        resolvedGridX = floorGridX;
        resolvedGridZ = floorGridZ;
        return RoomSlotGridUtility.TryGetPlacementTransform(
            targetBlock,
            RoomSlotSurfaceType.Floor,
            floorGridX,
            floorGridZ,
            floorWidthUnits,
            floorDepthUnits,
            floorRotationY,
            wallSide,
            wallGridPosition,
            0f,
            out localPosition,
            out localRotation,
            out errorMessage);
    }

    private RoomSlotPlacementMetadata BuildPreviewSnapshot(RoomSlotSurfaceType placementSurface, int resolvedGridX, int resolvedGridZ)
    {
        GameObject previewObject = new GameObject("RoomSlotPreviewSnapshot");
        RoomSlotPlacementMetadata metadata = previewObject.AddComponent<RoomSlotPlacementMetadata>();
        metadata.surfaceType = placementSurface;
        metadata.gridX = resolvedGridX;
        metadata.gridZ = resolvedGridZ;
        metadata.widthUnits = placementSurface == RoomSlotSurfaceType.Floor ? Mathf.Max(1, floorWidthUnits) : Mathf.Max(1, wallWidthUnits);
        metadata.depthUnits = placementSurface == RoomSlotSurfaceType.Floor ? Mathf.Max(1, floorDepthUnits) : 1;
        metadata.rotationY = placementSurface == RoomSlotSurfaceType.Floor
            ? RoomSlotGridUtility.NormalizeRotation(floorRotationY)
            : RoomSlotGridUtility.NormalizeRotation(wallRotationY);
        metadata.wallSide = wallSide;
        metadata.wallGridPosition = wallGridPosition;
        metadata.heightOffset = placementSurface == RoomSlotSurfaceType.Wall ? wallHeightOffset : 0f;
        return metadata;
    }

    private Vector3 GetWallPreviewSize(MemorySpaceBlock block)
    {
        float gridSize = RoomSlotGridUtility.GetGridSize(block);
        float width = Mathf.Max(1, wallWidthUnits) * gridSize;
        if (wallSide == WallSide.North || wallSide == WallSide.South)
        {
            return new Vector3(width, PreviewWallHeight, PreviewThickness);
        }

        return new Vector3(PreviewThickness, PreviewWallHeight, width);
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
