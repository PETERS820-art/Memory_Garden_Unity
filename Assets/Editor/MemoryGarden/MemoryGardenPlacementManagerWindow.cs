using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MemoryGardenPlacementManagerWindow : EditorWindow
{
    private const string MenuItemPath = "Tools/Memory Garden/Placement Manager";
    private const string SystemsRootObjectName = "-04-SYSTEMS";
    private const string ManagerObjectName = "MemoryGardenPlacementManager";
    private const string PlacementLayoutFolderRoot = "Assets/_project/ScriptableObjects";
    private const string PlacementLayoutFolderPath = "Assets/_project/ScriptableObjects/PlacementLayouts";

    private Vector2 authoringScrollPosition;
    private Vector2 unassignedScrollPosition;
    private MemoryGardenPlacementLayout layoutOverride;
    private bool showAuthoringSection = true;
    private bool showUnassignedSection = true;

    [MenuItem(MenuItemPath)]
    public static void OpenWindow()
    {
        MemoryGardenPlacementManagerWindow window =
            GetWindow<MemoryGardenPlacementManagerWindow>("Placement Manager");
        window.minSize = new Vector2(520f, 360f);
        window.Show();
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "Author placement layouts with scene object pickers instead of typing IDs. The layout asset still stores stable itemId / furnitureId / slotId strings for runtime use.",
            MessageType.Info);

        MemoryGardenPlacementManager manager = FindPlacementManager();
        MemoryGardenPlacementLayout activeLayout = DrawSceneReferences(manager);

        EditorGUILayout.Space();
        DrawPrimaryActions();

        EditorGUILayout.Space();
        DrawAuthoringSection(activeLayout);
    }

    private MemoryGardenPlacementLayout DrawSceneReferences(MemoryGardenPlacementManager manager)
    {
        MemoryGardenPlacementLayout managerLayout = manager != null ? manager.DefaultLayout : null;
        MemoryGardenPlacementLayout activeLayout = layoutOverride != null ? layoutOverride : managerLayout;

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Scene References", EditorStyles.boldLabel);
            EditorGUILayout.ObjectField("Placement Manager", manager, typeof(MemoryGardenPlacementManager), true);
            layoutOverride = (MemoryGardenPlacementLayout)EditorGUILayout.ObjectField(
                "Layout Asset",
                activeLayout,
                typeof(MemoryGardenPlacementLayout),
                false);

            if (manager != null && managerLayout != null && layoutOverride == null)
            {
                EditorGUILayout.HelpBox(
                    $"Using manager default layout: {managerLayout.name}",
                    MessageType.None);
            }
        }

        return layoutOverride != null ? layoutOverride : managerLayout;
    }

    private void DrawPrimaryActions()
    {
        if (GUILayout.Button("Create Missing Placement Layout Folder"))
        {
            EnsurePlacementLayoutFolders(showDialog: true);
        }

        if (GUILayout.Button("Ensure Placement Manager In Scene"))
        {
            EnsurePlacementManagerInScene(selectCreatedObject: true);
        }

        if (GUILayout.Button("Create Default Placement Layout From Current Scene"))
        {
            CreateDefaultPlacementLayoutFromCurrentScene();
        }

        if (GUILayout.Button("Validate Current Placement Layout"))
        {
            ValidateCurrentPlacementLayout();
        }
    }

    private void DrawAuthoringSection(MemoryGardenPlacementLayout layout)
    {
        showAuthoringSection = EditorGUILayout.Foldout(
            showAuthoringSection,
            "Layout Authoring",
            true);

        if (!showAuthoringSection)
        {
            return;
        }

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            if (layout == null)
            {
                EditorGUILayout.HelpBox(
                    "Assign a layout asset above, or create one from the current scene, to start authoring placement records here.",
                    MessageType.Warning);
                return;
            }

            EnsureRecordList(layout);

            List<MemoryObject> sceneItems = CollectSceneItems();
            List<MemoryDisplayFurniture> sceneFurniture = CollectSceneFurniture();
            HashSet<string> assignedItemIds = CollectAssignedItemIds(layout.records);
            int unassignedCount = CountUnassignedItems(sceneItems, assignedItemIds);

            EditorGUILayout.LabelField("Active Layout", EditorStyles.boldLabel);
            EditorGUILayout.ObjectField("Layout Asset", layout, typeof(MemoryGardenPlacementLayout), false);
            EditorGUI.BeginChangeCheck();
            string editedLayoutId = EditorGUILayout.TextField("Layout Id", layout.layoutId);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(layout, "Change Layout Id");
                layout.layoutId = editedLayoutId;
                MarkLayoutDirty(layout);
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField(
                $"Records: {layout.records.Count}    Scene Items: {sceneItems.Count}    Unassigned Items: {unassignedCount}",
                EditorStyles.miniBoldLabel);

            DrawAuthoringButtons(layout, sceneItems, assignedItemIds);
            DrawInlineLayoutWarnings(layout, sceneItems, sceneFurniture);
            DrawRecordList(layout, sceneItems, sceneFurniture);
            DrawUnassignedItems(layout, sceneItems, assignedItemIds);
        }
    }

    private void DrawAuthoringButtons(
        MemoryGardenPlacementLayout layout,
        List<MemoryObject> sceneItems,
        HashSet<string> assignedItemIds)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Capture Current Scene Layout"))
            {
                CaptureCurrentSceneLayoutIntoAsset(layout);
            }

            if (GUILayout.Button("Add Unassigned Scene Items"))
            {
                AddUnassignedSceneItemsToLayout(layout, sceneItems, assignedItemIds);
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Add Empty Record"))
            {
                Undo.RecordObject(layout, "Add Empty Placement Record");
                layout.records.Add(new MemoryItemPlacementRecord());
                MarkLayoutDirty(layout);
            }

            if (GUILayout.Button("Sort Records By Item Id"))
            {
                Undo.RecordObject(layout, "Sort Placement Records");
                layout.records.Sort(ComparePlacementRecordsByItemId);
                MarkLayoutDirty(layout);
            }
        }
    }

    private void DrawInlineLayoutWarnings(
        MemoryGardenPlacementLayout layout,
        List<MemoryObject> sceneItems,
        List<MemoryDisplayFurniture> sceneFurniture)
    {
        List<string> warnings = BuildLayoutWarnings(layout.records, sceneItems, sceneFurniture);
        if (warnings.Count == 0)
        {
            return;
        }

        int shownWarnings = Mathf.Min(warnings.Count, 5);
        for (int i = 0; i < shownWarnings; i++)
        {
            EditorGUILayout.HelpBox(warnings[i], MessageType.Warning);
        }

        if (warnings.Count > shownWarnings)
        {
            EditorGUILayout.HelpBox(
                $"{warnings.Count - shownWarnings} more warning(s) are hidden here. Use Validate Current Placement Layout for the full console report.",
                MessageType.None);
        }
    }

    private void DrawRecordList(
        MemoryGardenPlacementLayout layout,
        List<MemoryObject> sceneItems,
        List<MemoryDisplayFurniture> sceneFurniture)
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Placement Records", EditorStyles.boldLabel);

        if (layout.records.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "This layout has no records yet. Capture from the current scene, add unassigned items, or create an empty record.",
                MessageType.None);
            return;
        }

        authoringScrollPosition = EditorGUILayout.BeginScrollView(
            authoringScrollPosition,
            GUILayout.MinHeight(220f),
            GUILayout.MaxHeight(420f));

        int removeIndex = -1;

        for (int i = 0; i < layout.records.Count; i++)
        {
            MemoryItemPlacementRecord record = layout.records[i] ?? new MemoryItemPlacementRecord();
            layout.records[i] = record;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"Record {i + 1}", EditorStyles.boldLabel);

                    if (GUILayout.Button("Use Item Current Slot", GUILayout.Width(140f)))
                    {
                        UseCurrentSlotForRecord(layout, record, sceneItems);
                    }

                    if (GUILayout.Button("Remove", GUILayout.Width(70f)))
                    {
                        removeIndex = i;
                    }
                }

                DrawRecordFields(layout, record, sceneItems, sceneFurniture);
                DrawResolvedIdSummary(record, sceneItems, sceneFurniture);
            }
        }

        EditorGUILayout.EndScrollView();

        if (removeIndex >= 0)
        {
            Undo.RecordObject(layout, "Remove Placement Record");
            layout.records.RemoveAt(removeIndex);
            MarkLayoutDirty(layout);
        }
    }

    private void DrawRecordFields(
        MemoryGardenPlacementLayout layout,
        MemoryItemPlacementRecord record,
        List<MemoryObject> sceneItems,
        List<MemoryDisplayFurniture> sceneFurniture)
    {
        MemoryObject currentItem = FindItemByResolvedId(sceneItems, record.itemId);
        MemoryObject selectedItem = (MemoryObject)EditorGUILayout.ObjectField(
            "Item",
            currentItem,
            typeof(MemoryObject),
            true);

        if (selectedItem != currentItem)
        {
            Undo.RecordObject(layout, "Change Placement Item");
            record.itemId = GetResolvedItemId(selectedItem);

            if (selectedItem != null)
            {
                ApplyCurrentSlotToRecord(record, selectedItem);
            }

            MarkLayoutDirty(layout);
        }

        MemoryDisplayFurniture currentFurniture = FindFurnitureByResolvedId(sceneFurniture, record.furnitureId);
        MemoryDisplayFurniture selectedFurniture = (MemoryDisplayFurniture)EditorGUILayout.ObjectField(
            "Furniture",
            currentFurniture,
            typeof(MemoryDisplayFurniture),
            true);

        if (selectedFurniture != currentFurniture)
        {
            Undo.RecordObject(layout, "Change Placement Furniture");
            record.furnitureId = GetResolvedFurnitureId(selectedFurniture);

            if (selectedFurniture == null)
            {
                record.slotId = string.Empty;
            }
            else if (!FurnitureContainsSlot(selectedFurniture, record.slotId))
            {
                record.slotId = GetFirstSlotId(selectedFurniture);
            }

            MarkLayoutDirty(layout);
        }

        DrawSlotSelector(layout, record, selectedFurniture);
    }

    private void DrawSlotSelector(
        MemoryGardenPlacementLayout layout,
        MemoryItemPlacementRecord record,
        MemoryDisplayFurniture selectedFurniture)
    {
        using (new EditorGUI.DisabledScope(selectedFurniture == null))
        {
            List<MemoryDisplaySlot> furnitureSlots = GetFurnitureSlots(selectedFurniture);

            if (selectedFurniture == null)
            {
                EditorGUILayout.TextField("Slot", "Select furniture first");
                return;
            }

            if (furnitureSlots.Count == 0)
            {
                EditorGUILayout.TextField("Slot", "No slots found on furniture");
                return;
            }

            string[] slotLabels = new string[furnitureSlots.Count];
            int selectedIndex = -1;

            for (int i = 0; i < furnitureSlots.Count; i++)
            {
                MemoryDisplaySlot slot = furnitureSlots[i];
                string slotId = GetResolvedSlotId(slot);
                slotLabels[i] = $"{slotId} ({slot.name})";

                if (string.Equals(slotId, record.slotId, StringComparison.OrdinalIgnoreCase))
                {
                    selectedIndex = i;
                }
            }

            if (selectedIndex < 0)
            {
                string[] missingAwareLabels = new string[furnitureSlots.Count + 1];
                missingAwareLabels[0] = string.IsNullOrWhiteSpace(record.slotId)
                    ? "<Select Slot>"
                    : $"<Missing Slot: {record.slotId}>";

                for (int i = 0; i < furnitureSlots.Count; i++)
                {
                    missingAwareLabels[i + 1] = slotLabels[i];
                }

                EditorGUI.BeginChangeCheck();
                int newMissingAwareIndex = EditorGUILayout.Popup("Slot", 0, missingAwareLabels);
                if (EditorGUI.EndChangeCheck() && newMissingAwareIndex > 0)
                {
                    Undo.RecordObject(layout, "Change Placement Slot");
                    record.slotId = GetResolvedSlotId(furnitureSlots[newMissingAwareIndex - 1]);
                    MarkLayoutDirty(layout);
                }

                return;
            }

            EditorGUI.BeginChangeCheck();
            int newIndex = EditorGUILayout.Popup("Slot", selectedIndex, slotLabels);
            if (EditorGUI.EndChangeCheck())
            {
                string newSlotId = GetResolvedSlotId(furnitureSlots[newIndex]);
                if (!string.Equals(record.slotId, newSlotId, StringComparison.OrdinalIgnoreCase))
                {
                    Undo.RecordObject(layout, "Change Placement Slot");
                    record.slotId = newSlotId;
                    MarkLayoutDirty(layout);
                }
            }
        }
    }

    private void DrawResolvedIdSummary(
        MemoryItemPlacementRecord record,
        List<MemoryObject> sceneItems,
        List<MemoryDisplayFurniture> sceneFurniture)
    {
        string itemSummary = string.IsNullOrWhiteSpace(record.itemId) ? "(empty)" : record.itemId;
        string furnitureSummary = string.IsNullOrWhiteSpace(record.furnitureId) ? "(empty)" : record.furnitureId;
        string slotSummary = string.IsNullOrWhiteSpace(record.slotId) ? "(empty)" : record.slotId;

        EditorGUILayout.LabelField(
            $"Stored IDs: {itemSummary}  ->  {furnitureSummary} / {slotSummary}",
            EditorStyles.miniLabel);

        MemoryObject item = FindItemByResolvedId(sceneItems, record.itemId);
        MemoryDisplayFurniture furniture = FindFurnitureByResolvedId(sceneFurniture, record.furnitureId);
        MemoryDisplaySlot slot = FindSlotByResolvedId(furniture, record.slotId);

        if (!string.IsNullOrWhiteSpace(record.itemId) && item == null)
        {
            EditorGUILayout.HelpBox($"Item '{record.itemId}' is not found in the current scene.", MessageType.Warning);
        }

        if (!string.IsNullOrWhiteSpace(record.furnitureId) && furniture == null)
        {
            EditorGUILayout.HelpBox($"Furniture '{record.furnitureId}' is not found in the current scene.", MessageType.Warning);
        }

        if (!string.IsNullOrWhiteSpace(record.slotId) && furniture != null && slot == null)
        {
            EditorGUILayout.HelpBox(
                $"Slot '{record.slotId}' is not found under furniture '{GetResolvedFurnitureId(furniture)}'.",
                MessageType.Warning);
        }
    }

    private void DrawUnassignedItems(
        MemoryGardenPlacementLayout layout,
        List<MemoryObject> sceneItems,
        HashSet<string> assignedItemIds)
    {
        showUnassignedSection = EditorGUILayout.Foldout(
            showUnassignedSection,
            $"Unassigned Scene Items ({CountUnassignedItems(sceneItems, assignedItemIds)})",
            true);

        if (!showUnassignedSection)
        {
            return;
        }

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            List<MemoryObject> unassignedItems = CollectUnassignedItems(sceneItems, assignedItemIds);
            if (unassignedItems.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "All scene items already have a record in this layout.",
                    MessageType.None);
                return;
            }

            unassignedScrollPosition = EditorGUILayout.BeginScrollView(
                unassignedScrollPosition,
                GUILayout.MaxHeight(180f));

            for (int i = 0; i < unassignedItems.Count; i++)
            {
                MemoryObject item = unassignedItems[i];
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.ObjectField(item, typeof(MemoryObject), true);
                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button("Add Record", GUILayout.Width(90f)))
                    {
                        AddRecordForItem(layout, item);
                    }
                }
            }

            EditorGUILayout.EndScrollView();
        }
    }

    private static MemoryGardenPlacementManager FindPlacementManager()
    {
        return UnityEngine.Object.FindFirstObjectByType<MemoryGardenPlacementManager>(FindObjectsInactive.Include);
    }

    private static void EnsurePlacementLayoutFolders(bool showDialog)
    {
        bool createdAnyFolder = false;

        if (!AssetDatabase.IsValidFolder(PlacementLayoutFolderRoot))
        {
            AssetDatabase.CreateFolder("Assets/_project", "ScriptableObjects");
            createdAnyFolder = true;
        }

        if (!AssetDatabase.IsValidFolder(PlacementLayoutFolderPath))
        {
            AssetDatabase.CreateFolder(PlacementLayoutFolderRoot, "PlacementLayouts");
            createdAnyFolder = true;
        }

        if (createdAnyFolder)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        if (!showDialog)
        {
            return;
        }

        EditorUtility.DisplayDialog(
            "Placement Layout Folders",
            createdAnyFolder
                ? "Placement layout folders are ready."
                : "Placement layout folders already exist.",
            "OK");
    }

    private static MemoryGardenPlacementManager EnsurePlacementManagerInScene(bool selectCreatedObject)
    {
        MemoryGardenPlacementManager existingManager = FindPlacementManager();
        if (existingManager != null)
        {
            if (selectCreatedObject)
            {
                Selection.activeGameObject = existingManager.gameObject;
                EditorGUIUtility.PingObject(existingManager.gameObject);
            }

            return existingManager;
        }

        GameObject systemsRoot = FindSystemsRoot();
        if (systemsRoot == null)
        {
            EditorUtility.DisplayDialog(
                "Placement Manager",
                "Could not find the top-level '-04-SYSTEMS' object. Create it manually first so the scene hierarchy stays unchanged.",
                "OK");
            return null;
        }

        GameObject managerObject = new GameObject(ManagerObjectName);
        Undo.RegisterCreatedObjectUndo(managerObject, "Create MemoryGardenPlacementManager");
        managerObject.transform.SetParent(systemsRoot.transform, false);
        MemoryGardenPlacementManager manager =
            Undo.AddComponent<MemoryGardenPlacementManager>(managerObject);

        EditorSceneManager.MarkSceneDirty(managerObject.scene);

        if (selectCreatedObject)
        {
            Selection.activeGameObject = managerObject;
            EditorGUIUtility.PingObject(managerObject);
        }

        return manager;
    }

    private static GameObject FindSystemsRoot()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid())
        {
            return null;
        }

        GameObject[] rootObjects = activeScene.GetRootGameObjects();
        for (int i = 0; i < rootObjects.Length; i++)
        {
            if (rootObjects[i] != null && rootObjects[i].name == SystemsRootObjectName)
            {
                return rootObjects[i];
            }
        }

        return null;
    }

    private void CreateDefaultPlacementLayoutFromCurrentScene()
    {
        EnsurePlacementLayoutFolders(showDialog: false);

        MemoryGardenPlacementManager manager = EnsurePlacementManagerInScene(selectCreatedObject: false);
        if (manager == null)
        {
            return;
        }

        manager.RegisterSceneObjects();
        manager.ValidateSceneIds();

        Scene activeScene = SceneManager.GetActiveScene();
        string sceneName = string.IsNullOrWhiteSpace(activeScene.name) ? "MemoryGardenScene" : activeScene.name;
        string assetName = $"{sceneName}_PlacementLayout.asset";
        string assetPath = AssetDatabase.GenerateUniqueAssetPath(
            Path.Combine(PlacementLayoutFolderPath, assetName).Replace("\\", "/"));

        MemoryGardenPlacementLayout layout = CreateInstance<MemoryGardenPlacementLayout>();
        layout.layoutId = $"{sceneName}_default_layout";
        layout.records = CaptureSceneLayoutRecords();

        AssetDatabase.CreateAsset(layout, assetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        AssignDefaultLayout(manager, layout);
        layoutOverride = layout;

        Selection.activeObject = layout;
        EditorGUIUtility.PingObject(layout);

        EditorUtility.DisplayDialog(
            "Placement Layout Created",
            $"Created default placement layout at:\n{assetPath}",
            "OK");
    }

    private void ValidateCurrentPlacementLayout()
    {
        MemoryGardenPlacementManager manager = FindPlacementManager();
        if (manager == null)
        {
            EditorUtility.DisplayDialog(
                "Placement Layout Validation",
                "No MemoryGardenPlacementManager was found in the scene.",
                "OK");
            return;
        }

        MemoryGardenPlacementLayout layout = layoutOverride != null
            ? layoutOverride
            : manager.DefaultLayout;

        if (layout == null)
        {
            EditorUtility.DisplayDialog(
                "Placement Layout Validation",
                "Assign a placement layout in the window or on the manager before validating.",
                "OK");
            return;
        }

        manager.RegisterSceneObjects();
        bool sceneValid = manager.ValidateSceneIds();
        bool layoutValid = manager.ValidateLayout(layout);

        EditorUtility.DisplayDialog(
            "Placement Layout Validation",
            sceneValid && layoutValid
                ? "Scene IDs and placement layout validation passed."
                : "Validation finished with warnings. Check the Unity Console for details.",
            "OK");
    }

    private void CaptureCurrentSceneLayoutIntoAsset(MemoryGardenPlacementLayout layout)
    {
        if (layout == null)
        {
            return;
        }

        List<MemoryItemPlacementRecord> capturedRecords = CaptureSceneLayoutRecords();
        Undo.RecordObject(layout, "Capture Current Scene Layout");
        layout.records = capturedRecords;
        MarkLayoutDirty(layout);
    }

    private static List<MemoryItemPlacementRecord> CaptureSceneLayoutRecords()
    {
        List<MemoryObject> items = CollectSceneItems();
        List<MemoryItemPlacementRecord> records = new List<MemoryItemPlacementRecord>();

        for (int i = 0; i < items.Count; i++)
        {
            MemoryObject item = items[i];
            if (item == null || item.CurrentSlot == null)
            {
                continue;
            }

            MemoryDisplayFurniture furniture = item.CurrentSlot.GetComponentInParent<MemoryDisplayFurniture>();
            if (furniture == null)
            {
                continue;
            }

            records.Add(new MemoryItemPlacementRecord
            {
                itemId = GetResolvedItemId(item),
                furnitureId = GetResolvedFurnitureId(furniture),
                slotId = GetResolvedSlotId(item.CurrentSlot)
            });
        }

        records.Sort(ComparePlacementRecordsByItemId);
        return records;
    }

    private void AddUnassignedSceneItemsToLayout(
        MemoryGardenPlacementLayout layout,
        List<MemoryObject> sceneItems,
        HashSet<string> assignedItemIds)
    {
        if (layout == null)
        {
            return;
        }

        List<MemoryObject> unassignedItems = CollectUnassignedItems(sceneItems, assignedItemIds);
        if (unassignedItems.Count == 0)
        {
            EditorUtility.DisplayDialog(
                "Add Unassigned Scene Items",
                "All scene items are already present in this layout.",
                "OK");
            return;
        }

        Undo.RecordObject(layout, "Add Unassigned Scene Items");

        for (int i = 0; i < unassignedItems.Count; i++)
        {
            MemoryObject item = unassignedItems[i];
            MemoryItemPlacementRecord record = CreateRecordForItem(item);
            layout.records.Add(record);
        }

        layout.records.Sort(ComparePlacementRecordsByItemId);
        MarkLayoutDirty(layout);
    }

    private void AddRecordForItem(MemoryGardenPlacementLayout layout, MemoryObject item)
    {
        if (layout == null || item == null)
        {
            return;
        }

        Undo.RecordObject(layout, "Add Placement Record");
        layout.records.Add(CreateRecordForItem(item));
        layout.records.Sort(ComparePlacementRecordsByItemId);
        MarkLayoutDirty(layout);
    }

    private static MemoryItemPlacementRecord CreateRecordForItem(MemoryObject item)
    {
        MemoryItemPlacementRecord record = new MemoryItemPlacementRecord
        {
            itemId = GetResolvedItemId(item),
            furnitureId = string.Empty,
            slotId = string.Empty
        };

        ApplyCurrentSlotToRecord(record, item);
        return record;
    }

    private void UseCurrentSlotForRecord(
        MemoryGardenPlacementLayout layout,
        MemoryItemPlacementRecord record,
        List<MemoryObject> sceneItems)
    {
        if (layout == null || record == null)
        {
            return;
        }

        MemoryObject item = FindItemByResolvedId(sceneItems, record.itemId);
        if (item == null)
        {
            return;
        }

        Undo.RecordObject(layout, "Use Item Current Slot");
        ApplyCurrentSlotToRecord(record, item);
        MarkLayoutDirty(layout);
    }

    private static void ApplyCurrentSlotToRecord(MemoryItemPlacementRecord record, MemoryObject item)
    {
        if (record == null || item == null || item.CurrentSlot == null)
        {
            return;
        }

        MemoryDisplayFurniture furniture = item.CurrentSlot.GetComponentInParent<MemoryDisplayFurniture>();
        if (furniture == null)
        {
            return;
        }

        record.furnitureId = GetResolvedFurnitureId(furniture);
        record.slotId = GetResolvedSlotId(item.CurrentSlot);
    }

    private static HashSet<string> CollectAssignedItemIds(List<MemoryItemPlacementRecord> records)
    {
        HashSet<string> assignedItemIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (records == null)
        {
            return assignedItemIds;
        }

        for (int i = 0; i < records.Count; i++)
        {
            MemoryItemPlacementRecord record = records[i];
            if (record == null || string.IsNullOrWhiteSpace(record.itemId))
            {
                continue;
            }

            assignedItemIds.Add(record.itemId.Trim());
        }

        return assignedItemIds;
    }

    private static int CountUnassignedItems(
        List<MemoryObject> sceneItems,
        HashSet<string> assignedItemIds)
    {
        return CollectUnassignedItems(sceneItems, assignedItemIds).Count;
    }

    private static List<MemoryObject> CollectUnassignedItems(
        List<MemoryObject> sceneItems,
        HashSet<string> assignedItemIds)
    {
        List<MemoryObject> unassignedItems = new List<MemoryObject>();

        for (int i = 0; i < sceneItems.Count; i++)
        {
            MemoryObject item = sceneItems[i];
            string itemId = GetResolvedItemId(item);
            if (string.IsNullOrWhiteSpace(itemId) || assignedItemIds.Contains(itemId))
            {
                continue;
            }

            unassignedItems.Add(item);
        }

        return unassignedItems;
    }

    private static List<string> BuildLayoutWarnings(
        List<MemoryItemPlacementRecord> records,
        List<MemoryObject> sceneItems,
        List<MemoryDisplayFurniture> sceneFurniture)
    {
        List<string> warnings = new List<string>();
        HashSet<string> claimedItems = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        HashSet<string> claimedSlots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < records.Count; i++)
        {
            MemoryItemPlacementRecord record = records[i];
            if (record == null)
            {
                warnings.Add($"Record {i + 1} is null.");
                continue;
            }

            string itemId = NormalizeId(record.itemId);
            string furnitureId = NormalizeId(record.furnitureId);
            string slotId = NormalizeId(record.slotId);

            if (string.IsNullOrWhiteSpace(itemId))
            {
                warnings.Add($"Record {i + 1} has an empty itemId.");
            }
            else if (!claimedItems.Add(itemId))
            {
                warnings.Add($"Item '{itemId}' is assigned more than once.");
            }

            if (!string.IsNullOrWhiteSpace(itemId) && FindItemByResolvedId(sceneItems, itemId) == null)
            {
                warnings.Add($"Item '{itemId}' is missing from the current scene.");
            }

            if (!string.IsNullOrWhiteSpace(furnitureId) && FindFurnitureByResolvedId(sceneFurniture, furnitureId) == null)
            {
                warnings.Add($"Furniture '{furnitureId}' is missing from the current scene.");
            }

            if (!string.IsNullOrWhiteSpace(furnitureId) && !string.IsNullOrWhiteSpace(slotId))
            {
                string slotKey = $"{furnitureId}::{slotId}";
                if (!claimedSlots.Add(slotKey))
                {
                    warnings.Add($"Slot '{slotId}' on furniture '{furnitureId}' is assigned more than once.");
                }

                MemoryDisplayFurniture furniture = FindFurnitureByResolvedId(sceneFurniture, furnitureId);
                if (furniture != null && FindSlotByResolvedId(furniture, slotId) == null)
                {
                    warnings.Add($"Slot '{slotId}' is not found under furniture '{furnitureId}'.");
                }
            }
        }

        return warnings;
    }

    private static List<MemoryObject> CollectSceneItems()
    {
        MemoryObject[] items =
            UnityEngine.Object.FindObjectsByType<MemoryObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        List<MemoryObject> sceneItems = new List<MemoryObject>(items);
        sceneItems.Sort((left, right) => string.Compare(
            GetResolvedItemId(left),
            GetResolvedItemId(right),
            StringComparison.OrdinalIgnoreCase));
        return sceneItems;
    }

    private static List<MemoryDisplayFurniture> CollectSceneFurniture()
    {
        MemoryDisplayFurniture[] furniture =
            UnityEngine.Object.FindObjectsByType<MemoryDisplayFurniture>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        List<MemoryDisplayFurniture> sceneFurniture = new List<MemoryDisplayFurniture>(furniture);
        sceneFurniture.Sort((left, right) => string.Compare(
            GetResolvedFurnitureId(left),
            GetResolvedFurnitureId(right),
            StringComparison.OrdinalIgnoreCase));

        for (int i = 0; i < sceneFurniture.Count; i++)
        {
            if (sceneFurniture[i] != null)
            {
                sceneFurniture[i].AutoCollectFeatures();
            }
        }

        return sceneFurniture;
    }

    private static List<MemoryDisplaySlot> GetFurnitureSlots(MemoryDisplayFurniture furniture)
    {
        List<MemoryDisplaySlot> slots = new List<MemoryDisplaySlot>();
        if (furniture == null)
        {
            return slots;
        }

        furniture.AutoCollectFeatures();
        IReadOnlyList<MemoryDisplaySlot> sourceSlots = furniture.Slots;
        for (int i = 0; i < sourceSlots.Count; i++)
        {
            if (sourceSlots[i] != null)
            {
                slots.Add(sourceSlots[i]);
            }
        }

        slots.Sort((left, right) => string.Compare(
            GetResolvedSlotId(left),
            GetResolvedSlotId(right),
            StringComparison.OrdinalIgnoreCase));
        return slots;
    }

    private static MemoryObject FindItemByResolvedId(List<MemoryObject> sceneItems, string itemId)
    {
        string normalizedId = NormalizeId(itemId);
        for (int i = 0; i < sceneItems.Count; i++)
        {
            MemoryObject item = sceneItems[i];
            if (string.Equals(GetResolvedItemId(item), normalizedId, StringComparison.OrdinalIgnoreCase))
            {
                return item;
            }
        }

        return null;
    }

    private static MemoryDisplayFurniture FindFurnitureByResolvedId(
        List<MemoryDisplayFurniture> sceneFurniture,
        string furnitureId)
    {
        string normalizedId = NormalizeId(furnitureId);
        for (int i = 0; i < sceneFurniture.Count; i++)
        {
            MemoryDisplayFurniture furniture = sceneFurniture[i];
            if (string.Equals(GetResolvedFurnitureId(furniture), normalizedId, StringComparison.OrdinalIgnoreCase))
            {
                return furniture;
            }
        }

        return null;
    }

    private static MemoryDisplaySlot FindSlotByResolvedId(
        MemoryDisplayFurniture furniture,
        string slotId)
    {
        List<MemoryDisplaySlot> slots = GetFurnitureSlots(furniture);
        string normalizedId = NormalizeId(slotId);

        for (int i = 0; i < slots.Count; i++)
        {
            MemoryDisplaySlot slot = slots[i];
            if (string.Equals(GetResolvedSlotId(slot), normalizedId, StringComparison.OrdinalIgnoreCase))
            {
                return slot;
            }
        }

        return null;
    }

    private static bool FurnitureContainsSlot(MemoryDisplayFurniture furniture, string slotId)
    {
        return FindSlotByResolvedId(furniture, slotId) != null;
    }

    private static string GetFirstSlotId(MemoryDisplayFurniture furniture)
    {
        List<MemoryDisplaySlot> slots = GetFurnitureSlots(furniture);
        return slots.Count > 0 ? GetResolvedSlotId(slots[0]) : string.Empty;
    }

    private static string GetResolvedItemId(MemoryObject item)
    {
        if (item == null)
        {
            return string.Empty;
        }

        return !string.IsNullOrWhiteSpace(item.ItemId)
            ? item.ItemId.Trim()
            : item.gameObject.name.Trim();
    }

    private static string GetResolvedFurnitureId(MemoryDisplayFurniture furniture)
    {
        if (furniture == null)
        {
            return string.Empty;
        }

        return !string.IsNullOrWhiteSpace(furniture.FurnitureId)
            ? furniture.FurnitureId.Trim()
            : furniture.gameObject.name.Trim();
    }

    private static string GetResolvedSlotId(MemoryDisplaySlot slot)
    {
        if (slot == null)
        {
            return string.Empty;
        }

        return !string.IsNullOrWhiteSpace(slot.SlotId)
            ? slot.SlotId.Trim()
            : slot.gameObject.name.Trim();
    }

    private static string NormalizeId(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static int ComparePlacementRecordsByItemId(
        MemoryItemPlacementRecord left,
        MemoryItemPlacementRecord right)
    {
        string leftId = left != null ? NormalizeId(left.itemId) : string.Empty;
        string rightId = right != null ? NormalizeId(right.itemId) : string.Empty;
        return string.Compare(leftId, rightId, StringComparison.OrdinalIgnoreCase);
    }

    private static void EnsureRecordList(MemoryGardenPlacementLayout layout)
    {
        if (layout != null && layout.records == null)
        {
            layout.records = new List<MemoryItemPlacementRecord>();
        }
    }

    private static void MarkLayoutDirty(MemoryGardenPlacementLayout layout)
    {
        EditorUtility.SetDirty(layout);
    }

    private static void AssignDefaultLayout(
        MemoryGardenPlacementManager manager,
        MemoryGardenPlacementLayout layout)
    {
        if (manager == null || layout == null)
        {
            return;
        }

        SerializedObject serializedManager = new SerializedObject(manager);
        SerializedProperty defaultLayoutProperty = serializedManager.FindProperty("defaultLayout");
        defaultLayoutProperty.objectReferenceValue = layout;
        serializedManager.ApplyModifiedProperties();

        EditorUtility.SetDirty(manager);
        EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
    }
}
