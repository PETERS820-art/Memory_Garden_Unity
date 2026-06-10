#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class MemoryItemPrefabBuilder : EditorWindow
{
    private enum BatchBuildFilter
    {
        All,
        MissingOnly,
        ExistingOnly
    }

    private const string MenuItemPath = "Tools/Memory Garden/Build Memory Item Prefabs";
    private const string ModelContainerObjectName = "Model";

    [MenuItem(MenuItemPath)]
    public static void OpenWindow()
    {
        MemoryItemPrefabBuilder window = GetWindow<MemoryItemPrefabBuilder>("Memory Item Prefab Builder");
        window.minSize = new Vector2(420f, 220f);
        window.Show();
    }

    private void OnGUI()
    {
        string modelRoot = ResolveModelRoot();
        string prefabRoot = ResolveOrCreatePrefabFolder();

        EditorGUILayout.HelpBox(
            "Select one or more Memory Item FBX assets or source folders in the Project window to build only those items.",
            MessageType.Info);

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.TextField("Source Models", string.IsNullOrWhiteSpace(modelRoot) ? "<Missing>" : modelRoot);
            EditorGUILayout.TextField("Prefab Output", prefabRoot);
        }

        EditorGUILayout.Space();

        using (new EditorGUI.DisabledScope(!HasSelectedBatchSource(modelRoot)))
        {
            if (GUILayout.Button("Build Selected Memory Item Prefabs"))
            {
                BuildSelectedMemoryItemPrefabs(modelRoot, prefabRoot);
            }
        }

        if (GUILayout.Button("Build All Memory Item Prefabs"))
        {
            BuildMemoryItemPrefabs(modelRoot, prefabRoot, BatchBuildFilter.MissingOnly);
        }

        if (GUILayout.Button("Rebuild Existing Prefabs"))
        {
            if (ConfirmRebuildExistingPrefabs())
            {
                BuildMemoryItemPrefabs(modelRoot, prefabRoot, BatchBuildFilter.ExistingOnly);
            }
        }
    }

    private static void BuildSelectedMemoryItemPrefabs(string modelRoot, string prefabRoot)
    {
        List<MemoryItemSourceEntry> selectedSources = DiscoverSelectedMemoryItemSources(modelRoot);
        if (selectedSources.Count == 0)
        {
            ShowSummary(
                "Select one or more Memory Item FBX assets or source folders in the Project window.");
            return;
        }

        BuildMemoryItemPrefabs(selectedSources, prefabRoot, BatchBuildFilter.All, "Built selected memory item prefabs");
    }

    private static void BuildMemoryItemPrefabs(string modelRoot, string prefabRoot, BatchBuildFilter filter)
    {
        if (string.IsNullOrWhiteSpace(modelRoot))
        {
            Debug.LogWarning("[MemoryItemPrefabBuilder] Could not find MemoryItems model folder.");
            ShowSummary("No MemoryItems model folder was found.");
            return;
        }

        string modelRootAbsolute = MemoryItemDataAssetBuilder.ToAbsolutePath(modelRoot);
        if (!Directory.Exists(modelRootAbsolute))
        {
            Debug.LogWarning($"[MemoryItemPrefabBuilder] Model folder does not exist on disk: {modelRoot}");
            ShowSummary($"Model folder does not exist on disk: {modelRoot}");
            return;
        }

        List<MemoryItemSourceEntry> sources = DiscoverMemoryItemSources(modelRoot);
        if (sources.Count == 0)
        {
            Debug.LogWarning($"[MemoryItemPrefabBuilder] No FBX files found under {modelRoot}.");
            ShowSummary($"No FBX files found under {modelRoot}.");
            return;
        }

        string summaryPrefix = filter switch
        {
            BatchBuildFilter.MissingOnly => "Built missing memory item prefabs",
            BatchBuildFilter.ExistingOnly => "Rebuilt existing memory item prefabs",
            _ => "Built memory item prefabs"
        };

        BuildMemoryItemPrefabs(sources, prefabRoot, filter, summaryPrefix);
    }

    private static void BuildMemoryItemPrefabs(
        List<MemoryItemSourceEntry> sources,
        string prefabRoot,
        BatchBuildFilter filter,
        string summaryPrefix)
    {
        List<MemoryItemSourceEntry> targets = FilterSources(sources, filter);
        if (targets.Count == 0)
        {
            string message = filter switch
            {
                BatchBuildFilter.MissingOnly => "No missing memory item prefabs were found.",
                BatchBuildFilter.ExistingOnly => "No existing memory item prefabs were found to rebuild.",
                _ => "No memory item sources were selected."
            };

            ShowSummary(message);
            return;
        }

        List<string> generatedPrefabs = new List<string>();

        try
        {
            for (int i = 0; i < targets.Count; i++)
            {
                MemoryItemSourceEntry source = targets[i];
                EditorUtility.DisplayProgressBar(
                    "Memory Item Prefab Builder",
                    $"Building {source.ModelId} ({i + 1}/{targets.Count})",
                    (float)(i + 1) / targets.Count);

                string prefabAssetPath = BuildPrefabForFbx(source.SourceAssetPath, prefabRoot);
                generatedPrefabs.Add(prefabAssetPath);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        catch (Exception exception)
        {
            Debug.LogError($"[MemoryItemPrefabBuilder] Failed while building memory item prefabs: {exception}");
            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog("Memory Item Prefabs", exception.Message, "OK");
            }

            throw;
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        for (int i = 0; i < generatedPrefabs.Count; i++)
        {
            Debug.Log($"[MemoryItemPrefabBuilder] Generated prefab: {generatedPrefabs[i]}");
        }

        ShowSummary($"{summaryPrefix}: {generatedPrefabs.Count} prefab(s).");
    }

    private static string BuildPrefabForFbx(string fbxAssetPath, string prefabRoot)
    {
        GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(fbxAssetPath);
        if (modelAsset == null)
        {
            throw new InvalidOperationException($"Could not load FBX asset at {fbxAssetPath}.");
        }

        string fbxName = Path.GetFileNameWithoutExtension(fbxAssetPath);
        string rootName = $"PF_{fbxName}";
        string prefabAssetPath = $"{prefabRoot}/{rootName}.prefab";

        if (File.Exists(MemoryItemDataAssetBuilder.ToAbsolutePath(prefabAssetPath)))
        {
            Debug.LogWarning($"[MemoryItemPrefabBuilder] Overwriting existing prefab: {prefabAssetPath}");
        }

        GameObject root = new GameObject(rootName);

        try
        {
            GameObject modelContainer = new GameObject(ModelContainerObjectName);
            modelContainer.transform.SetParent(root.transform, false);

            GameObject modelInstance = PrefabUtility.InstantiatePrefab(modelAsset) as GameObject;
            if (modelInstance == null)
            {
                modelInstance = UnityEngine.Object.Instantiate(modelAsset);
            }

            modelInstance.name = modelAsset.name;
            modelInstance.transform.SetParent(modelContainer.transform, false);
            modelInstance.transform.localPosition = Vector3.zero;
            modelInstance.transform.localRotation = Quaternion.identity;
            modelInstance.transform.localScale = Vector3.one;

            ConfigureModelOffset(root, modelContainer.transform);
            ConfigureCollider(root);
            ConfigureRigidbody(root);
            ConfigureGrabInteractable(root);
            ConfigureMemoryObject(root, fbxAssetPath);
            ConfigureRespawn(root);

            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, prefabAssetPath);
            if (savedPrefab == null)
            {
                throw new InvalidOperationException($"Failed to save prefab at {prefabAssetPath}.");
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }

        return prefabAssetPath;
    }

    private static List<MemoryItemSourceEntry> DiscoverMemoryItemSources(string modelRoot)
    {
        List<MemoryItemSourceEntry> sources = new List<MemoryItemSourceEntry>();
        if (string.IsNullOrWhiteSpace(modelRoot) || !AssetDatabase.IsValidFolder(modelRoot))
        {
            return sources;
        }

        string absoluteModelRoot = MemoryItemDataAssetBuilder.ToAbsolutePath(modelRoot);
        if (!Directory.Exists(absoluteModelRoot))
        {
            return sources;
        }

        string[] itemFolders = Directory.GetDirectories(absoluteModelRoot, "MI_*", SearchOption.TopDirectoryOnly);
        Array.Sort(itemFolders, StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < itemFolders.Length; i++)
        {
            if (TryCreateSourceEntryFromFolder(itemFolders[i], out MemoryItemSourceEntry source))
            {
                sources.Add(source);
            }
        }

        string[] rootLevelFbxFiles = Directory.GetFiles(absoluteModelRoot, "*.fbx", SearchOption.TopDirectoryOnly);
        Array.Sort(rootLevelFbxFiles, StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < rootLevelFbxFiles.Length; i++)
        {
            if (TryCreateSourceEntryFromFbx(rootLevelFbxFiles[i], modelRoot, out MemoryItemSourceEntry source))
            {
                sources.Add(source);
            }
        }

        return sources;
    }

    private static List<MemoryItemSourceEntry> DiscoverSelectedMemoryItemSources(string modelRoot)
    {
        List<MemoryItemSourceEntry> allSources = DiscoverMemoryItemSources(modelRoot);
        if (allSources.Count == 0)
        {
            return allSources;
        }

        Dictionary<string, MemoryItemSourceEntry> sourcesByPath =
            new Dictionary<string, MemoryItemSourceEntry>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < allSources.Count; i++)
        {
            sourcesByPath[allSources[i].SourceAssetPath] = allSources[i];
        }

        List<MemoryItemSourceEntry> selectedSources = new List<MemoryItemSourceEntry>();
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
                    MemoryItemSourceEntry source = allSources[sourceIndex];
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
                && sourcesByPath.TryGetValue(assetPath, out MemoryItemSourceEntry selectedSource)
                && seenPaths.Add(selectedSource.SourceAssetPath))
            {
                selectedSources.Add(selectedSource);
            }
        }

        return selectedSources;
    }

    private static List<MemoryItemSourceEntry> FilterSources(
        List<MemoryItemSourceEntry> sources,
        BatchBuildFilter filter)
    {
        if (filter == BatchBuildFilter.All)
        {
            return new List<MemoryItemSourceEntry>(sources);
        }

        List<MemoryItemSourceEntry> filteredSources = new List<MemoryItemSourceEntry>();

        for (int i = 0; i < sources.Count; i++)
        {
            MemoryItemSourceEntry source = sources[i];
            bool prefabExists = File.Exists(MemoryItemDataAssetBuilder.ToAbsolutePath(source.PrefabAssetPath));

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

    private static bool TryCreateSourceEntryFromFolder(
        string folderAbsolutePath,
        out MemoryItemSourceEntry source)
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
                $"[MemoryItemPrefabBuilder] Skipped folder {folderName} because no main FBX could be resolved.");
            return false;
        }

        return TryCreateSourceEntry(
            chosenFbxPath,
            MemoryItemDataAssetBuilder.ToAssetPath(folderAbsolutePath),
            out source);
    }

    private static bool TryCreateSourceEntryFromFbx(
        string fbxAbsolutePath,
        string modelRoot,
        out MemoryItemSourceEntry source)
    {
        source = null;

        if (string.IsNullOrWhiteSpace(fbxAbsolutePath) || !File.Exists(fbxAbsolutePath))
        {
            return false;
        }

        return TryCreateSourceEntry(fbxAbsolutePath, modelRoot, out source);
    }

    private static bool TryCreateSourceEntry(
        string fbxAbsolutePath,
        string sourceFolderAssetPath,
        out MemoryItemSourceEntry source)
    {
        source = null;

        string sourceAssetPath = MemoryItemDataAssetBuilder.ToAssetPath(fbxAbsolutePath);
        if (!sourceAssetPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string modelId = Path.GetFileNameWithoutExtension(sourceAssetPath);
        string prefabAssetPath = $"{ResolveOrCreatePrefabFolder()}/PF_{modelId}.prefab";
        source = new MemoryItemSourceEntry(
            modelId,
            sourceAssetPath,
            NormalizeAssetPath(sourceFolderAssetPath),
            prefabAssetPath);
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

    private static void ConfigureCollider(GameObject root)
    {
        BoxCollider collider = GetOrAddComponent<BoxCollider>(root);
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);

        if (renderers.Length == 0)
        {
            collider.center = Vector3.zero;
            collider.size = Vector3.one * 0.25f;
            return;
        }

        Bounds combinedBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            combinedBounds.Encapsulate(renderers[i].bounds);
        }

        collider.center = root.transform.InverseTransformPoint(combinedBounds.center);

        Vector3 localSize = root.transform.InverseTransformVector(combinedBounds.size);
        collider.size = new Vector3(
            Mathf.Max(Mathf.Abs(localSize.x), 0.01f),
            Mathf.Max(Mathf.Abs(localSize.y), 0.01f),
            Mathf.Max(Mathf.Abs(localSize.z), 0.01f));
    }

    private static void ConfigureRigidbody(GameObject root)
    {
        Rigidbody rigidbody = GetOrAddComponent<Rigidbody>(root);
        rigidbody.useGravity = true;
        rigidbody.isKinematic = false;
    }

    private static void ConfigureGrabInteractable(GameObject root)
    {
        XRGrabInteractable grabInteractable = GetOrAddComponent<XRGrabInteractable>(root);
        grabInteractable.movementType = XRBaseInteractable.MovementType.VelocityTracking;
        grabInteractable.selectMode = InteractableSelectMode.Single;
        grabInteractable.trackPosition = true;
        grabInteractable.trackRotation = true;
        grabInteractable.throwOnDetach = true;
        ConfigureDynamicAttachSettings(grabInteractable);
    }

    private static void ConfigureMemoryObject(GameObject root, string fbxAssetPath)
    {
        MemoryObject memoryObject = GetOrAddComponent<MemoryObject>(root);
        memoryObject.observeRequiredTime = 5f;
        memoryObject.maxObserveAngle = 25f;
        memoryObject.observeAnchor = null;
        memoryObject.useBoundsCenterForObservation = true;
        memoryObject.preferColliderBounds = true;
        MemoryItemDataAssetBuilder.TryAssignExistingDataAsset(memoryObject, fbxAssetPath);
    }

    private static void ConfigureModelOffset(GameObject root, Transform modelRoot)
    {
        MemoryItemModelOffset modelOffset = GetOrAddComponent<MemoryItemModelOffset>(root);
        BoxCollider collider = GetOrAddComponent<BoxCollider>(root);
        modelOffset.SetReferences(modelRoot, collider);
    }

    private static void ConfigureDynamicAttachSettings(XRGrabInteractable grabInteractable)
    {
        SerializedObject serializedObject = new SerializedObject(grabInteractable);
        serializedObject.UpdateIfRequiredOrScript();

        bool useDynamicAttachApplied = TrySetSerializedBool(serializedObject, "m_UseDynamicAttach", true);
        bool matchAttachPositionApplied = TrySetSerializedBool(serializedObject, "m_MatchAttachPosition", true);
        bool matchAttachRotationApplied = TrySetSerializedBool(serializedObject, "m_MatchAttachRotation", false);
        bool snapToColliderVolumeApplied = TrySetSerializedBool(serializedObject, "m_SnapToColliderVolume", true);

        if (serializedObject.hasModifiedProperties)
        {
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        if (!useDynamicAttachApplied)
        {
            grabInteractable.useDynamicAttach = true;
        }

        if (!matchAttachPositionApplied)
        {
            grabInteractable.matchAttachPosition = true;
        }

        if (!matchAttachRotationApplied)
        {
            grabInteractable.matchAttachRotation = false;
        }

        if (!snapToColliderVolumeApplied)
        {
            grabInteractable.snapToColliderVolume = true;
        }
    }

    private static void ConfigureRespawn(GameObject root)
    {
        MemoryItemRespawn respawn = GetOrAddComponent<MemoryItemRespawn>(root);
        SerializedObject serializedObject = new SerializedObject(respawn);
        serializedObject.UpdateIfRequiredOrScript();

        TrySetSerializedFloat(serializedObject, "maxDistanceFromStart", 100f);
        TrySetSerializedFloat(serializedObject, "minY", -10f);
        TrySetSerializedFloat(serializedObject, "checkInterval", 0.5f);
        TrySetSerializedFloat(serializedObject, "respawnHeightOffset", 0.3f);
        TrySetSerializedBool(serializedObject, "resetWhenSelected", false);
        TrySetSerializedBool(serializedObject, "resetOnLowYEvenWhenSelected", true);

        if (serializedObject.hasModifiedProperties)
        {
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static bool TrySetSerializedBool(SerializedObject serializedObject, string propertyName, bool value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null || property.propertyType != SerializedPropertyType.Boolean)
        {
            return false;
        }

        property.boolValue = value;
        return true;
    }

    private static bool TrySetSerializedFloat(SerializedObject serializedObject, string propertyName, float value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null || property.propertyType != SerializedPropertyType.Float)
        {
            return false;
        }

        property.floatValue = value;
        return true;
    }

    private static T GetOrAddComponent<T>(GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();
        if (component == null)
        {
            component = gameObject.AddComponent<T>();
        }

        if (component == null)
        {
            throw new InvalidOperationException(
                $"Failed to get or add component {typeof(T).Name} on {gameObject.name}.");
        }

        return component;
    }

    private static string ResolveModelRoot()
    {
        return MemoryItemDataAssetBuilder.ResolveExistingFolder(MemoryItemDataAssetBuilder.ModelRootCandidates);
    }

    private static string ResolveOrCreatePrefabFolder()
    {
        string[] candidates = MemoryItemDataAssetBuilder.PrefabRootCandidates;
        string existing = MemoryItemDataAssetBuilder.ResolveExistingFolder(candidates);
        if (!string.IsNullOrWhiteSpace(existing))
        {
            return existing;
        }

        if (candidates.Length == 0)
        {
            throw new InvalidOperationException("No asset folder candidates were provided.");
        }

        MemoryItemDataAssetBuilder.EnsureFolder(candidates[candidates.Length - 1]);
        return candidates[candidates.Length - 1];
    }

    private static bool HasSelectedBatchSource(string modelRoot)
    {
        if (string.IsNullOrWhiteSpace(modelRoot) || Selection.objects == null || Selection.objects.Length == 0)
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

            if (AssetDatabase.IsValidFolder(assetPath) && IsPathUnderFolder(assetPath, modelRoot))
            {
                return true;
            }

            if (assetPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)
                && IsPathUnderFolder(assetPath, modelRoot))
            {
                return true;
            }
        }

        return false;
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

    private static string NormalizeAssetPath(string path)
    {
        return string.IsNullOrWhiteSpace(path) ? string.Empty : path.Replace("\\", "/");
    }

    private static bool ConfirmRebuildExistingPrefabs()
    {
        if (Application.isBatchMode)
        {
            return true;
        }

        return EditorUtility.DisplayDialog(
            "Rebuild Memory Item Prefabs",
            "Rebuild will overwrite existing memory item prefabs. Any manual prefab adjustments may be lost.\n\nDo you want to continue?",
            "Rebuild",
            "Cancel");
    }

    private static void ShowSummary(string message)
    {
        Debug.Log($"[MemoryItemPrefabBuilder] {message}");

        if (!Application.isBatchMode)
        {
            EditorUtility.DisplayDialog("Memory Item Prefabs", message, "OK");
        }
    }

    private sealed class MemoryItemSourceEntry
    {
        public MemoryItemSourceEntry(
            string modelId,
            string sourceAssetPath,
            string sourceFolderAssetPath,
            string prefabAssetPath)
        {
            ModelId = modelId;
            SourceAssetPath = sourceAssetPath;
            SourceFolderAssetPath = sourceFolderAssetPath;
            PrefabAssetPath = prefabAssetPath;
        }

        public string ModelId { get; }
        public string SourceAssetPath { get; }
        public string SourceFolderAssetPath { get; }
        public string PrefabAssetPath { get; }
    }
}
#endif
