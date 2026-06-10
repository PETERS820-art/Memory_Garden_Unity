using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class MemoryDisplayFurnitureBuilderWindow : EditorWindow
{
    private const string MenuItemPath = "Tools/Memory Garden/Display Furniture Builder";
    private const string PlacementBoundsObjectName = "_PlacementBounds";
    private const string BlockingColliderObjectName = "_BlockingCollider";
    private const string DefaultPrefabFolder = "Assets/Prefabs/MemoryGarden/DisplayFurniture";
    private const float DefaultSingleFallbackOffset = 0.25f;
    private const float LeftRightNormalized = 0.2f;
    private const float RightNormalized = 0.8f;

    private GameObject furnitureTarget;
    private FurnitureType furnitureType = FurnitureType.Shelf;
    private SlotPreset slotPreset = SlotPreset.ShelfLeftCenterRight;
    private float slotHeightOffset = 0.02f;
    private bool clearExistingSlots = true;
    private bool createOrUpdatePlacementBounds = true;
    private bool autoFitBoundsFromRenderers = true;
    private bool createBlockingColliderOption = true;
    private bool generateSlotsFromPlacementBounds = true;
    private bool saveAsPrefab;
    private string prefabFolder = DefaultPrefabFolder;

    [MenuItem(MenuItemPath)]
    public static void OpenWindow()
    {
        MemoryDisplayFurnitureBuilderWindow window =
            GetWindow<MemoryDisplayFurnitureBuilderWindow>("Display Furniture Builder");
        window.minSize = new Vector2(380f, 260f);
        window.Show();
    }

    private void OnGUI()
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
        furnitureType = (FurnitureType)EditorGUILayout.EnumPopup("Furniture Type", furnitureType);
        slotPreset = (SlotPreset)EditorGUILayout.EnumPopup("Slot Preset", slotPreset);
        slotHeightOffset = EditorGUILayout.FloatField("Slot Height Offset", slotHeightOffset);
        clearExistingSlots = EditorGUILayout.Toggle("Clear Existing Slots", clearExistingSlots);
        createOrUpdatePlacementBounds = EditorGUILayout.Toggle("Create / Update Placement Bounds", createOrUpdatePlacementBounds);
        autoFitBoundsFromRenderers = EditorGUILayout.Toggle("Auto Fit Bounds From Renderers", autoFitBoundsFromRenderers);
        createBlockingColliderOption = EditorGUILayout.Toggle("Create Blocking Collider", createBlockingColliderOption);
        generateSlotsFromPlacementBounds = EditorGUILayout.Toggle("Generate Slots From Placement Bounds", generateSlotsFromPlacementBounds);
        saveAsPrefab = EditorGUILayout.Toggle("Save As Prefab", saveAsPrefab);
        using (new EditorGUI.DisabledScope(!saveAsPrefab))
        {
            prefabFolder = EditorGUILayout.TextField("Prefab Folder", prefabFolder);
        }

        EditorGUILayout.Space();

        using (new EditorGUI.DisabledScope(furnitureTarget == null))
        {
            if (GUILayout.Button("Build / Update"))
            {
                BuildOrUpdateFurniture();
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

    private void BuildOrUpdateFurniture()
    {
        if (furnitureTarget == null)
        {
            Debug.LogWarning("[MemoryDisplayFurnitureBuilderWindow] No furniture target selected.");
            return;
        }

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Build Display Furniture");
        int undoGroup = Undo.GetCurrentGroup();

        MemoryDisplayFurniture furniture = GetOrAddComponentWithUndo<MemoryDisplayFurniture>(furnitureTarget);
        ApplyFurnitureSettings(furniture, furnitureTarget.name);

        BoxCollider placementBoundsCollider = null;
        if (createOrUpdatePlacementBounds || generateSlotsFromPlacementBounds || createBlockingColliderOption)
        {
            placementBoundsCollider = CreateOrUpdatePlacementBounds(furnitureTarget.transform, furniture);
        }

        if (createBlockingColliderOption && placementBoundsCollider != null)
        {
            CreateOrUpdateBlockingCollider(furnitureTarget.transform, placementBoundsCollider);
        }

        Transform slotsRoot = FindOrCreateSlotsRoot(furnitureTarget.transform);
        if (clearExistingSlots)
        {
            ClearExistingSlotChildren(slotsRoot);
        }

        List<SlotDefinition> slotDefinitions = CreateSlotDefinitions(furnitureTarget.transform, furniture, placementBoundsCollider);
        for (int i = 0; i < slotDefinitions.Count; i++)
        {
            CreateOrUpdateSlot(slotsRoot, slotDefinitions[i]);
        }

        furniture.AutoCollectSlots();

        if (saveAsPrefab)
        {
            SaveFurnitureAsPrefab(furnitureTarget);
        }

        EditorUtility.SetDirty(furnitureTarget);
        EditorUtility.SetDirty(furniture);
        EditorUtility.SetDirty(slotsRoot.gameObject);

        for (int i = 0; i < furniture.Slots.Count; i++)
        {
            MemoryDisplaySlot slot = furniture.Slots[i];
            if (slot != null)
            {
                EditorUtility.SetDirty(slot);
                EditorUtility.SetDirty(slot.gameObject);
            }
        }

        Undo.CollapseUndoOperations(undoGroup);
        Debug.Log(
            $"[MemoryDisplayFurnitureBuilderWindow] Built {slotDefinitions.Count} slot(s) for {furnitureTarget.name}.",
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
            MemoryObject memoryObject = GetOrAddComponentWithUndo<MemoryObject>(target);
            EnsurePlacementDefaults(memoryObject, wasMissingMemoryObject);

            configuredCount++;
            EditorUtility.SetDirty(memoryObject);
            EditorUtility.SetDirty(target);
        }

        Undo.CollapseUndoOperations(undoGroup);
        Debug.Log(
            $"[MemoryDisplayFurnitureBuilderWindow] Configured placement settings on {configuredCount} object(s).");
    }

    private void ApplyFurnitureSettings(MemoryDisplayFurniture furniture, string fallbackId)
    {
        SerializedObject serializedFurniture = new SerializedObject(furniture);
        serializedFurniture.UpdateIfRequiredOrScript();

        SerializedProperty furnitureIdProperty = serializedFurniture.FindProperty("furnitureId");
        if (furnitureIdProperty != null && string.IsNullOrWhiteSpace(furnitureIdProperty.stringValue))
        {
            furnitureIdProperty.stringValue = fallbackId;
        }

        SerializedProperty furnitureTypeProperty = serializedFurniture.FindProperty("furnitureType");
        if (furnitureTypeProperty != null)
        {
            furnitureTypeProperty.enumValueIndex = (int)furnitureType;
        }

        SerializedProperty usePlacementBoundsForSlotsProperty = serializedFurniture.FindProperty("usePlacementBoundsForSlots");
        if (usePlacementBoundsForSlotsProperty != null)
        {
            usePlacementBoundsForSlotsProperty.boolValue = generateSlotsFromPlacementBounds;
        }

        SerializedProperty createBlockingColliderProperty = serializedFurniture.FindProperty("createBlockingCollider");
        if (createBlockingColliderProperty != null)
        {
            createBlockingColliderProperty.boolValue = createBlockingColliderOption;
        }

        serializedFurniture.ApplyModifiedPropertiesWithoutUndo();
    }

    private BoxCollider CreateOrUpdatePlacementBounds(Transform furnitureTransform, MemoryDisplayFurniture furniture)
    {
        Transform placementBoundsTransform = FindOrCreateChild(furnitureTransform, PlacementBoundsObjectName);
        BoxCollider placementBoundsCollider = GetOrAddComponentWithUndo<BoxCollider>(placementBoundsTransform.gameObject);

        if (autoFitBoundsFromRenderers)
        {
            Undo.RecordObject(placementBoundsCollider, "Auto Fit Placement Bounds");
            furniture.AutoAssignPlacementBounds();

            SerializedObject serializedFurniture = new SerializedObject(furniture);
            serializedFurniture.UpdateIfRequiredOrScript();
            SerializedProperty placementBoundsProperty = serializedFurniture.FindProperty("placementBoundsCollider");
            if (placementBoundsProperty != null)
            {
                placementBoundsProperty.objectReferenceValue = placementBoundsCollider;
            }
            serializedFurniture.ApplyModifiedPropertiesWithoutUndo();

            furniture.AutoFitPlacementBoundsFromRenderers();
        }
        else
        {
            SerializedObject serializedFurniture = new SerializedObject(furniture);
            serializedFurniture.UpdateIfRequiredOrScript();
            SerializedProperty placementBoundsProperty = serializedFurniture.FindProperty("placementBoundsCollider");
            if (placementBoundsProperty != null)
            {
                placementBoundsProperty.objectReferenceValue = placementBoundsCollider;
            }
            serializedFurniture.ApplyModifiedPropertiesWithoutUndo();
            furniture.AutoAssignPlacementBounds();
        }

        placementBoundsCollider.isTrigger = true;

        EditorUtility.SetDirty(placementBoundsTransform.gameObject);
        EditorUtility.SetDirty(placementBoundsCollider);
        return placementBoundsCollider;
    }

    private void CreateOrUpdateBlockingCollider(Transform furnitureTransform, BoxCollider placementBoundsCollider)
    {
        Transform blockingColliderTransform = FindOrCreateChild(furnitureTransform, BlockingColliderObjectName);
        BoxCollider blockingCollider = GetOrAddComponentWithUndo<BoxCollider>(blockingColliderTransform.gameObject);

        Undo.RecordObject(blockingColliderTransform, "Update Blocking Collider");
        Undo.RecordObject(blockingCollider, "Update Blocking Collider");

        blockingColliderTransform.localPosition = placementBoundsCollider.transform.localPosition;
        blockingColliderTransform.localRotation = placementBoundsCollider.transform.localRotation;
        blockingColliderTransform.localScale = placementBoundsCollider.transform.localScale;

        blockingCollider.center = placementBoundsCollider.center;
        blockingCollider.size = placementBoundsCollider.size;
        blockingCollider.isTrigger = false;

        EditorUtility.SetDirty(blockingColliderTransform.gameObject);
        EditorUtility.SetDirty(blockingCollider);
    }

    private Transform FindOrCreateSlotsRoot(Transform furnitureTransform)
    {
        Transform slotsRoot = furnitureTransform.Find("Slots");
        if (slotsRoot != null)
        {
            return slotsRoot;
        }

        GameObject slotsRootObject = new GameObject("Slots");
        Undo.RegisterCreatedObjectUndo(slotsRootObject, "Create Slots Root");
        slotsRoot = slotsRootObject.transform;
        slotsRoot.SetParent(furnitureTransform, false);
        slotsRoot.localPosition = Vector3.zero;
        slotsRoot.localRotation = Quaternion.identity;
        slotsRoot.localScale = Vector3.one;
        return slotsRoot;
    }

    private Transform FindOrCreateChild(Transform parent, string childName)
    {
        Transform child = parent.Find(childName);
        if (child != null)
        {
            return child;
        }

        GameObject childObject = new GameObject(childName);
        Undo.RegisterCreatedObjectUndo(childObject, $"Create {childName}");
        child = childObject.transform;
        child.SetParent(parent, false);
        child.localPosition = Vector3.zero;
        child.localRotation = Quaternion.identity;
        child.localScale = Vector3.one;
        return child;
    }

    private void ClearExistingSlotChildren(Transform slotsRoot)
    {
        for (int i = slotsRoot.childCount - 1; i >= 0; i--)
        {
            Undo.DestroyObjectImmediate(slotsRoot.GetChild(i).gameObject);
        }
    }

    private void CreateOrUpdateSlot(Transform slotsRoot, SlotDefinition definition)
    {
        Transform slotTransform = slotsRoot.Find(definition.Name);
        GameObject slotObject;

        if (slotTransform == null)
        {
            slotObject = new GameObject(definition.Name);
            Undo.RegisterCreatedObjectUndo(slotObject, "Create Display Slot");
            slotTransform = slotObject.transform;
            slotTransform.SetParent(slotsRoot, false);
        }
        else
        {
            slotObject = slotTransform.gameObject;
            Undo.RecordObject(slotTransform, "Update Display Slot");
        }

        slotObject.name = definition.Name;
        slotTransform.localPosition = definition.LocalPosition;
        slotTransform.localRotation = Quaternion.identity;
        slotTransform.localScale = Vector3.one;

        MemoryDisplaySlot slot = GetOrAddComponentWithUndo<MemoryDisplaySlot>(slotObject);
        ApplySlotSettings(slot, definition);

        EditorUtility.SetDirty(slotTransform);
        EditorUtility.SetDirty(slotObject);
        EditorUtility.SetDirty(slot);
    }

    private void ApplySlotSettings(MemoryDisplaySlot slot, SlotDefinition definition)
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
        MemoryDisplayFurniture furniture,
        BoxCollider placementBoundsCollider)
    {
        if (slotPreset == SlotPreset.Custom)
        {
            return new List<SlotDefinition>();
        }

        bool hasLocalBounds = false;
        Bounds localBounds = default;

        if (generateSlotsFromPlacementBounds && placementBoundsCollider != null)
        {
            hasLocalBounds = TryGetColliderBoundsInRootLocal(placementBoundsCollider, furnitureTransform, out localBounds);
        }

        if (!hasLocalBounds)
        {
            hasLocalBounds = TryGetCombinedLocalRendererBounds(furnitureTransform, out localBounds);
        }

        float topY = hasLocalBounds ? localBounds.max.y + slotHeightOffset : slotHeightOffset;
        float centerX = hasLocalBounds ? localBounds.center.x : 0f;
        float centerZ = hasLocalBounds ? localBounds.center.z : 0f;
        float leftX = hasLocalBounds ? Mathf.Lerp(localBounds.min.x, localBounds.max.x, LeftRightNormalized) : -DefaultSingleFallbackOffset;
        float rightX = hasLocalBounds ? Mathf.Lerp(localBounds.min.x, localBounds.max.x, RightNormalized) : DefaultSingleFallbackOffset;

        List<SlotDefinition> definitions = new List<SlotDefinition>();

        switch (slotPreset)
        {
            case SlotPreset.SingleCenter:
                definitions.Add(new SlotDefinition(
                    "Slot_Center",
                    new Vector3(centerX, topY, centerZ),
                    ResolveSingleCenterSlotType(),
                    ResolveSingleCenterAcceptedSizes()));
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

    private SlotType ResolveSingleCenterSlotType()
    {
        if (furnitureType == FurnitureType.FloorPad)
        {
            return SlotType.FloorLarge;
        }

        if (furnitureType == FurnitureType.WallShelf)
        {
            return SlotType.WallShelf;
        }

        return SlotType.MediumTabletop;
    }

    private List<ItemSize> ResolveSingleCenterAcceptedSizes()
    {
        if (furnitureType == FurnitureType.FloorPad)
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

    private void SaveFurnitureAsPrefab(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        string normalizedFolder = string.IsNullOrWhiteSpace(prefabFolder)
            ? DefaultPrefabFolder
            : prefabFolder.Replace('\\', '/').TrimEnd('/');

        EnsureAssetFolder(normalizedFolder);
        string prefabAssetPath = $"{normalizedFolder}/{target.name}.prefab";

        if (EditorUtility.IsPersistent(target))
        {
            PrefabUtility.SaveAsPrefabAsset(target, prefabAssetPath);
            return;
        }

        PrefabUtility.SaveAsPrefabAssetAndConnect(target, prefabAssetPath, InteractionMode.UserAction);
    }

    private static void EnsureAssetFolder(string assetFolderPath)
    {
        if (AssetDatabase.IsValidFolder(assetFolderPath))
        {
            return;
        }

        string normalizedPath = assetFolderPath.Replace('\\', '/').TrimEnd('/');
        if (!normalizedPath.StartsWith("Assets"))
        {
            throw new IOException($"Prefab folder must be inside Assets: {assetFolderPath}");
        }

        string[] parts = normalizedPath.Split('/');
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

    private static T GetOrAddComponentWithUndo<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        if (component == null)
        {
            component = Undo.AddComponent<T>(target);
        }

        return component;
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
