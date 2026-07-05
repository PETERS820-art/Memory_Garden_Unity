using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class MemoryDisplayFurnitureBuilderWindow : EditorWindow
{
    private enum BuilderMode
    {
        SceneUtility,
        BatchPrefabBuilder
    }

    private enum BatchBuildFilter
    {
        All,
        MissingOnly,
        ExistingOnly
    }

    private const string MenuItemPath = "Tools/Memory Garden/Display Furniture Builder";
    private const string ModelContainerObjectName = "Model";
    private const string SlotsRootObjectName = "Slots";
    private const string LightsRootObjectName = "_Lights";
    private const string FrameSurfacesRootObjectName = "_FrameSurfaces";
    private const string PlacementBoundsObjectName = "_PlacementBounds";
    private const string BlockingColliderObjectName = "_BlockingCollider";
    private const string SourceModelFolder = "Assets/_project/Art/Models/DisplayFurniture";
    private const string OutputPrefabFolder = "Assets/_project/Prefabs/DisplayFurniture";
    private const string DataFolderName = "Data";
    private const float DefaultSingleFallbackOffset = 0.25f;
    private const float DefaultSlotHeightOffset = 0.02f;
    private const float LeftRightNormalized = 0.2f;
    private const float RightNormalized = 0.8f;

    private static readonly string[] ModeLabels =
    {
        "Scene Utility Mode",
        "Batch Prefab Builder Mode"
    };

    private BuilderMode builderMode = BuilderMode.SceneUtility;

    private GameObject furnitureTarget;
    private DisplayFurnitureAuthoringMode authoringMode = DisplayFurnitureAuthoringMode.Display;
    private FurnitureType displayFurnitureType = FurnitureType.Shelf;
    private SlotPreset displaySlotPreset = SlotPreset.ShelfLeftCenterRight;
    private LightFeaturePreset lightFeaturePreset = LightFeaturePreset.SinglePoint;
    private FrameSurfacePreset frameSurfacePreset = FrameSurfacePreset.SingleQuad;
    private bool customEnableDisplayFeature = true;
    private bool customEnableLightFeature;
    private bool customEnableFrameFeature;
    private float slotHeightOffset = DefaultSlotHeightOffset;
    private Vector3 boundsPadding = Vector3.zero;
    private bool clearExistingSlots = true;
    private bool createOrUpdatePlacementBounds = true;
    private bool autoFitBoundsFromRenderers = true;
    private bool createBlockingColliderOption = true;
    private bool generateSlotsFromPlacementBounds = true;
    private bool saveAsPrefab;
    private string prefabFolder = OutputPrefabFolder;

    [MenuItem(MenuItemPath)]
    public static void OpenWindow()
    {
        MemoryDisplayFurnitureBuilderWindow window =
            GetWindow<MemoryDisplayFurnitureBuilderWindow>("Display Furniture Builder");
        window.minSize = new Vector2(420f, 320f);
        window.Show();
    }

    private void OnGUI()
    {
        builderMode = (BuilderMode)GUILayout.Toolbar((int)builderMode, ModeLabels);
        EditorGUILayout.Space();

        switch (builderMode)
        {
            case BuilderMode.SceneUtility:
                DrawSceneUtilityMode();
                break;

            case BuilderMode.BatchPrefabBuilder:
                DrawBatchPrefabBuilderMode();
                break;
        }
    }

    private void DrawSceneUtilityMode()
    {
        EditorGUILayout.LabelField("Furniture Target", EditorStyles.boldLabel);
        furnitureTarget = (GameObject)EditorGUILayout.ObjectField(
            "Target",
            furnitureTarget,
            typeof(GameObject),
            true);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Use Selected GameObject"))
            {
                furnitureTarget = Selection.activeGameObject;
                Repaint();
            }

            using (new EditorGUI.DisabledScope(Selection.activeGameObject == null))
            {
                if (GUILayout.Button("Use Active Selection"))
                {
                    furnitureTarget = Selection.activeGameObject;
                    Repaint();
                }
            }
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Build Settings", EditorStyles.boldLabel);
        authoringMode = (DisplayFurnitureAuthoringMode)EditorGUILayout.EnumPopup("Furniture Typing", authoringMode);
        DrawAuthoringFeatureSettings();

        bool sceneDisplayFeatureEnabled = authoringMode == DisplayFurnitureAuthoringMode.Display
            || (authoringMode == DisplayFurnitureAuthoringMode.Custom && customEnableDisplayFeature);
        if (sceneDisplayFeatureEnabled)
        {
            slotHeightOffset = EditorGUILayout.FloatField("Slot Height Offset", slotHeightOffset);
            generateSlotsFromPlacementBounds = EditorGUILayout.Toggle("Generate Slots From Placement Bounds", generateSlotsFromPlacementBounds);
        }

        boundsPadding = EditorGUILayout.Vector3Field("Bounds Padding", boundsPadding);
        clearExistingSlots = EditorGUILayout.Toggle("Clear Existing Slots", clearExistingSlots);
        createOrUpdatePlacementBounds = EditorGUILayout.Toggle("Create / Update Placement Bounds", createOrUpdatePlacementBounds);
        autoFitBoundsFromRenderers = EditorGUILayout.Toggle("Auto Fit Bounds From Renderers", autoFitBoundsFromRenderers);
        createBlockingColliderOption = EditorGUILayout.Toggle("Create Blocking Collider", createBlockingColliderOption);
        saveAsPrefab = EditorGUILayout.Toggle("Save As Prefab", saveAsPrefab);
        using (new EditorGUI.DisabledScope(!saveAsPrefab))
        {
            prefabFolder = EditorGUILayout.TextField("Prefab Folder", prefabFolder);
        }

        EditorGUILayout.Space();

        using (new EditorGUI.DisabledScope(furnitureTarget == null))
        {
            if (GUILayout.Button("Build / Update Selected Scene Furniture"))
            {
                BuildOrUpdateSelectedSceneFurniture();
            }
        }

        using (new EditorGUI.DisabledScope(furnitureTarget == null))
        {
            if (GUILayout.Button("Capture Scene Adjustments + Overwrite System Prefab"))
            {
                CaptureSceneAdjustmentsAndOverwriteFurniturePrefab();
            }
        }

        using (new EditorGUI.DisabledScope(Selection.gameObjects == null || Selection.gameObjects.Length == 0))
        {
            if (GUILayout.Button("Configure Placement On Selected Memory Items"))
            {
                ConfigurePlacementOnSelection();
            }
        }
    }

    private void DrawBatchPrefabBuilderMode()
    {
        EditorGUILayout.HelpBox(
            "Select one or more Display Furniture FBX assets or source folders in the Project window to use the selected-build action.",
            MessageType.Info);

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.TextField("Source Models", SourceModelFolder);
            EditorGUILayout.TextField("Prefab Output", OutputPrefabFolder);
            EditorGUILayout.TextField("Build Profiles", "<Each DF folder>/Data");
        }

        EditorGUILayout.Space();
        if (GUILayout.Button("Create Missing Folders"))
        {
            CreateMissingFolders();
        }

        if (GUILayout.Button("Generate Missing Profiles"))
        {
            GenerateMissingProfiles();
        }

        EditorGUILayout.Space();

        using (new EditorGUI.DisabledScope(!HasSelectedBatchSource()))
        {
            if (GUILayout.Button("Build Selected Furniture Prefab"))
            {
                BuildSelectedFurniturePrefabs();
            }
        }

        if (GUILayout.Button("Build All Display Furniture Prefabs"))
        {
            BuildDisplayFurniturePrefabs(BatchBuildFilter.MissingOnly);
        }

        if (GUILayout.Button("Rebuild Existing Prefabs"))
        {
            if (ConfirmRebuildExistingPrefabs("Display Furniture"))
            {
                BuildDisplayFurniturePrefabs(BatchBuildFilter.ExistingOnly);
            }
        }
    }

    private void DrawAuthoringFeatureSettings()
    {
        switch (authoringMode)
        {
            case DisplayFurnitureAuthoringMode.Display:
                displayFurnitureType = (FurnitureType)EditorGUILayout.EnumPopup("Display Shape", displayFurnitureType);
                displaySlotPreset = (SlotPreset)EditorGUILayout.EnumPopup("Slot Preset", displaySlotPreset);
                DrawDisplayPresetHelp(displaySlotPreset);
                EditorGUILayout.HelpBox(
                    "Display mode focuses on slot-bearing furniture such as shelves, plinths, floor pads, and wall shelves.",
                    MessageType.Info);
                break;

            case DisplayFurnitureAuthoringMode.Light:
                lightFeaturePreset = (LightFeaturePreset)EditorGUILayout.EnumPopup("Light Scheme", lightFeaturePreset);
                DrawLightPresetHelp(lightFeaturePreset);
                break;

            case DisplayFurnitureAuthoringMode.Frame:
                frameSurfacePreset = (FrameSurfacePreset)EditorGUILayout.EnumPopup("Image Area Scheme", frameSurfacePreset);
                DrawFramePresetHelp(frameSurfacePreset);
                break;

            case DisplayFurnitureAuthoringMode.Custom:
                customEnableDisplayFeature = EditorGUILayout.Toggle("Enable Display", customEnableDisplayFeature);
                if (customEnableDisplayFeature)
                {
                    displayFurnitureType = (FurnitureType)EditorGUILayout.EnumPopup("Display Shape", displayFurnitureType);
                    displaySlotPreset = (SlotPreset)EditorGUILayout.EnumPopup("Slot Preset", displaySlotPreset);
                    DrawDisplayPresetHelp(displaySlotPreset);
                }

                customEnableLightFeature = EditorGUILayout.Toggle("Enable Lighting", customEnableLightFeature);
                if (customEnableLightFeature)
                {
                    lightFeaturePreset = (LightFeaturePreset)EditorGUILayout.EnumPopup("Light Scheme", lightFeaturePreset);
                    DrawLightPresetHelp(lightFeaturePreset);
                }

                customEnableFrameFeature = EditorGUILayout.Toggle("Enable Frame", customEnableFrameFeature);
                if (customEnableFrameFeature)
                {
                    frameSurfacePreset = (FrameSurfacePreset)EditorGUILayout.EnumPopup("Image Area Scheme", frameSurfacePreset);
                    DrawFramePresetHelp(frameSurfacePreset);
                }

                if (!customEnableDisplayFeature && !customEnableLightFeature && !customEnableFrameFeature)
                {
                    EditorGUILayout.HelpBox(
                        "No feature is enabled. Build will keep only the furniture root/model container, which is useful for an empty custom object.",
                        MessageType.Info);
                }
                else if (customEnableDisplayFeature && customEnableLightFeature)
                {
                    EditorGUILayout.HelpBox(
                        "Custom mode can combine display and lighting, so a furniture piece can behave like both a shelf and a lamp.",
                        MessageType.Info);
                }
                break;
        }

        EditorGUILayout.HelpBox(
            "For manual authoring, place MemoryDisplayLight or MemoryDisplayFrameSurface on child objects, then use overwrite/save to capture those adjustments into the build profile.",
            MessageType.None);
    }

    private static void DrawDisplayPresetHelp(SlotPreset preset)
    {
        if (preset != SlotPreset.Custom)
        {
            return;
        }

        EditorGUILayout.HelpBox(
            "Custom slot preset does not auto-generate MemoryDisplaySlot objects. Use it when you want to author slot transforms manually or disable auto slot creation.",
            MessageType.Info);
    }

    private static void DrawLightPresetHelp(LightFeaturePreset preset)
    {
        string message = preset switch
        {
            LightFeaturePreset.SinglePoint => "Single Point creates one managed point light under _Lights.",
            LightFeaturePreset.SingleSpot => "Single Spot creates one managed spotlight under _Lights.",
            LightFeaturePreset.Custom => "Custom does not auto-create lights. Add or adjust MemoryDisplayLight children manually, then overwrite/save.",
            _ => "None skips light generation."
        };

        EditorGUILayout.HelpBox(message, MessageType.Info);
    }

    private static void DrawFramePresetHelp(FrameSurfacePreset preset)
    {
        string message = preset switch
        {
            FrameSurfacePreset.SingleQuad => "Single Quad creates one managed image display surface under _FrameSurfaces.",
            FrameSurfacePreset.Custom => "Custom does not auto-create image areas. Add or adjust MemoryDisplayFrameSurface children manually, then overwrite/save.",
            _ => "None skips frame surface generation."
        };

        EditorGUILayout.HelpBox(message, MessageType.Info);
    }

    private void BuildOrUpdateSelectedSceneFurniture()
    {
        if (furnitureTarget == null)
        {
            Debug.LogWarning("[MemoryDisplayFurnitureBuilderWindow] No furniture target selected.");
            return;
        }

        FurnitureBuildSettings settings = CreateSceneBuildSettings();
        BuildResult result = BuildFurniture(furnitureTarget, settings, useUndo: true);

        if (saveAsPrefab)
        {
            SaveFurnitureAsPrefab(furnitureTarget);
        }

        Debug.Log(
            $"[MemoryDisplayFurnitureBuilderWindow] Built {result.SlotCount} slot(s) for {furnitureTarget.name}.",
            furnitureTarget);
    }

    private void ConfigurePlacementOnSelection()
    {
        GameObject[] selectedObjects = Selection.gameObjects;
        if (selectedObjects == null || selectedObjects.Length == 0)
        {
            Debug.LogWarning("[MemoryDisplayFurnitureBuilderWindow] No selected Memory Items found.");
            return;
        }

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Configure Memory Item Placement");
        int undoGroup = Undo.GetCurrentGroup();

        HashSet<GameObject> processedTargets = new HashSet<GameObject>();
        int configuredCount = 0;

        for (int i = 0; i < selectedObjects.Length; i++)
        {
            GameObject target = ResolveMemoryItemTarget(selectedObjects[i]);
            if (target == null || !processedTargets.Add(target))
            {
                continue;
            }

            bool wasMissingMemoryObject = target.GetComponent<MemoryObject>() == null;
            MemoryObject memoryObject = GetOrAddComponent<MemoryObject>(target, useUndo: true);
            EnsurePlacementDefaults(memoryObject, wasMissingMemoryObject);

            configuredCount++;
            SetDirty(memoryObject);
            SetDirty(target);
        }

        Undo.CollapseUndoOperations(undoGroup);
        Debug.Log(
            $"[MemoryDisplayFurnitureBuilderWindow] Configured placement settings on {configuredCount} object(s).");
    }

    private void CreateMissingFolders()
    {
        int createdFolderCount = EnsureRequiredFolders();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string message = createdFolderCount > 0
            ? $"Created {createdFolderCount} folder(s) for Display Furniture."
            : "All Display Furniture folders already exist.";
        ShowSummary("Display Furniture Builder", message);
    }

    private void GenerateMissingProfiles()
    {
        EnsureRequiredFolders();

        List<DisplayFurnitureSourceEntry> sources = DiscoverDisplayFurnitureSources();
        if (sources.Count == 0)
        {
            ShowSummary("Display Furniture Profiles", $"No FBX files found under {SourceModelFolder}.");
            return;
        }

        int createdProfiles = 0;
        for (int i = 0; i < sources.Count; i++)
        {
            EnsureBuildProfile(sources[i], out bool wasCreated);
            if (wasCreated)
            {
                createdProfiles++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string message = createdProfiles > 0
            ? $"Created {createdProfiles} missing build profile(s)."
            : "All Display Furniture build profiles already exist.";
        ShowSummary("Display Furniture Profiles", message);
    }

    private void BuildSelectedFurniturePrefabs()
    {
        EnsureRequiredFolders();

        List<DisplayFurnitureSourceEntry> selectedSources = DiscoverSelectedDisplayFurnitureSources();
        if (selectedSources.Count == 0)
        {
            ShowSummary(
                "Display Furniture Prefabs",
                "Select one or more Display Furniture FBX assets or source folders in the Project window.");
            return;
        }

        BuildDisplayFurniturePrefabs(selectedSources, BatchBuildFilter.All, "Built selected Display Furniture prefab(s)");
    }

    private void BuildDisplayFurniturePrefabs(BatchBuildFilter filter)
    {
        EnsureRequiredFolders();

        List<DisplayFurnitureSourceEntry> sources = DiscoverDisplayFurnitureSources();
        if (sources.Count == 0)
        {
            ShowSummary("Display Furniture Prefabs", $"No FBX files found under {SourceModelFolder}.");
            return;
        }

        string message = filter switch
        {
            BatchBuildFilter.MissingOnly => "Built missing Display Furniture prefab(s)",
            BatchBuildFilter.ExistingOnly => "Rebuilt existing Display Furniture prefab(s)",
            _ => "Built Display Furniture prefab(s)"
        };

        BuildDisplayFurniturePrefabs(sources, filter, message);
    }

    private void BuildDisplayFurniturePrefabs(
        List<DisplayFurnitureSourceEntry> sources,
        BatchBuildFilter filter,
        string summaryAction)
    {
        EnsureRequiredFolders();

        List<DisplayFurnitureSourceEntry> targets = FilterBatchSources(sources, filter);
        if (targets.Count == 0)
        {
            string message = filter switch
            {
                BatchBuildFilter.MissingOnly => "No missing Display Furniture prefabs were found.",
                BatchBuildFilter.ExistingOnly => "No existing Display Furniture prefabs were found to rebuild.",
                _ => "No Display Furniture sources were selected."
            };

            ShowSummary("Display Furniture Prefabs", message);
            return;
        }

        int createdProfiles = 0;

        try
        {
            for (int i = 0; i < targets.Count; i++)
            {
                DisplayFurnitureSourceEntry source = targets[i];
                EditorUtility.DisplayProgressBar(
                    "Display Furniture Builder",
                    $"Building {source.ModelId} ({i + 1}/{targets.Count})",
                    (float)(i + 1) / targets.Count);

                DisplayFurnitureBuildProfile profile = EnsureBuildProfile(source, out bool wasCreated);
                if (wasCreated)
                {
                    createdProfiles++;
                }

                FurnitureBuildSettings settings = CreateBatchBuildSettings(source.ModelId, profile);
                BuildPrefabFromSource(source, settings);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        catch (Exception exception)
        {
            Debug.LogError($"[MemoryDisplayFurnitureBuilderWindow] Failed while building Display Furniture prefabs: {exception}");
            ShowSummary("Display Furniture Prefabs", exception.Message);
            throw;
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        string summary = createdProfiles > 0
            ? $"{summaryAction}: {targets.Count} prefab(s), {createdProfiles} profile(s) created."
            : $"{summaryAction}: {targets.Count} prefab(s).";
        ShowSummary("Display Furniture Prefabs", summary);
    }

    private void BuildPrefabFromSource(DisplayFurnitureSourceEntry source, FurnitureBuildSettings settings)
    {
        GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(source.SourceAssetPath);
        if (modelAsset == null)
        {
            throw new InvalidOperationException($"Could not load Display Furniture model at {source.SourceAssetPath}.");
        }

        string prefabFolderPath = Path.GetDirectoryName(source.PrefabAssetPath)?.Replace("\\", "/");
        if (string.IsNullOrWhiteSpace(prefabFolderPath))
        {
            throw new InvalidOperationException($"Could not resolve prefab folder for {source.SourceAssetPath}.");
        }

        EnsureAssetFolder(prefabFolderPath);

        GameObject root = new GameObject(source.PrefabRootName);

        try
        {
            Transform modelContainer = FindOrCreateChild(root.transform, ModelContainerObjectName, useUndo: false);
            GameObject modelInstance = PrefabUtility.InstantiatePrefab(modelAsset) as GameObject;
            if (modelInstance == null)
            {
                modelInstance = UnityEngine.Object.Instantiate(modelAsset);
            }

            modelInstance.name = modelAsset.name;
            modelInstance.transform.SetParent(modelContainer, false);
            modelInstance.transform.localPosition = Vector3.zero;
            modelInstance.transform.localRotation = Quaternion.identity;
            modelInstance.transform.localScale = Vector3.one;

            ApplyPrefabRootOverride(root.transform, settings.PrefabProfile != null ? settings.PrefabProfile.prefabRootTransform : null);
            ApplyTransformOverride(modelContainer, settings.PrefabProfile != null ? settings.PrefabProfile.modelContainerTransform : null);
            ApplyTransformOverride(modelInstance.transform, settings.PrefabProfile != null ? settings.PrefabProfile.modelAssetTransform : null);

            BuildFurniture(root, settings, useUndo: false);
            ApplyPlacementBoundsOverride(root.transform, settings.PrefabProfile);
            ApplySlotOverrides(root.transform, settings.PrefabProfile);
            ApplyLightOverrides(root.transform, settings.PrefabProfile);
            ApplyFrameSurfaceOverrides(root.transform, settings.PrefabProfile);

            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, source.PrefabAssetPath);
            if (savedPrefab == null)
            {
                throw new InvalidOperationException($"Failed to save Display Furniture prefab at {source.PrefabAssetPath}.");
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private BuildResult BuildFurniture(GameObject target, FurnitureBuildSettings settings, bool useUndo)
    {
        int undoGroup = -1;

        if (useUndo)
        {
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Build Display Furniture");
            undoGroup = Undo.GetCurrentGroup();
        }

        try
        {
            MemoryDisplayFurniture furniture = GetOrAddComponent<MemoryDisplayFurniture>(target, useUndo);
            ApplyFurnitureSettings(furniture, settings, target.name);

            BoxCollider placementBoundsCollider = null;
            if (settings.CreatePlacementBounds || settings.GenerateSlotsFromPlacementBounds || settings.CreateBlockingCollider)
            {
                placementBoundsCollider = CreateOrUpdatePlacementBounds(target.transform, furniture, settings, useUndo);
            }
            else
            {
                RemoveChildIfExists(target.transform, PlacementBoundsObjectName, useUndo);
                AssignPlacementBoundsReference(furniture, null);
            }

            if (settings.CreateBlockingCollider && placementBoundsCollider != null)
            {
                CreateOrUpdateBlockingCollider(target.transform, placementBoundsCollider, useUndo);
            }
            else
            {
                RemoveChildIfExists(target.transform, BlockingColliderObjectName, useUndo);
            }

            List<SlotDefinition> slotDefinitions = new List<SlotDefinition>();
            Transform slotsRoot = target.transform.Find(SlotsRootObjectName);
            if (settings.EnableDisplayFeature)
            {
                slotsRoot = FindOrCreateChild(target.transform, SlotsRootObjectName, useUndo);
                if (settings.ClearExistingSlots)
                {
                    ClearExistingSlotChildren(slotsRoot, useUndo);
                }

                slotDefinitions = CreateSlotDefinitions(target.transform, settings, placementBoundsCollider);
                for (int i = 0; i < slotDefinitions.Count; i++)
                {
                    CreateOrUpdateSlot(slotsRoot, slotDefinitions[i], useUndo);
                }
            }
            else
            {
                RemoveChildIfExists(target.transform, SlotsRootObjectName, useUndo);
            }

            if (settings.EnableLightFeature)
            {
                ConfigureLightFeatures(target.transform, settings, useUndo);
            }
            else
            {
                RemoveChildIfExists(target.transform, LightsRootObjectName, useUndo);
            }

            if (settings.EnableFrameFeature)
            {
                ConfigureFrameSurfaces(target.transform, settings, useUndo);
            }
            else
            {
                RemoveChildIfExists(target.transform, FrameSurfacesRootObjectName, useUndo);
            }

            furniture.AutoCollectFeatures();

            SetDirty(target);
            SetDirty(furniture);
            if (slotsRoot != null)
            {
                SetDirty(slotsRoot.gameObject);
            }

            IReadOnlyList<MemoryDisplaySlot> slots = furniture.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                MemoryDisplaySlot slot = slots[i];
                if (slot == null)
                {
                    continue;
                }

                SetDirty(slot);
                SetDirty(slot.gameObject);
            }

            return new BuildResult(furniture, slotDefinitions.Count);
        }
        finally
        {
            if (useUndo && undoGroup >= 0)
            {
                Undo.CollapseUndoOperations(undoGroup);
            }
        }
    }

    private void ApplyFurnitureSettings(MemoryDisplayFurniture furniture, FurnitureBuildSettings settings, string fallbackId)
    {
        SerializedObject serializedFurniture = new SerializedObject(furniture);
        serializedFurniture.UpdateIfRequiredOrScript();

        SerializedProperty furnitureIdProperty = serializedFurniture.FindProperty("furnitureId");
        if (furnitureIdProperty != null)
        {
            furnitureIdProperty.stringValue = string.IsNullOrWhiteSpace(settings.FurnitureId)
                ? fallbackId
                : settings.FurnitureId;
        }

        SerializedProperty furnitureTypeProperty = serializedFurniture.FindProperty("furnitureType");
        if (furnitureTypeProperty != null)
        {
            furnitureTypeProperty.enumValueIndex = (int)settings.FurnitureType;
        }

        SerializedProperty usePlacementBoundsForSlotsProperty = serializedFurniture.FindProperty("usePlacementBoundsForSlots");
        if (usePlacementBoundsForSlotsProperty != null)
        {
            usePlacementBoundsForSlotsProperty.boolValue = settings.EnableDisplayFeature && settings.GenerateSlotsFromPlacementBounds;
        }

        SerializedProperty createBlockingColliderProperty = serializedFurniture.FindProperty("createBlockingCollider");
        if (createBlockingColliderProperty != null)
        {
            createBlockingColliderProperty.boolValue = settings.CreateBlockingCollider;
        }

        serializedFurniture.ApplyModifiedPropertiesWithoutUndo();
    }

    private BoxCollider CreateOrUpdatePlacementBounds(
        Transform furnitureTransform,
        MemoryDisplayFurniture furniture,
        FurnitureBuildSettings settings,
        bool useUndo)
    {
        Transform placementBoundsTransform = FindOrCreateChild(furnitureTransform, PlacementBoundsObjectName, useUndo);
        SetLocalTransformIdentity(placementBoundsTransform, useUndo, "Reset Placement Bounds");

        BoxCollider placementBoundsCollider = GetOrAddComponent<BoxCollider>(placementBoundsTransform.gameObject, useUndo);
        if (useUndo)
        {
            Undo.RecordObject(placementBoundsCollider, "Update Placement Bounds");
        }

        AssignPlacementBoundsReference(furniture, placementBoundsCollider);

        if (settings.AutoFitBoundsFromRenderers)
        {
            FitColliderFromRenderers(furnitureTransform, placementBoundsCollider, settings.BoundsPadding);
        }

        placementBoundsCollider.isTrigger = true;

        SetDirty(placementBoundsTransform.gameObject);
        SetDirty(placementBoundsCollider);
        return placementBoundsCollider;
    }

    private void CreateOrUpdateBlockingCollider(
        Transform furnitureTransform,
        BoxCollider placementBoundsCollider,
        bool useUndo)
    {
        Transform blockingColliderTransform = FindOrCreateChild(furnitureTransform, BlockingColliderObjectName, useUndo);
        BoxCollider blockingCollider = GetOrAddComponent<BoxCollider>(blockingColliderTransform.gameObject, useUndo);

        if (useUndo)
        {
            Undo.RecordObject(blockingColliderTransform, "Update Blocking Collider");
            Undo.RecordObject(blockingCollider, "Update Blocking Collider");
        }

        blockingColliderTransform.localPosition = placementBoundsCollider.transform.localPosition;
        blockingColliderTransform.localRotation = placementBoundsCollider.transform.localRotation;
        blockingColliderTransform.localScale = placementBoundsCollider.transform.localScale;

        blockingCollider.center = placementBoundsCollider.center;
        blockingCollider.size = placementBoundsCollider.size;
        blockingCollider.isTrigger = false;

        SetDirty(blockingColliderTransform.gameObject);
        SetDirty(blockingCollider);
    }

    private static void AssignPlacementBoundsReference(MemoryDisplayFurniture furniture, BoxCollider placementBoundsCollider)
    {
        SerializedObject serializedFurniture = new SerializedObject(furniture);
        serializedFurniture.UpdateIfRequiredOrScript();

        SerializedProperty placementBoundsProperty = serializedFurniture.FindProperty("placementBoundsCollider");
        if (placementBoundsProperty != null)
        {
            placementBoundsProperty.objectReferenceValue = placementBoundsCollider;
            serializedFurniture.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void FitColliderFromRenderers(
        Transform furnitureTransform,
        BoxCollider placementBoundsCollider,
        Vector3 boundsPadding)
    {
        Vector3 clampedPadding = ClampToNonNegative(boundsPadding);

        if (!TryGetCombinedLocalRendererBounds(furnitureTransform, out Bounds localBounds))
        {
            placementBoundsCollider.center = Vector3.zero;
            placementBoundsCollider.size = Vector3.Max(Vector3.one * 0.25f + (clampedPadding * 2f), Vector3.one * 0.01f);
            return;
        }

        localBounds.Expand(clampedPadding * 2f);
        ApplyFurnitureLocalBoundsToCollider(placementBoundsCollider, furnitureTransform, localBounds);
    }

    private static void ApplyFurnitureLocalBoundsToCollider(
        BoxCollider collider,
        Transform furnitureTransform,
        Bounds furnitureLocalBounds)
    {
        Vector3[] worldCorners = new Vector3[8];
        Vector3 center = furnitureLocalBounds.center;
        Vector3 extents = furnitureLocalBounds.extents;
        int index = 0;

        for (int x = -1; x <= 1; x += 2)
        {
            for (int y = -1; y <= 1; y += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 localCorner = center + new Vector3(
                        extents.x * x,
                        extents.y * y,
                        extents.z * z);
                    worldCorners[index] = furnitureTransform.TransformPoint(localCorner);
                    index++;
                }
            }
        }

        Vector3 min = Vector3.zero;
        Vector3 max = Vector3.zero;
        bool hasCorner = false;

        for (int i = 0; i < worldCorners.Length; i++)
        {
            Vector3 colliderLocalCorner = collider.transform.InverseTransformPoint(worldCorners[i]);
            if (!hasCorner)
            {
                min = colliderLocalCorner;
                max = colliderLocalCorner;
                hasCorner = true;
                continue;
            }

            min = Vector3.Min(min, colliderLocalCorner);
            max = Vector3.Max(max, colliderLocalCorner);
        }

        if (!hasCorner)
        {
            collider.center = Vector3.zero;
            collider.size = Vector3.one * 0.01f;
            return;
        }

        collider.center = (min + max) * 0.5f;
        collider.size = Vector3.Max(max - min, Vector3.one * 0.01f);
    }

    private static Transform FindOrCreateChild(Transform parent, string childName, bool useUndo)
    {
        Transform child = parent.Find(childName);
        if (child != null)
        {
            return child;
        }

        GameObject childObject = new GameObject(childName);
        if (useUndo)
        {
            Undo.RegisterCreatedObjectUndo(childObject, $"Create {childName}");
        }

        child = childObject.transform;
        child.SetParent(parent, false);
        child.localPosition = Vector3.zero;
        child.localRotation = Quaternion.identity;
        child.localScale = Vector3.one;
        return child;
    }

    private static void SetLocalTransformIdentity(Transform transformToReset, bool useUndo, string undoLabel)
    {
        if (transformToReset == null)
        {
            return;
        }

        if (useUndo)
        {
            Undo.RecordObject(transformToReset, undoLabel);
        }

        transformToReset.localPosition = Vector3.zero;
        transformToReset.localRotation = Quaternion.identity;
        transformToReset.localScale = Vector3.one;
    }

    private static void RemoveChildIfExists(Transform parent, string childName, bool useUndo)
    {
        Transform child = parent.Find(childName);
        if (child == null)
        {
            return;
        }

        if (useUndo)
        {
            Undo.DestroyObjectImmediate(child.gameObject);
            return;
        }

        UnityEngine.Object.DestroyImmediate(child.gameObject);
    }

    private static void ClearExistingSlotChildren(Transform slotsRoot, bool useUndo)
    {
        for (int i = slotsRoot.childCount - 1; i >= 0; i--)
        {
            if (useUndo)
            {
                Undo.DestroyObjectImmediate(slotsRoot.GetChild(i).gameObject);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(slotsRoot.GetChild(i).gameObject);
            }
        }
    }

    private static void ClearChildObjects(Transform parent, bool useUndo)
    {
        if (parent == null)
        {
            return;
        }

        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            if (useUndo)
            {
                Undo.DestroyObjectImmediate(parent.GetChild(i).gameObject);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(parent.GetChild(i).gameObject);
            }
        }
    }

    private void ConfigureLightFeatures(Transform furnitureRoot, FurnitureBuildSettings settings, bool useUndo)
    {
        if (furnitureRoot == null)
        {
            return;
        }

        switch (settings.LightFeaturePreset)
        {
            case LightFeaturePreset.None:
                RemoveChildIfExists(furnitureRoot, LightsRootObjectName, useUndo);
                return;

            case LightFeaturePreset.Custom:
                return;
        }

        Transform lightsRoot = FindOrCreateChild(furnitureRoot, LightsRootObjectName, useUndo);
        ClearChildObjects(lightsRoot, useUndo);

        switch (settings.LightFeaturePreset)
        {
            case LightFeaturePreset.SinglePoint:
                CreateDisplayLight(lightsRoot, "Light_Main", LightType.Point);
                break;

            case LightFeaturePreset.SingleSpot:
                CreateDisplayLight(lightsRoot, "Light_Main", LightType.Spot);
                break;
        }
    }

    private void ConfigureFrameSurfaces(Transform furnitureRoot, FurnitureBuildSettings settings, bool useUndo)
    {
        if (furnitureRoot == null)
        {
            return;
        }

        switch (settings.FrameSurfacePreset)
        {
            case FrameSurfacePreset.None:
                RemoveChildIfExists(furnitureRoot, FrameSurfacesRootObjectName, useUndo);
                return;

            case FrameSurfacePreset.Custom:
                return;
        }

        Transform frameRoot = FindOrCreateChild(furnitureRoot, FrameSurfacesRootObjectName, useUndo);
        ClearChildObjects(frameRoot, useUndo);

        if (settings.FrameSurfacePreset == FrameSurfacePreset.SingleQuad)
        {
            CreateFrameSurface(frameRoot, "FrameSurface_Main", createQuadIfMissing: true);
        }
    }

    private void CreateOrUpdateSlot(Transform slotsRoot, SlotDefinition definition, bool useUndo)
    {
        Transform slotTransform = slotsRoot.Find(definition.Name);
        GameObject slotObject;

        if (slotTransform == null)
        {
            slotObject = new GameObject(definition.Name);
            if (useUndo)
            {
                Undo.RegisterCreatedObjectUndo(slotObject, "Create Display Slot");
            }

            slotTransform = slotObject.transform;
            slotTransform.SetParent(slotsRoot, false);
        }
        else
        {
            slotObject = slotTransform.gameObject;
            if (useUndo)
            {
                Undo.RecordObject(slotTransform, "Update Display Slot");
            }
        }

        slotObject.name = definition.Name;
        slotTransform.localPosition = definition.LocalPosition;
        slotTransform.localRotation = Quaternion.identity;
        slotTransform.localScale = Vector3.one;

        MemoryDisplaySlot slot = GetOrAddComponent<MemoryDisplaySlot>(slotObject, useUndo);
        ApplySlotSettings(slot, definition);

        SetDirty(slotTransform);
        SetDirty(slotObject);
        SetDirty(slot);
    }

    private static void ApplySlotSettings(MemoryDisplaySlot slot, SlotDefinition definition)
    {
        SerializedObject serializedSlot = new SerializedObject(slot);
        serializedSlot.UpdateIfRequiredOrScript();

        SerializedProperty slotIdProperty = serializedSlot.FindProperty("slotId");
        if (slotIdProperty != null)
        {
            slotIdProperty.stringValue = definition.Name;
        }

        SerializedProperty slotTypeProperty = serializedSlot.FindProperty("slotType");
        if (slotTypeProperty != null)
        {
            slotTypeProperty.enumValueIndex = (int)definition.Type;
        }

        SerializedProperty acceptedItemSizesProperty = serializedSlot.FindProperty("acceptedItemSizes");
        if (acceptedItemSizesProperty != null && acceptedItemSizesProperty.isArray)
        {
            acceptedItemSizesProperty.arraySize = definition.AcceptedSizes.Count;
            for (int i = 0; i < definition.AcceptedSizes.Count; i++)
            {
                SerializedProperty element = acceptedItemSizesProperty.GetArrayElementAtIndex(i);
                element.enumValueIndex = (int)definition.AcceptedSizes[i];
            }
        }

        SerializedProperty snapRadiusProperty = serializedSlot.FindProperty("snapRadius");
        if (snapRadiusProperty != null)
        {
            snapRadiusProperty.floatValue = 0.8f;
        }

        SerializedProperty useSmoothSnapProperty = serializedSlot.FindProperty("useSmoothSnap");
        if (useSmoothSnapProperty != null)
        {
            useSmoothSnapProperty.boolValue = true;
        }

        SerializedProperty snapDurationProperty = serializedSlot.FindProperty("snapDuration");
        if (snapDurationProperty != null)
        {
            snapDurationProperty.floatValue = 0.2f;
        }

        SerializedProperty occupiedProperty = serializedSlot.FindProperty("isOccupied");
        if (occupiedProperty != null)
        {
            occupiedProperty.boolValue = false;
        }

        SerializedProperty occupiedItemProperty = serializedSlot.FindProperty("occupiedItem");
        if (occupiedItemProperty != null)
        {
            occupiedItemProperty.objectReferenceValue = null;
        }

        serializedSlot.ApplyModifiedPropertiesWithoutUndo();
    }

    private List<SlotDefinition> CreateSlotDefinitions(
        Transform furnitureTransform,
        FurnitureBuildSettings settings,
        BoxCollider placementBoundsCollider)
    {
        if (!settings.EnableDisplayFeature || settings.SlotPreset == SlotPreset.Custom)
        {
            return new List<SlotDefinition>();
        }

        bool hasLocalBounds = false;
        Bounds localBounds = default;

        if (settings.GenerateSlotsFromPlacementBounds && placementBoundsCollider != null)
        {
            hasLocalBounds = TryGetColliderBoundsInRootLocal(placementBoundsCollider, furnitureTransform, out localBounds);
        }

        if (!hasLocalBounds)
        {
            hasLocalBounds = TryGetCombinedLocalRendererBounds(furnitureTransform, out localBounds);
        }

        float topY = hasLocalBounds ? localBounds.max.y + settings.SlotHeightOffset : settings.SlotHeightOffset;
        float centerX = hasLocalBounds ? localBounds.center.x : 0f;
        float centerZ = hasLocalBounds ? localBounds.center.z : 0f;
        float leftX = hasLocalBounds
            ? Mathf.Lerp(localBounds.min.x, localBounds.max.x, LeftRightNormalized)
            : -DefaultSingleFallbackOffset;
        float rightX = hasLocalBounds
            ? Mathf.Lerp(localBounds.min.x, localBounds.max.x, RightNormalized)
            : DefaultSingleFallbackOffset;

        List<SlotDefinition> definitions = new List<SlotDefinition>();

        switch (settings.SlotPreset)
        {
            case SlotPreset.SingleCenter:
                definitions.Add(new SlotDefinition(
                    "Slot_Center",
                    new Vector3(centerX, topY, centerZ),
                    ResolveSingleCenterSlotType(settings.FurnitureType),
                    ResolveSingleCenterAcceptedSizes(settings.FurnitureType)));
                break;

            case SlotPreset.ShelfLeftCenterRight:
                definitions.Add(new SlotDefinition(
                    "Slot_Left",
                    new Vector3(leftX, topY, centerZ),
                    SlotType.SmallTabletop,
                    CreateSizeList(ItemSize.Small, ItemSize.Medium)));
                definitions.Add(new SlotDefinition(
                    "Slot_Center",
                    new Vector3(centerX, topY, centerZ),
                    SlotType.MediumTabletop,
                    CreateSizeList(ItemSize.Small, ItemSize.Medium)));
                definitions.Add(new SlotDefinition(
                    "Slot_Right",
                    new Vector3(rightX, topY, centerZ),
                    SlotType.SmallTabletop,
                    CreateSizeList(ItemSize.Small, ItemSize.Medium)));
                break;

            case SlotPreset.FloorLargeCenter:
                definitions.Add(new SlotDefinition(
                    "Slot_Large",
                    new Vector3(centerX, topY, centerZ),
                    SlotType.FloorLarge,
                    CreateSizeList(ItemSize.Medium, ItemSize.Large, ItemSize.Tall)));
                break;

            case SlotPreset.WallShelfLeftCenterRight:
                definitions.Add(new SlotDefinition(
                    "Slot_Left",
                    new Vector3(leftX, topY, centerZ),
                    SlotType.WallShelf,
                    CreateSizeList(ItemSize.Small, ItemSize.Medium)));
                definitions.Add(new SlotDefinition(
                    "Slot_Center",
                    new Vector3(centerX, topY, centerZ),
                    SlotType.WallShelf,
                    CreateSizeList(ItemSize.Small, ItemSize.Medium)));
                definitions.Add(new SlotDefinition(
                    "Slot_Right",
                    new Vector3(rightX, topY, centerZ),
                    SlotType.WallShelf,
                    CreateSizeList(ItemSize.Small, ItemSize.Medium)));
                break;
        }

        return definitions;
    }

    private static SlotType ResolveSingleCenterSlotType(FurnitureType resolvedFurnitureType)
    {
        if (resolvedFurnitureType == FurnitureType.FloorPad)
        {
            return SlotType.FloorLarge;
        }

        if (resolvedFurnitureType == FurnitureType.WallShelf)
        {
            return SlotType.WallShelf;
        }

        return SlotType.MediumTabletop;
    }

    private static List<ItemSize> ResolveSingleCenterAcceptedSizes(FurnitureType resolvedFurnitureType)
    {
        if (resolvedFurnitureType == FurnitureType.FloorPad)
        {
            return CreateSizeList(ItemSize.Medium, ItemSize.Large, ItemSize.Tall);
        }

        return CreateSizeList(ItemSize.Small, ItemSize.Medium);
    }

    private static List<ItemSize> CreateSizeList(params ItemSize[] sizes)
    {
        List<ItemSize> result = new List<ItemSize>(sizes.Length);
        for (int i = 0; i < sizes.Length; i++)
        {
            result.Add(sizes[i]);
        }

        return result;
    }

    private void ResolveSceneFeatureFlags(
        out bool enableDisplayFeature,
        out bool enableLightFeature,
        out bool enableFrameFeature)
    {
        switch (authoringMode)
        {
            case DisplayFurnitureAuthoringMode.Display:
                enableDisplayFeature = true;
                enableLightFeature = false;
                enableFrameFeature = false;
                break;

            case DisplayFurnitureAuthoringMode.Light:
                enableDisplayFeature = false;
                enableLightFeature = true;
                enableFrameFeature = false;
                break;

            case DisplayFurnitureAuthoringMode.Frame:
                enableDisplayFeature = false;
                enableLightFeature = false;
                enableFrameFeature = true;
                break;

            case DisplayFurnitureAuthoringMode.Custom:
            default:
                enableDisplayFeature = customEnableDisplayFeature;
                enableLightFeature = customEnableLightFeature;
                enableFrameFeature = customEnableFrameFeature;
                break;
        }
    }

    private static void ResolveProfileFeatureFlags(
        DisplayFurnitureBuildProfile profile,
        out bool enableDisplayFeature,
        out bool enableLightFeature,
        out bool enableFrameFeature)
    {
        if (profile == null)
        {
            enableDisplayFeature = true;
            enableLightFeature = false;
            enableFrameFeature = false;
            return;
        }

        switch (profile.authoringMode)
        {
            case DisplayFurnitureAuthoringMode.Display:
                enableDisplayFeature = true;
                enableLightFeature = false;
                enableFrameFeature = false;
                break;

            case DisplayFurnitureAuthoringMode.Light:
                enableDisplayFeature = false;
                enableLightFeature = true;
                enableFrameFeature = false;
                break;

            case DisplayFurnitureAuthoringMode.Frame:
                enableDisplayFeature = false;
                enableLightFeature = false;
                enableFrameFeature = true;
                break;

            case DisplayFurnitureAuthoringMode.Custom:
            default:
                enableDisplayFeature = profile.enableDisplayFeature;
                enableLightFeature = profile.enableLightFeature;
                enableFrameFeature = profile.enableFrameFeature;
                break;
        }
    }

    private FurnitureBuildSettings CreateSceneBuildSettings()
    {
        ResolveSceneFeatureFlags(out bool enableDisplayFeature, out bool enableLightFeature, out bool enableFrameFeature);
        return new FurnitureBuildSettings
        {
            FurnitureId = furnitureTarget != null ? furnitureTarget.name : string.Empty,
            AuthoringMode = authoringMode,
            EnableDisplayFeature = enableDisplayFeature,
            EnableLightFeature = enableLightFeature,
            EnableFrameFeature = enableFrameFeature,
            FurnitureType = enableDisplayFeature ? displayFurnitureType : FurnitureType.Custom,
            SlotPreset = enableDisplayFeature ? displaySlotPreset : SlotPreset.Custom,
            LightFeaturePreset = enableLightFeature ? lightFeaturePreset : LightFeaturePreset.None,
            FrameSurfacePreset = enableFrameFeature ? frameSurfacePreset : FrameSurfacePreset.None,
            SlotHeightOffset = slotHeightOffset,
            BoundsPadding = boundsPadding,
            ClearExistingSlots = clearExistingSlots,
            CreatePlacementBounds = createOrUpdatePlacementBounds,
            AutoFitBoundsFromRenderers = autoFitBoundsFromRenderers,
            CreateBlockingCollider = createBlockingColliderOption,
            GenerateSlotsFromPlacementBounds = generateSlotsFromPlacementBounds,
            PrefabProfile = null
        };
    }

    private static FurnitureBuildSettings CreateBatchBuildSettings(
        string modelId,
        DisplayFurnitureBuildProfile profile)
    {
        InferDisplayTypeAndPreset(modelId, out FurnitureType inferredType, out SlotPreset inferredPreset);

        if (profile == null)
        {
            return new FurnitureBuildSettings
            {
                FurnitureId = modelId,
                AuthoringMode = DisplayFurnitureAuthoringMode.Display,
                EnableDisplayFeature = true,
                EnableLightFeature = false,
                EnableFrameFeature = false,
                FurnitureType = inferredType,
                SlotPreset = inferredPreset,
                LightFeaturePreset = LightFeaturePreset.None,
                FrameSurfacePreset = FrameSurfacePreset.None,
                SlotHeightOffset = DefaultSlotHeightOffset,
                BoundsPadding = Vector3.zero,
                ClearExistingSlots = true,
                CreatePlacementBounds = true,
                AutoFitBoundsFromRenderers = true,
                CreateBlockingCollider = true,
                GenerateSlotsFromPlacementBounds = true
            };
        }

        ResolveProfileFeatureFlags(profile, out bool enableDisplayFeature, out bool enableLightFeature, out bool enableFrameFeature);

        return new FurnitureBuildSettings
        {
            FurnitureId = string.IsNullOrWhiteSpace(profile.furnitureId) ? modelId : profile.furnitureId,
            AuthoringMode = profile.authoringMode,
            EnableDisplayFeature = enableDisplayFeature,
            EnableLightFeature = enableLightFeature,
            EnableFrameFeature = enableFrameFeature,
            FurnitureType = enableDisplayFeature ? profile.furnitureType : FurnitureType.Custom,
            SlotPreset = enableDisplayFeature ? profile.slotPreset : SlotPreset.Custom,
            LightFeaturePreset = enableLightFeature ? profile.lightFeaturePreset : LightFeaturePreset.None,
            FrameSurfacePreset = enableFrameFeature ? profile.frameSurfacePreset : FrameSurfacePreset.None,
            SlotHeightOffset = profile.slotHeightOffset,
            BoundsPadding = ClampToNonNegative(profile.boundsPadding),
            ClearExistingSlots = true,
            CreatePlacementBounds = profile.createPlacementBounds,
            AutoFitBoundsFromRenderers = true,
            CreateBlockingCollider = profile.createBlockingCollider,
            GenerateSlotsFromPlacementBounds = profile.generateSlotsFromPlacementBounds,
            PrefabProfile = profile
        };
    }

    private static DisplayFurnitureBuildProfile EnsureBuildProfile(
        DisplayFurnitureSourceEntry source,
        out bool wasCreated)
    {
        DisplayFurnitureBuildProfile existingProfile =
            AssetDatabase.LoadAssetAtPath<DisplayFurnitureBuildProfile>(source.ProfileAssetPath);
        if (existingProfile != null)
        {
            wasCreated = false;
            return existingProfile;
        }

        EnsureAssetFolder(source.DataFolderAssetPath);

        DisplayFurnitureBuildProfile profile = CreateInstance<DisplayFurnitureBuildProfile>();
        InferDisplayTypeAndPreset(source.ModelId, out FurnitureType inferredType, out SlotPreset inferredPreset);

        profile.furnitureId = source.ModelId;
        profile.authoringMode = DisplayFurnitureAuthoringMode.Display;
        profile.enableDisplayFeature = true;
        profile.enableLightFeature = false;
        profile.enableFrameFeature = false;
        profile.furnitureType = inferredType;
        profile.slotPreset = inferredPreset;
        profile.lightFeaturePreset = LightFeaturePreset.None;
        profile.frameSurfacePreset = FrameSurfacePreset.None;
        profile.slotHeightOffset = DefaultSlotHeightOffset;
        profile.boundsPadding = Vector3.zero;
        profile.createPlacementBounds = true;
        profile.createBlockingCollider = true;
        profile.generateSlotsFromPlacementBounds = true;

        AssetDatabase.CreateAsset(profile, source.ProfileAssetPath);
        SetDirty(profile);

        wasCreated = true;
        return profile;
    }

    private static void InferDisplayTypeAndPreset(string modelId, out FurnitureType furnitureType, out SlotPreset slotPreset)
    {
        if (modelId.StartsWith("DF_WallShelf_", StringComparison.OrdinalIgnoreCase))
        {
            furnitureType = FurnitureType.WallShelf;
            slotPreset = SlotPreset.WallShelfLeftCenterRight;
            return;
        }

        if (modelId.StartsWith("DF_FloorPad_", StringComparison.OrdinalIgnoreCase))
        {
            furnitureType = FurnitureType.FloorPad;
            slotPreset = SlotPreset.FloorLargeCenter;
            return;
        }

        if (modelId.StartsWith("DF_Plith_", StringComparison.OrdinalIgnoreCase)
            || modelId.StartsWith("DF_Plinth_", StringComparison.OrdinalIgnoreCase))
        {
            furnitureType = FurnitureType.Plinth;
            slotPreset = SlotPreset.SingleCenter;
            return;
        }

        if (modelId.StartsWith("DF_Shelf_", StringComparison.OrdinalIgnoreCase))
        {
            furnitureType = FurnitureType.Shelf;
            slotPreset = SlotPreset.ShelfLeftCenterRight;
            return;
        }

        furnitureType = FurnitureType.Custom;
        slotPreset = SlotPreset.Custom;
    }

    private static List<DisplayFurnitureSourceEntry> DiscoverDisplayFurnitureSources()
    {
        List<DisplayFurnitureSourceEntry> sources = new List<DisplayFurnitureSourceEntry>();

        if (!AssetDatabase.IsValidFolder(SourceModelFolder))
        {
            return sources;
        }

        string absoluteSourceFolder = ToAbsolutePath(SourceModelFolder);
        if (!Directory.Exists(absoluteSourceFolder))
        {
            return sources;
        }

        string[] furnitureFolders = Directory.GetDirectories(absoluteSourceFolder, "DF_*", SearchOption.TopDirectoryOnly);
        Array.Sort(furnitureFolders, StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < furnitureFolders.Length; i++)
        {
            if (TryCreateSourceEntryFromFolder(furnitureFolders[i], out DisplayFurnitureSourceEntry source))
            {
                sources.Add(source);
            }
        }

        string[] rootLevelFbxFiles = Directory.GetFiles(absoluteSourceFolder, "*.fbx", SearchOption.TopDirectoryOnly);
        Array.Sort(rootLevelFbxFiles, StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < rootLevelFbxFiles.Length; i++)
        {
            if (TryCreateSourceEntryFromFbx(rootLevelFbxFiles[i], out DisplayFurnitureSourceEntry source))
            {
                sources.Add(source);
            }
        }

        return sources;
    }

    private static bool TryCreateSourceEntryFromFolder(
        string folderAbsolutePath,
        out DisplayFurnitureSourceEntry source)
    {
        source = null;

        if (string.IsNullOrWhiteSpace(folderAbsolutePath) || !Directory.Exists(folderAbsolutePath))
        {
            return false;
        }

        string folderName = Path.GetFileName(folderAbsolutePath);
        string preferredFbxName = $"{folderName}.fbx";
        string[] rootLevelFbxFiles = Directory.GetFiles(folderAbsolutePath, "*.fbx", SearchOption.TopDirectoryOnly);
        Array.Sort(rootLevelFbxFiles, StringComparer.OrdinalIgnoreCase);

        string chosenFbxPath = FindPreferredFbxPath(rootLevelFbxFiles, preferredFbxName);
        if (string.IsNullOrWhiteSpace(chosenFbxPath))
        {
            string[] descendantFbxFiles = Directory.GetFiles(folderAbsolutePath, "*.fbx", SearchOption.AllDirectories);
            Array.Sort(descendantFbxFiles, StringComparer.OrdinalIgnoreCase);
            chosenFbxPath = FindPreferredFbxPath(descendantFbxFiles, preferredFbxName);
        }

        if (string.IsNullOrWhiteSpace(chosenFbxPath))
        {
            Debug.LogWarning(
                $"[MemoryDisplayFurnitureBuilderWindow] Skipped folder {folderName} because no main FBX could be resolved.");
            return false;
        }

        string sourceFolderAssetPath = ToAssetPath(folderAbsolutePath);
        return TryCreateSourceEntry(
            chosenFbxPath,
            sourceFolderAssetPath,
            out source);
    }

    private static bool TryCreateSourceEntryFromFbx(
        string fbxAbsolutePath,
        out DisplayFurnitureSourceEntry source)
    {
        source = null;

        if (string.IsNullOrWhiteSpace(fbxAbsolutePath) || !File.Exists(fbxAbsolutePath))
        {
            return false;
        }

        return TryCreateSourceEntry(
            fbxAbsolutePath,
            SourceModelFolder,
            out source);
    }

    private static bool TryCreateSourceEntry(
        string fbxAbsolutePath,
        string sourceFolderAssetPath,
        out DisplayFurnitureSourceEntry source)
    {
        source = null;

        string sourceAssetPath = ToAssetPath(fbxAbsolutePath);
        if (!sourceAssetPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string modelId = Path.GetFileNameWithoutExtension(sourceAssetPath);
        string prefabAssetPath = $"{OutputPrefabFolder}/PF_{modelId}.prefab";
        string normalizedSourceFolderAssetPath = NormalizeAssetPath(sourceFolderAssetPath);
        string dataFolderAssetPath = $"{normalizedSourceFolderAssetPath}/{DataFolderName}";
        string profileAssetPath = $"{dataFolderAssetPath}/{modelId}_BuildProfile.asset";
        source = new DisplayFurnitureSourceEntry(
            modelId,
            sourceAssetPath,
            normalizedSourceFolderAssetPath,
            dataFolderAssetPath,
            prefabAssetPath,
            profileAssetPath);
        return true;
    }

    private static string FindPreferredFbxPath(string[] candidatePaths, string preferredFileName)
    {
        if (candidatePaths == null || candidatePaths.Length == 0)
        {
            return null;
        }

        for (int i = 0; i < candidatePaths.Length; i++)
        {
            if (string.Equals(
                Path.GetFileName(candidatePaths[i]),
                preferredFileName,
                StringComparison.OrdinalIgnoreCase))
            {
                return candidatePaths[i];
            }
        }

        if (candidatePaths.Length == 1)
        {
            return candidatePaths[0];
        }

        return null;
    }

    private static List<DisplayFurnitureSourceEntry> DiscoverSelectedDisplayFurnitureSources()
    {
        List<DisplayFurnitureSourceEntry> allSources = DiscoverDisplayFurnitureSources();
        if (allSources.Count == 0)
        {
            return allSources;
        }

        Dictionary<string, DisplayFurnitureSourceEntry> sourcesByPath =
            new Dictionary<string, DisplayFurnitureSourceEntry>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < allSources.Count; i++)
        {
            sourcesByPath[allSources[i].SourceAssetPath] = allSources[i];
        }

        List<DisplayFurnitureSourceEntry> selectedSources = new List<DisplayFurnitureSourceEntry>();
        HashSet<string> seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < Selection.objects.Length; i++)
        {
            string assetPath = NormalizeAssetPath(AssetDatabase.GetAssetPath(Selection.objects[i]));
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                continue;
            }

            if (AssetDatabase.IsValidFolder(assetPath))
            {
                for (int sourceIndex = 0; sourceIndex < allSources.Count; sourceIndex++)
                {
                    DisplayFurnitureSourceEntry source = allSources[sourceIndex];
                    bool matchesSelectedFolder =
                        IsPathUnderFolder(source.SourceFolderAssetPath, assetPath)
                        || IsPathUnderFolder(assetPath, source.SourceFolderAssetPath)
                        || IsPathUnderFolder(source.SourceAssetPath, assetPath);
                    if (matchesSelectedFolder && seenPaths.Add(source.SourceAssetPath))
                    {
                        selectedSources.Add(source);
                    }
                }

                continue;
            }

            if (assetPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)
                && sourcesByPath.TryGetValue(assetPath, out DisplayFurnitureSourceEntry selectedSource)
                && seenPaths.Add(selectedSource.SourceAssetPath))
            {
                selectedSources.Add(selectedSource);
            }
        }

        return selectedSources;
    }

    private static List<DisplayFurnitureSourceEntry> FilterBatchSources(
        List<DisplayFurnitureSourceEntry> sources,
        BatchBuildFilter filter)
    {
        if (filter == BatchBuildFilter.All)
        {
            return new List<DisplayFurnitureSourceEntry>(sources);
        }

        List<DisplayFurnitureSourceEntry> filteredSources = new List<DisplayFurnitureSourceEntry>();

        for (int i = 0; i < sources.Count; i++)
        {
            DisplayFurnitureSourceEntry source = sources[i];
            bool prefabExists = File.Exists(ToAbsolutePath(source.PrefabAssetPath));

            if (filter == BatchBuildFilter.MissingOnly && !prefabExists)
            {
                filteredSources.Add(source);
            }
            else if (filter == BatchBuildFilter.ExistingOnly && prefabExists)
            {
                filteredSources.Add(source);
            }
        }

        return filteredSources;
    }

    private static bool HasSelectedBatchSource()
    {
        if (Selection.objects == null || Selection.objects.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < Selection.objects.Length; i++)
        {
            string assetPath = NormalizeAssetPath(AssetDatabase.GetAssetPath(Selection.objects[i]));
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                continue;
            }

            if (AssetDatabase.IsValidFolder(assetPath) && IsPathUnderFolder(assetPath, SourceModelFolder))
            {
                return true;
            }

            if (assetPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)
                && IsPathUnderFolder(assetPath, SourceModelFolder))
            {
                return true;
            }
        }

        return false;
    }

    private void SaveFurnitureAsPrefab(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        string normalizedFolder = string.IsNullOrWhiteSpace(prefabFolder)
            ? OutputPrefabFolder
            : NormalizeAssetPath(prefabFolder).TrimEnd('/');

        EnsureAssetFolder(normalizedFolder);
        string prefabAssetPath = $"{normalizedFolder}/{target.name}.prefab";

        if (EditorUtility.IsPersistent(target))
        {
            PrefabUtility.SaveAsPrefabAsset(target, prefabAssetPath);
            return;
        }

        PrefabUtility.SaveAsPrefabAssetAndConnect(target, prefabAssetPath, InteractionMode.UserAction);
    }

    private void CaptureSceneAdjustmentsAndOverwriteFurniturePrefab()
    {
        GameObject authoringTarget = ResolveFurnitureAuthoringTarget(furnitureTarget);
        if (authoringTarget == null)
        {
            ShowSummary("Display Furniture Override", "Select a Display Furniture prefab instance in the scene first.");
            return;
        }

        if (!TryResolveSourceEntryFromFurnitureTarget(authoringTarget, out DisplayFurnitureSourceEntry source))
        {
            ShowSummary(
                "Display Furniture Override",
                $"Could not resolve a Display Furniture source/profile for {authoringTarget.name}.");
            return;
        }

        if (!ConfirmSceneOverwrite("Display Furniture Prefab", source.PrefabAssetPath))
        {
            return;
        }

        DisplayFurnitureBuildProfile profile = EnsureBuildProfile(source, out _);
        CaptureFurnitureOverrides(authoringTarget, profile);
        SaveSceneTargetToPrefabAsset(authoringTarget, source.PrefabAssetPath);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        ShowSummary(
            "Display Furniture Override",
            $"Captured scene adjustments and overwrote prefab:\n{source.PrefabAssetPath}");
    }

    private static int EnsureRequiredFolders()
    {
        int createdFolders = 0;
        createdFolders += EnsureAssetFolder(SourceModelFolder);
        createdFolders += EnsureAssetFolder(OutputPrefabFolder);
        return createdFolders;
    }

    private static int EnsureAssetFolder(string assetFolderPath)
    {
        string normalizedPath = NormalizeAssetPath(assetFolderPath).TrimEnd('/');
        if (AssetDatabase.IsValidFolder(normalizedPath))
        {
            return 0;
        }

        if (!normalizedPath.StartsWith("Assets", StringComparison.Ordinal))
        {
            throw new IOException($"Folder must be inside Assets: {assetFolderPath}");
        }

        string[] parts = normalizedPath.Split('/');
        string currentPath = parts[0];
        int createdFolders = 0;

        for (int i = 1; i < parts.Length; i++)
        {
            string nextPath = $"{currentPath}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(nextPath))
            {
                AssetDatabase.CreateFolder(currentPath, parts[i]);
                createdFolders++;
            }

            currentPath = nextPath;
        }

        return createdFolders;
    }

    private static T GetOrAddComponent<T>(GameObject target, bool useUndo) where T : Component
    {
        T component = target.GetComponent<T>();
        if (component != null)
        {
            return component;
        }

        if (useUndo)
        {
            return Undo.AddComponent<T>(target);
        }

        return target.AddComponent<T>();
    }

    private static GameObject ResolveMemoryItemTarget(GameObject selectedObject)
    {
        if (selectedObject == null)
        {
            return null;
        }

        MemoryObject memoryObject = selectedObject.GetComponentInParent<MemoryObject>();
        if (memoryObject != null)
        {
            return memoryObject.gameObject;
        }

        return selectedObject;
    }

    private static void EnsurePlacementDefaults(MemoryObject memoryObject, bool applyFullDefaults)
    {
        if (memoryObject == null)
        {
            return;
        }

        SerializedObject serializedObject = new SerializedObject(memoryObject);
        serializedObject.UpdateIfRequiredOrScript();

        SerializedProperty enablePlacementProperty = serializedObject.FindProperty("enablePlacement");
        if (enablePlacementProperty != null && applyFullDefaults)
        {
            enablePlacementProperty.boolValue = true;
        }

        SerializedProperty itemSizeModeProperty = serializedObject.FindProperty("itemSizeMode");
        if (itemSizeModeProperty != null && applyFullDefaults)
        {
            itemSizeModeProperty.enumValueIndex = (int)ItemSizeMode.AutoFromBounds;
        }

        SerializedProperty manualItemSizeProperty = serializedObject.FindProperty("manualItemSize");
        if (manualItemSizeProperty != null && applyFullDefaults)
        {
            manualItemSizeProperty.enumValueIndex = (int)ItemSize.Medium;
        }

        SerializedProperty allowedSlotTypesProperty = serializedObject.FindProperty("allowedSlotTypes");
        if (allowedSlotTypesProperty != null && allowedSlotTypesProperty.isArray && allowedSlotTypesProperty.arraySize == 0)
        {
            allowedSlotTypesProperty.arraySize = 4;
            allowedSlotTypesProperty.GetArrayElementAtIndex(0).enumValueIndex = (int)SlotType.SmallTabletop;
            allowedSlotTypesProperty.GetArrayElementAtIndex(1).enumValueIndex = (int)SlotType.MediumTabletop;
            allowedSlotTypesProperty.GetArrayElementAtIndex(2).enumValueIndex = (int)SlotType.WallShelf;
            allowedSlotTypesProperty.GetArrayElementAtIndex(3).enumValueIndex = (int)SlotType.FloorLarge;
        }

        SerializedProperty preferredHeightOffsetProperty = serializedObject.FindProperty("preferredHeightOffset");
        if (preferredHeightOffsetProperty != null && applyFullDefaults)
        {
            preferredHeightOffsetProperty.floatValue = 0f;
        }

        SerializedProperty alignToSlotRotationProperty = serializedObject.FindProperty("alignToSlotRotation");
        if (alignToSlotRotationProperty != null && applyFullDefaults)
        {
            alignToSlotRotationProperty.boolValue = true;
        }

        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static bool TryGetCombinedLocalRendererBounds(Transform root, out Bounds localBounds)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        Vector3 min = Vector3.zero;
        Vector3 max = Vector3.zero;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            Bounds rendererBounds = renderer.bounds;
            Vector3 center = rendererBounds.center;
            Vector3 extents = rendererBounds.extents;

            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 worldCorner = center + new Vector3(
                            extents.x * x,
                            extents.y * y,
                            extents.z * z);
                        Vector3 localCorner = root.InverseTransformPoint(worldCorner);

                        if (!hasBounds)
                        {
                            min = localCorner;
                            max = localCorner;
                            hasBounds = true;
                            continue;
                        }

                        min = Vector3.Min(min, localCorner);
                        max = Vector3.Max(max, localCorner);
                    }
                }
            }
        }

        if (!hasBounds)
        {
            localBounds = default;
            return false;
        }

        localBounds = new Bounds((min + max) * 0.5f, max - min);
        return true;
    }

    private static bool TryGetColliderBoundsInRootLocal(BoxCollider collider, Transform root, out Bounds localBounds)
    {
        if (collider == null)
        {
            localBounds = default;
            return false;
        }

        Vector3 center = collider.center;
        Vector3 extents = collider.size * 0.5f;
        Vector3 min = Vector3.zero;
        Vector3 max = Vector3.zero;
        bool hasBounds = false;

        for (int x = -1; x <= 1; x += 2)
        {
            for (int y = -1; y <= 1; y += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 localCorner = center + new Vector3(
                        extents.x * x,
                        extents.y * y,
                        extents.z * z);
                    Vector3 worldCorner = collider.transform.TransformPoint(localCorner);
                    Vector3 rootLocalCorner = root.InverseTransformPoint(worldCorner);

                    if (!hasBounds)
                    {
                        min = rootLocalCorner;
                        max = rootLocalCorner;
                        hasBounds = true;
                        continue;
                    }

                    min = Vector3.Min(min, rootLocalCorner);
                    max = Vector3.Max(max, rootLocalCorner);
                }
            }
        }

        if (!hasBounds)
        {
            localBounds = default;
            return false;
        }

        localBounds = new Bounds((min + max) * 0.5f, max - min);
        return true;
    }

    private static Vector3 ClampToNonNegative(Vector3 value)
    {
        return new Vector3(
            Mathf.Max(0f, value.x),
            Mathf.Max(0f, value.y),
            Mathf.Max(0f, value.z));
    }

    private static string NormalizeAssetPath(string path)
    {
        return string.IsNullOrWhiteSpace(path) ? string.Empty : path.Replace("\\", "/");
    }

    private static bool IsPathUnderFolder(string assetPath, string folderPath)
    {
        string normalizedAssetPath = NormalizeAssetPath(assetPath).TrimEnd('/');
        string normalizedFolderPath = NormalizeAssetPath(folderPath).TrimEnd('/');

        if (string.Equals(normalizedAssetPath, normalizedFolderPath, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return normalizedAssetPath.StartsWith(
            normalizedFolderPath + "/",
            StringComparison.OrdinalIgnoreCase);
    }

    private static string ToAbsolutePath(string assetPath)
    {
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
    }

    private static string ToAssetPath(string absolutePath)
    {
        string normalizedAbsolute = NormalizeAssetPath(absolutePath);
        string normalizedDataPath = NormalizeAssetPath(Application.dataPath);

        if (!normalizedAbsolute.StartsWith(normalizedDataPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Path is outside the Unity Assets folder: {absolutePath}");
        }

        return $"Assets{normalizedAbsolute.Substring(normalizedDataPath.Length)}";
    }

    private static void SetDirty(UnityEngine.Object target)
    {
        if (target != null)
        {
            EditorUtility.SetDirty(target);
        }
    }

    private static GameObject ResolveFurnitureAuthoringTarget(GameObject target)
    {
        if (target == null)
        {
            return null;
        }

        MemoryDisplayFurniture furniture = target.GetComponentInParent<MemoryDisplayFurniture>();
        if (furniture != null)
        {
            return furniture.gameObject;
        }

        GameObject prefabRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(target);
        return prefabRoot != null ? prefabRoot : target;
    }

    private static bool TryResolveSourceEntryFromFurnitureTarget(
        GameObject target,
        out DisplayFurnitureSourceEntry resolvedSource)
    {
        resolvedSource = null;
        if (target == null)
        {
            return false;
        }

        GameObject prefabAsset = EditorUtility.IsPersistent(target)
            ? target
            : PrefabUtility.GetCorrespondingObjectFromSource(target);
        string prefabAssetPath = NormalizeAssetPath(AssetDatabase.GetAssetPath(prefabAsset));

        List<DisplayFurnitureSourceEntry> sources = DiscoverDisplayFurnitureSources();
        for (int i = 0; i < sources.Count; i++)
        {
            DisplayFurnitureSourceEntry source = sources[i];
            if (!string.IsNullOrWhiteSpace(prefabAssetPath)
                && string.Equals(source.PrefabAssetPath, prefabAssetPath, StringComparison.OrdinalIgnoreCase))
            {
                resolvedSource = source;
                return true;
            }

            if (string.Equals(source.PrefabRootName, target.name, StringComparison.OrdinalIgnoreCase))
            {
                resolvedSource = source;
                return true;
            }
        }

        return false;
    }

    private static void CaptureFurnitureOverrides(GameObject target, DisplayFurnitureBuildProfile profile)
    {
        if (target == null || profile == null)
        {
            return;
        }

        ApplyFeatureSettingsFromSceneTarget(target, profile);

        CaptureRootRotationScaleOverride(target.transform, profile.prefabRootTransform);

        Transform modelContainer = target.transform.Find(ModelContainerObjectName);
        CaptureTransformOverride(modelContainer, profile.modelContainerTransform, includePosition: true);

        Transform modelAssetTransform = GetPrimaryModelAssetTransform(target.transform);
        CaptureTransformOverride(modelAssetTransform, profile.modelAssetTransform, includePosition: true);

        BoxCollider placementBoundsCollider = FindPlacementBoundsCollider(target.transform);
        CapturePlacementBoundsOverride(placementBoundsCollider, profile.placementBoundsOverride);
        CaptureSlotOverrides(target.transform, profile.slotOverrides);
        CaptureLightOverrides(target.transform, profile.lightOverrides);
        CaptureFrameSurfaceOverrides(target.transform, profile.frameSurfaceOverrides);
        SetDirty(profile);
    }

    private static void ApplyFeatureSettingsFromSceneTarget(GameObject target, DisplayFurnitureBuildProfile profile)
    {
        MemoryDisplayFurniture furniture = target != null ? target.GetComponent<MemoryDisplayFurniture>() : null;
        int slotCount = target != null ? target.GetComponentsInChildren<MemoryDisplaySlot>(true).Length : 0;
        int lightCount = target != null ? target.GetComponentsInChildren<MemoryDisplayLight>(true).Length : 0;
        int frameCount = target != null ? target.GetComponentsInChildren<MemoryDisplayFrameSurface>(true).Length : 0;

        profile.enableDisplayFeature = slotCount > 0;
        profile.enableLightFeature = lightCount > 0;
        profile.enableFrameFeature = frameCount > 0;

        int enabledFeatureCount = (profile.enableDisplayFeature ? 1 : 0)
            + (profile.enableLightFeature ? 1 : 0)
            + (profile.enableFrameFeature ? 1 : 0);

        if (enabledFeatureCount == 1)
        {
            profile.authoringMode = profile.enableDisplayFeature
                ? DisplayFurnitureAuthoringMode.Display
                : profile.enableLightFeature
                    ? DisplayFurnitureAuthoringMode.Light
                    : DisplayFurnitureAuthoringMode.Frame;
        }
        else
        {
            profile.authoringMode = DisplayFurnitureAuthoringMode.Custom;
        }

        if (furniture != null)
        {
            profile.furnitureType = furniture.Type;
        }

        if (!profile.enableDisplayFeature)
        {
            profile.slotPreset = SlotPreset.Custom;
        }

        if (profile.enableLightFeature && profile.lightFeaturePreset == LightFeaturePreset.None)
        {
            profile.lightFeaturePreset = LightFeaturePreset.Custom;
        }
        else if (!profile.enableLightFeature)
        {
            profile.lightFeaturePreset = LightFeaturePreset.None;
        }

        if (profile.enableFrameFeature && profile.frameSurfacePreset == FrameSurfacePreset.None)
        {
            profile.frameSurfacePreset = FrameSurfacePreset.Custom;
        }
        else if (!profile.enableFrameFeature)
        {
            profile.frameSurfacePreset = FrameSurfacePreset.None;
        }
    }

    private static void CaptureRootRotationScaleOverride(Transform source, PrefabTransformOverrideData target)
    {
        if (target == null)
        {
            return;
        }

        if (source == null)
        {
            target.enabled = false;
            target.localPosition = Vector3.zero;
            target.localEulerAngles = Vector3.zero;
            target.localScale = Vector3.one;
            return;
        }

        target.enabled = true;
        target.localPosition = Vector3.zero;
        target.localEulerAngles = source.localEulerAngles;
        target.localScale = EnsureNonZeroScale(source.localScale);
    }

    private static void CaptureTransformOverride(Transform source, PrefabTransformOverrideData target, bool includePosition)
    {
        if (target == null)
        {
            return;
        }

        if (source == null)
        {
            target.enabled = false;
            target.localPosition = Vector3.zero;
            target.localEulerAngles = Vector3.zero;
            target.localScale = Vector3.one;
            return;
        }

        target.enabled = true;
        target.localPosition = includePosition ? source.localPosition : Vector3.zero;
        target.localEulerAngles = source.localEulerAngles;
        target.localScale = EnsureNonZeroScale(source.localScale);
    }

    private static void CapturePlacementBoundsOverride(BoxCollider source, BoxColliderOverrideData target)
    {
        if (target == null)
        {
            return;
        }

        if (source == null)
        {
            target.enabled = false;
            target.localPosition = Vector3.zero;
            target.localEulerAngles = Vector3.zero;
            target.localScale = Vector3.one;
            target.center = Vector3.zero;
            target.size = Vector3.one;
            return;
        }

        target.enabled = true;
        target.localPosition = source.transform.localPosition;
        target.localEulerAngles = source.transform.localEulerAngles;
        target.localScale = EnsureNonZeroScale(source.transform.localScale);
        target.center = source.center;
        target.size = EnsureMinSize(source.size);
    }

    private static void CaptureSlotOverrides(Transform furnitureRoot, List<NamedTransformOverrideData> slotOverrides)
    {
        if (slotOverrides == null)
        {
            return;
        }

        slotOverrides.Clear();
        if (furnitureRoot == null)
        {
            return;
        }

        Transform slotsRoot = furnitureRoot.Find(SlotsRootObjectName);
        if (slotsRoot == null)
        {
            return;
        }

        MemoryDisplaySlot[] slots = slotsRoot.GetComponentsInChildren<MemoryDisplaySlot>(true);
        for (int i = 0; i < slots.Length; i++)
        {
            MemoryDisplaySlot slot = slots[i];
            if (slot == null)
            {
                continue;
            }

            slotOverrides.Add(new NamedTransformOverrideData
            {
                id = string.IsNullOrWhiteSpace(slot.SlotId) ? slot.name : slot.SlotId,
                localPosition = slot.transform.localPosition,
                localEulerAngles = slot.transform.localEulerAngles,
                localScale = EnsureNonZeroScale(slot.transform.localScale)
            });
        }
    }

    private static void CaptureLightOverrides(Transform furnitureRoot, List<NamedLightOverrideData> lightOverrides)
    {
        if (lightOverrides == null)
        {
            return;
        }

        lightOverrides.Clear();
        if (furnitureRoot == null)
        {
            return;
        }

        MemoryDisplayLight[] displayLights = furnitureRoot.GetComponentsInChildren<MemoryDisplayLight>(true);
        for (int i = 0; i < displayLights.Length; i++)
        {
            MemoryDisplayLight displayLight = displayLights[i];
            if (displayLight == null)
            {
                continue;
            }

            Light targetLight = displayLight.TargetLight;
            if (targetLight == null)
            {
                targetLight = displayLight.GetComponent<Light>();
            }

            lightOverrides.Add(new NamedLightOverrideData
            {
                id = displayLight.LightId,
                lightRole = displayLight.LightRole,
                allowRuntimeAdjustment = displayLight.AllowRuntimeAdjustment,
                gameObjectActive = displayLight.gameObject.activeSelf,
                lightEnabled = targetLight == null || targetLight.enabled,
                localPosition = displayLight.transform.localPosition,
                localEulerAngles = displayLight.transform.localEulerAngles,
                localScale = EnsureNonZeroScale(displayLight.transform.localScale),
                lightType = targetLight != null ? targetLight.type : LightType.Point,
                color = targetLight != null ? targetLight.color : Color.white,
                intensity = targetLight != null ? targetLight.intensity : 1f,
                range = targetLight != null ? targetLight.range : 10f,
                spotAngle = targetLight != null ? targetLight.spotAngle : 30f,
                innerSpotAngle = targetLight != null ? targetLight.innerSpotAngle : 21.8f,
                shadows = targetLight != null ? targetLight.shadows : LightShadows.None
            });
        }
    }

    private static void CaptureFrameSurfaceOverrides(Transform furnitureRoot, List<NamedFrameSurfaceOverrideData> frameSurfaceOverrides)
    {
        if (frameSurfaceOverrides == null)
        {
            return;
        }

        frameSurfaceOverrides.Clear();
        if (furnitureRoot == null)
        {
            return;
        }

        MemoryDisplayFrameSurface[] frameSurfaces = furnitureRoot.GetComponentsInChildren<MemoryDisplayFrameSurface>(true);
        for (int i = 0; i < frameSurfaces.Length; i++)
        {
            MemoryDisplayFrameSurface frameSurface = frameSurfaces[i];
            if (frameSurface == null)
            {
                continue;
            }

            Renderer targetRenderer = frameSurface.TargetRenderer;
            if (targetRenderer == null)
            {
                targetRenderer = frameSurface.GetComponent<Renderer>();
            }

            frameSurfaceOverrides.Add(new NamedFrameSurfaceOverrideData
            {
                id = frameSurface.SurfaceId,
                contentType = frameSurface.ContentType,
                createQuadIfMissing = targetRenderer != null,
                gameObjectActive = frameSurface.gameObject.activeSelf,
                rendererEnabled = targetRenderer == null || targetRenderer.enabled,
                localPosition = frameSurface.transform.localPosition,
                localEulerAngles = frameSurface.transform.localEulerAngles,
                localScale = EnsureNonZeroScale(frameSurface.transform.localScale),
                emissiveDisplay = frameSurface.EmissiveDisplay,
                emissionColor = frameSurface.EmissionColor,
                texturePropertyName = frameSurface.TexturePropertyName,
                emissionColorPropertyName = frameSurface.EmissionColorPropertyName,
                emissionTexturePropertyName = frameSurface.EmissionTexturePropertyName
            });
        }
    }

    private static void ApplyPrefabRootOverride(Transform target, PrefabTransformOverrideData data)
    {
        if (target == null || data == null || !data.enabled)
        {
            return;
        }

        target.localPosition = Vector3.zero;
        target.localRotation = Quaternion.Euler(data.localEulerAngles);
        target.localScale = EnsureNonZeroScale(data.localScale);
    }

    private static void ApplyTransformOverride(Transform target, PrefabTransformOverrideData data)
    {
        if (target == null || data == null || !data.enabled)
        {
            return;
        }

        target.localPosition = data.localPosition;
        target.localRotation = Quaternion.Euler(data.localEulerAngles);
        target.localScale = EnsureNonZeroScale(data.localScale);
    }

    private static void ApplyPlacementBoundsOverride(Transform furnitureRoot, DisplayFurnitureBuildProfile profile)
    {
        if (furnitureRoot == null || profile?.placementBoundsOverride == null || !profile.placementBoundsOverride.enabled)
        {
            return;
        }

        Transform placementBoundsTransform = furnitureRoot.Find(PlacementBoundsObjectName);
        BoxCollider placementBoundsCollider = placementBoundsTransform != null
            ? placementBoundsTransform.GetComponent<BoxCollider>()
            : null;
        if (placementBoundsTransform == null || placementBoundsCollider == null)
        {
            return;
        }

        BoxColliderOverrideData data = profile.placementBoundsOverride;
        placementBoundsTransform.localPosition = data.localPosition;
        placementBoundsTransform.localRotation = Quaternion.Euler(data.localEulerAngles);
        placementBoundsTransform.localScale = EnsureNonZeroScale(data.localScale);
        placementBoundsCollider.center = data.center;
        placementBoundsCollider.size = EnsureMinSize(data.size);

        Transform blockingColliderTransform = furnitureRoot.Find(BlockingColliderObjectName);
        BoxCollider blockingCollider = blockingColliderTransform != null
            ? blockingColliderTransform.GetComponent<BoxCollider>()
            : null;
        if (blockingColliderTransform != null && blockingCollider != null)
        {
            blockingColliderTransform.localPosition = placementBoundsTransform.localPosition;
            blockingColliderTransform.localRotation = placementBoundsTransform.localRotation;
            blockingColliderTransform.localScale = placementBoundsTransform.localScale;
            blockingCollider.center = placementBoundsCollider.center;
            blockingCollider.size = placementBoundsCollider.size;
        }
    }

    private static void ApplySlotOverrides(Transform furnitureRoot, DisplayFurnitureBuildProfile profile)
    {
        if (furnitureRoot == null || profile?.slotOverrides == null || profile.slotOverrides.Count == 0)
        {
            return;
        }

        Transform slotsRoot = furnitureRoot.Find(SlotsRootObjectName);
        if (slotsRoot == null)
        {
            return;
        }

        Dictionary<string, NamedTransformOverrideData> overridesById =
            new Dictionary<string, NamedTransformOverrideData>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < profile.slotOverrides.Count; i++)
        {
            NamedTransformOverrideData slotOverride = profile.slotOverrides[i];
            if (slotOverride == null || string.IsNullOrWhiteSpace(slotOverride.id))
            {
                continue;
            }

            overridesById[slotOverride.id] = slotOverride;
        }

        MemoryDisplaySlot[] slots = slotsRoot.GetComponentsInChildren<MemoryDisplaySlot>(true);
        for (int i = 0; i < slots.Length; i++)
        {
            MemoryDisplaySlot slot = slots[i];
            if (slot == null)
            {
                continue;
            }

            string slotId = string.IsNullOrWhiteSpace(slot.SlotId) ? slot.name : slot.SlotId;
            if (!overridesById.TryGetValue(slotId, out NamedTransformOverrideData slotOverride))
            {
                continue;
            }

            slot.transform.localPosition = slotOverride.localPosition;
            slot.transform.localRotation = Quaternion.Euler(slotOverride.localEulerAngles);
            slot.transform.localScale = EnsureNonZeroScale(slotOverride.localScale);
        }
    }

    private static void ApplyLightOverrides(Transform furnitureRoot, DisplayFurnitureBuildProfile profile)
    {
        if (furnitureRoot == null || profile?.lightOverrides == null || profile.lightOverrides.Count == 0)
        {
            return;
        }

        Dictionary<string, MemoryDisplayLight> lightsById =
            new Dictionary<string, MemoryDisplayLight>(StringComparer.OrdinalIgnoreCase);
        MemoryDisplayLight[] displayLights = furnitureRoot.GetComponentsInChildren<MemoryDisplayLight>(true);
        for (int i = 0; i < displayLights.Length; i++)
        {
            MemoryDisplayLight displayLight = displayLights[i];
            if (displayLight == null || string.IsNullOrWhiteSpace(displayLight.LightId))
            {
                continue;
            }

            lightsById[displayLight.LightId] = displayLight;
        }

        for (int i = 0; i < profile.lightOverrides.Count; i++)
        {
            NamedLightOverrideData lightOverride = profile.lightOverrides[i];
            if (lightOverride == null || string.IsNullOrWhiteSpace(lightOverride.id))
            {
                continue;
            }

            if (!lightsById.TryGetValue(lightOverride.id, out MemoryDisplayLight displayLight) || displayLight == null)
            {
                Transform lightsRoot = FindOrCreateChild(furnitureRoot, LightsRootObjectName, useUndo: false);
                displayLight = CreateDisplayLight(lightsRoot, lightOverride.id, lightOverride.lightType);
                lightsById[lightOverride.id] = displayLight;
            }

            displayLight.transform.localPosition = lightOverride.localPosition;
            displayLight.transform.localRotation = Quaternion.Euler(lightOverride.localEulerAngles);
            displayLight.transform.localScale = EnsureNonZeroScale(lightOverride.localScale);
            displayLight.gameObject.SetActive(lightOverride.gameObjectActive);

            Light targetLight = displayLight.TargetLight;
            if (targetLight == null)
            {
                targetLight = displayLight.GetComponent<Light>();
            }

            if (targetLight == null)
            {
                targetLight = displayLight.gameObject.AddComponent<Light>();
            }

            targetLight.enabled = lightOverride.lightEnabled;
            targetLight.type = lightOverride.lightType;
            targetLight.color = lightOverride.color;
            targetLight.intensity = Mathf.Max(0f, lightOverride.intensity);
            targetLight.range = Mathf.Max(0.01f, lightOverride.range);
            targetLight.spotAngle = Mathf.Clamp(lightOverride.spotAngle, 1f, 179f);
            targetLight.innerSpotAngle = Mathf.Clamp(lightOverride.innerSpotAngle, 0f, targetLight.spotAngle);
            targetLight.shadows = lightOverride.shadows;

            displayLight.ApplyAuthoringData(
                lightOverride.id,
                lightOverride.lightRole,
                targetLight,
                lightOverride.allowRuntimeAdjustment);
        }
    }

    private static void ApplyFrameSurfaceOverrides(Transform furnitureRoot, DisplayFurnitureBuildProfile profile)
    {
        if (furnitureRoot == null || profile?.frameSurfaceOverrides == null || profile.frameSurfaceOverrides.Count == 0)
        {
            return;
        }

        Dictionary<string, MemoryDisplayFrameSurface> surfacesById =
            new Dictionary<string, MemoryDisplayFrameSurface>(StringComparer.OrdinalIgnoreCase);
        MemoryDisplayFrameSurface[] frameSurfaces = furnitureRoot.GetComponentsInChildren<MemoryDisplayFrameSurface>(true);
        for (int i = 0; i < frameSurfaces.Length; i++)
        {
            MemoryDisplayFrameSurface frameSurface = frameSurfaces[i];
            if (frameSurface == null || string.IsNullOrWhiteSpace(frameSurface.SurfaceId))
            {
                continue;
            }

            surfacesById[frameSurface.SurfaceId] = frameSurface;
        }

        for (int i = 0; i < profile.frameSurfaceOverrides.Count; i++)
        {
            NamedFrameSurfaceOverrideData frameOverride = profile.frameSurfaceOverrides[i];
            if (frameOverride == null || string.IsNullOrWhiteSpace(frameOverride.id))
            {
                continue;
            }

            if (!surfacesById.TryGetValue(frameOverride.id, out MemoryDisplayFrameSurface frameSurface) || frameSurface == null)
            {
                Transform frameRoot = FindOrCreateChild(furnitureRoot, FrameSurfacesRootObjectName, useUndo: false);
                frameSurface = CreateFrameSurface(frameRoot, frameOverride.id, frameOverride.createQuadIfMissing);
                surfacesById[frameOverride.id] = frameSurface;
            }

            frameSurface.transform.localPosition = frameOverride.localPosition;
            frameSurface.transform.localRotation = Quaternion.Euler(frameOverride.localEulerAngles);
            frameSurface.transform.localScale = EnsureNonZeroScale(frameOverride.localScale);
            frameSurface.gameObject.SetActive(frameOverride.gameObjectActive);

            Renderer targetRenderer = frameSurface.TargetRenderer;
            if (targetRenderer == null)
            {
                targetRenderer = frameSurface.GetComponent<Renderer>();
            }

            if (targetRenderer != null)
            {
                targetRenderer.enabled = frameOverride.rendererEnabled;
            }

            frameSurface.ApplyAuthoringData(
                frameOverride.id,
                frameOverride.contentType,
                targetRenderer,
                frameOverride.emissiveDisplay,
                frameOverride.emissionColor,
                frameOverride.texturePropertyName,
                frameOverride.emissionColorPropertyName,
                frameOverride.emissionTexturePropertyName);
        }
    }

    private static MemoryDisplayLight CreateDisplayLight(Transform parentRoot, string lightId, LightType lightType)
    {
        string childName = $"Light_{SanitizeFeatureChildName(lightId)}";
        GameObject lightObject = new GameObject(childName);
        lightObject.transform.SetParent(parentRoot, false);
        lightObject.transform.localPosition = Vector3.zero;
        lightObject.transform.localRotation = Quaternion.identity;
        lightObject.transform.localScale = Vector3.one;

        Light lightComponent = lightObject.AddComponent<Light>();
        lightComponent.type = lightType;
        MemoryDisplayLight displayLight = lightObject.AddComponent<MemoryDisplayLight>();
        displayLight.ApplyAuthoringData(lightId, FurnitureLightRole.Decorative, lightComponent, allowAdjustment: true);
        return displayLight;
    }

    private static MemoryDisplayFrameSurface CreateFrameSurface(Transform parentRoot, string surfaceId, bool createQuadIfMissing)
    {
        string childName = $"FrameSurface_{SanitizeFeatureChildName(surfaceId)}";
        GameObject surfaceObject;
        if (createQuadIfMissing)
        {
            surfaceObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
            surfaceObject.name = childName;
            Collider quadCollider = surfaceObject.GetComponent<Collider>();
            if (quadCollider != null)
            {
                UnityEngine.Object.DestroyImmediate(quadCollider);
            }
        }
        else
        {
            surfaceObject = new GameObject(childName);
        }

        surfaceObject.transform.SetParent(parentRoot, false);
        surfaceObject.transform.localPosition = Vector3.zero;
        surfaceObject.transform.localRotation = Quaternion.identity;
        surfaceObject.transform.localScale = Vector3.one;

        MemoryDisplayFrameSurface frameSurface = surfaceObject.GetComponent<MemoryDisplayFrameSurface>();
        if (frameSurface == null)
        {
            frameSurface = surfaceObject.AddComponent<MemoryDisplayFrameSurface>();
        }

        Renderer targetRenderer = surfaceObject.GetComponent<Renderer>();
        frameSurface.ApplyAuthoringData(
            surfaceId,
            FrameSurfaceContentType.MemoryItemImage,
            targetRenderer,
            isEmissive: false,
            targetEmissionColor: Color.white,
            targetTexturePropertyName: "_BaseMap",
            targetEmissionColorPropertyName: "_EmissionColor",
            targetEmissionTexturePropertyName: "_EmissionMap");
        return frameSurface;
    }

    private static string SanitizeFeatureChildName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Feature";
        }

        return value
            .Replace('/', '_')
            .Replace('\\', '_')
            .Replace(':', '_')
            .Replace('*', '_')
            .Replace('?', '_')
            .Replace('"', '_')
            .Replace('<', '_')
            .Replace('>', '_')
            .Replace('|', '_');
    }

    private static BoxCollider FindPlacementBoundsCollider(Transform furnitureRoot)
    {
        Transform placementBoundsTransform = furnitureRoot != null ? furnitureRoot.Find(PlacementBoundsObjectName) : null;
        return placementBoundsTransform != null ? placementBoundsTransform.GetComponent<BoxCollider>() : null;
    }

    private static Transform GetPrimaryModelAssetTransform(Transform furnitureRoot)
    {
        Transform modelContainer = furnitureRoot != null ? furnitureRoot.Find(ModelContainerObjectName) : null;
        if (modelContainer == null)
        {
            return null;
        }

        return modelContainer.childCount > 0 ? modelContainer.GetChild(0) : null;
    }

    private static void SaveSceneTargetToPrefabAsset(GameObject target, string prefabAssetPath)
    {
        if (target == null || string.IsNullOrWhiteSpace(prefabAssetPath))
        {
            return;
        }

        string prefabFolderPath = Path.GetDirectoryName(NormalizeAssetPath(prefabAssetPath));
        if (!string.IsNullOrWhiteSpace(prefabFolderPath))
        {
            EnsureAssetFolder(prefabFolderPath);
        }

        GameObject clone = UnityEngine.Object.Instantiate(target);
        clone.name = Path.GetFileNameWithoutExtension(prefabAssetPath);
        clone.transform.SetParent(null, true);
        clone.transform.position = Vector3.zero;

        try
        {
            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(clone, prefabAssetPath);
            if (savedPrefab == null)
            {
                throw new InvalidOperationException($"Failed to overwrite prefab at {prefabAssetPath}.");
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(clone);
        }
    }

    private static bool ConfirmSceneOverwrite(string subjectLabel, string prefabAssetPath)
    {
        if (Application.isBatchMode)
        {
            return true;
        }

        return EditorUtility.DisplayDialog(
            $"Overwrite {subjectLabel}",
            $"This will capture the current scene adjustments and overwrite the system prefab:\n{prefabAssetPath}\n\nDo you want to continue?",
            "Overwrite",
            "Cancel");
    }

    private static Vector3 EnsureNonZeroScale(Vector3 value)
    {
        return new Vector3(
            Mathf.Approximately(value.x, 0f) ? 1f : value.x,
            Mathf.Approximately(value.y, 0f) ? 1f : value.y,
            Mathf.Approximately(value.z, 0f) ? 1f : value.z);
    }

    private static Vector3 EnsureMinSize(Vector3 value)
    {
        return new Vector3(
            Mathf.Max(0.01f, value.x),
            Mathf.Max(0.01f, value.y),
            Mathf.Max(0.01f, value.z));
    }

    private static void ShowSummary(string title, string message)
    {
        Debug.Log($"[MemoryDisplayFurnitureBuilderWindow] {message}");

        if (!Application.isBatchMode)
        {
            EditorUtility.DisplayDialog(title, message, "OK");
        }
    }

    private static bool ConfirmRebuildExistingPrefabs(string subjectLabel)
    {
        if (Application.isBatchMode)
        {
            return true;
        }

        return EditorUtility.DisplayDialog(
            $"Rebuild {subjectLabel} Prefabs",
            $"Rebuild will overwrite existing {subjectLabel.ToLowerInvariant()} prefabs. Any manual prefab adjustments may be lost.\n\nDo you want to continue?",
            "Rebuild",
            "Cancel");
    }

    private sealed class FurnitureBuildSettings
    {
        public string FurnitureId;
        public DisplayFurnitureAuthoringMode AuthoringMode;
        public bool EnableDisplayFeature;
        public bool EnableLightFeature;
        public bool EnableFrameFeature;
        public FurnitureType FurnitureType;
        public SlotPreset SlotPreset;
        public LightFeaturePreset LightFeaturePreset;
        public FrameSurfacePreset FrameSurfacePreset;
        public float SlotHeightOffset;
        public Vector3 BoundsPadding;
        public bool ClearExistingSlots;
        public bool CreatePlacementBounds;
        public bool AutoFitBoundsFromRenderers;
        public bool CreateBlockingCollider;
        public bool GenerateSlotsFromPlacementBounds;
        public DisplayFurnitureBuildProfile PrefabProfile;
    }

    private sealed class BuildResult
    {
        public BuildResult(MemoryDisplayFurniture furniture, int slotCount)
        {
            Furniture = furniture;
            SlotCount = slotCount;
        }

        public MemoryDisplayFurniture Furniture { get; }
        public int SlotCount { get; }
    }

    private sealed class DisplayFurnitureSourceEntry
    {
        public DisplayFurnitureSourceEntry(
            string modelId,
            string sourceAssetPath,
            string sourceFolderAssetPath,
            string dataFolderAssetPath,
            string prefabAssetPath,
            string profileAssetPath)
        {
            ModelId = modelId;
            SourceAssetPath = sourceAssetPath;
            SourceFolderAssetPath = sourceFolderAssetPath;
            DataFolderAssetPath = dataFolderAssetPath;
            PrefabAssetPath = prefabAssetPath;
            ProfileAssetPath = profileAssetPath;
        }

        public string ModelId { get; }
        public string SourceAssetPath { get; }
        public string SourceFolderAssetPath { get; }
        public string DataFolderAssetPath { get; }
        public string PrefabAssetPath { get; }
        public string ProfileAssetPath { get; }
        public string PrefabRootName => $"PF_{ModelId}";
    }

    private sealed class SlotDefinition
    {
        public SlotDefinition(string name, Vector3 localPosition, SlotType type, List<ItemSize> acceptedSizes)
        {
            Name = name;
            LocalPosition = localPosition;
            Type = type;
            AcceptedSizes = acceptedSizes;
        }

        public string Name { get; }
        public Vector3 LocalPosition { get; }
        public SlotType Type { get; }
        public List<ItemSize> AcceptedSizes { get; }
    }
}
