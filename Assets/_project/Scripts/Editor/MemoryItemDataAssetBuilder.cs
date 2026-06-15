#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class MemoryItemDataAssetBuilder
{
    private const string MenuItemPath = "Tools/Memory Garden/Build Memory Item Data Assets";
    internal static readonly string[] ModelRootCandidates =
    {
        "Assets/_Project/Art/Models/MemoryItems",
        "Assets/_project/Art/Models/MemoryItems",
    };

    internal static readonly string[] PrefabRootCandidates =
    {
        "Assets/_Project/Prefabs/MemoryItems",
        "Assets/_project/Prefabs/MemoryItems",
    };

    [MenuItem(MenuItemPath)]
    public static void BuildMemoryItemDataAssets()
    {
        var modelRoot = ResolveExistingFolder(ModelRootCandidates);
        var prefabRoot = ResolveExistingFolder(PrefabRootCandidates);

        if (string.IsNullOrWhiteSpace(modelRoot))
        {
            ShowSummary("No MemoryItems model folder was found.");
            return;
        }

        var modelRootAbsolute = ToAbsolutePath(modelRoot);
        if (!Directory.Exists(modelRootAbsolute))
        {
            ShowSummary($"Model folder does not exist on disk: {modelRoot}");
            return;
        }

        var fbxFiles = Directory.GetFiles(modelRootAbsolute, "*.fbx", SearchOption.AllDirectories);
        Array.Sort(fbxFiles, StringComparer.OrdinalIgnoreCase);

        if (fbxFiles.Length == 0)
        {
            ShowSummary($"No FBX files found under {modelRoot}.");
            return;
        }

        var generatedDataAssets = new List<string>();
        var updatedPrefabs = new List<string>();

        foreach (var fbxAbsolutePath in fbxFiles)
        {
            var fbxAssetPath = ToAssetPath(fbxAbsolutePath);
            var dataAsset = GetOrCreateDataAssetForFbx(fbxAssetPath, out var dataAssetPath);
            generatedDataAssets.Add(dataAssetPath);

            if (TryAssignDataAssetToMatchingPrefab(prefabRoot, fbxAssetPath, dataAsset, out var prefabPath))
            {
                updatedPrefabs.Add(prefabPath);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        foreach (var dataAssetPath in generatedDataAssets)
        {
            Debug.Log($"[MemoryItemDataAssetBuilder] Ready data asset: {dataAssetPath}");
        }

        foreach (var prefabPath in updatedPrefabs)
        {
            Debug.Log($"[MemoryItemDataAssetBuilder] Assigned MemoryItemData on prefab: {prefabPath}");
        }

        ShowSummary(
            $"Prepared {generatedDataAssets.Count} MemoryItemData asset(s) and updated {updatedPrefabs.Count} prefab(s).");
    }

    internal static bool TryAssignExistingDataAsset(MemoryObject memoryObject, string fbxAssetPath)
    {
        if (memoryObject == null || string.IsNullOrWhiteSpace(fbxAssetPath))
        {
            return false;
        }

        var dataAsset = FindExistingDataAssetForFbx(fbxAssetPath);
        if (dataAsset == null)
        {
            return false;
        }

        return AssignDataAsset(memoryObject, dataAsset);
    }

    private static MemoryItemData GetOrCreateDataAssetForFbx(string fbxAssetPath, out string dataAssetPath)
    {
        dataAssetPath = GetDataAssetPathForFbx(fbxAssetPath);

        var existing = AssetDatabase.LoadAssetAtPath<MemoryItemData>(dataAssetPath);
        if (existing != null)
        {
            return existing;
        }

        var dataFolderPath = Path.GetDirectoryName(dataAssetPath)?.Replace("\\", "/");
        if (string.IsNullOrWhiteSpace(dataFolderPath))
        {
            throw new InvalidOperationException($"Could not resolve data folder for {fbxAssetPath}.");
        }

        EnsureFolder(dataFolderPath);

        var dataAsset = ScriptableObject.CreateInstance<MemoryItemData>();
        AssetDatabase.CreateAsset(dataAsset, dataAssetPath);

        var itemId = Path.GetFileNameWithoutExtension(fbxAssetPath);
        var serializedObject = new SerializedObject(dataAsset);
        serializedObject.FindProperty("itemId").stringValue = itemId;
        serializedObject.FindProperty("itemName").stringValue = itemId;
        serializedObject.FindProperty("shortDescription").stringValue = "Imported memory item.";
        serializedObject.FindProperty("emotionType").stringValue = EmotionMaterialLogEditorUtility.GetDefaultEmotionType();
        serializedObject.FindProperty("storyText").stringValue = string.Empty;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(dataAsset);
        return dataAsset;
    }

    private static MemoryItemData FindExistingDataAssetForFbx(string fbxAssetPath)
    {
        var dataAssetPath = GetDataAssetPathForFbx(fbxAssetPath);
        return AssetDatabase.LoadAssetAtPath<MemoryItemData>(dataAssetPath);
    }

    private static string GetDataAssetPathForFbx(string fbxAssetPath)
    {
        var itemId = Path.GetFileNameWithoutExtension(fbxAssetPath);
        var modelFolderPath = Path.GetDirectoryName(fbxAssetPath)?.Replace("\\", "/");

        if (string.IsNullOrWhiteSpace(modelFolderPath))
        {
            throw new InvalidOperationException($"Could not resolve model folder for {fbxAssetPath}.");
        }

        return $"{modelFolderPath}/Data/{itemId}_Data.asset";
    }

    private static bool TryAssignDataAssetToMatchingPrefab(
        string prefabRoot,
        string fbxAssetPath,
        MemoryItemData dataAsset,
        out string prefabAssetPath)
    {
        prefabAssetPath = null;

        if (string.IsNullOrWhiteSpace(prefabRoot) || dataAsset == null)
        {
            return false;
        }

        var itemId = Path.GetFileNameWithoutExtension(fbxAssetPath);
        prefabAssetPath = $"{prefabRoot}/PF_{itemId}.prefab";

        if (!File.Exists(ToAbsolutePath(prefabAssetPath)))
        {
            Debug.LogWarning($"[MemoryItemDataAssetBuilder] Matching prefab not found for {itemId}: {prefabAssetPath}");
            return false;
        }

        var prefabRootObject = PrefabUtility.LoadPrefabContents(prefabAssetPath);
        try
        {
            var memoryObject = prefabRootObject.GetComponent<MemoryObject>();
            if (memoryObject == null)
            {
                Debug.LogWarning($"[MemoryItemDataAssetBuilder] No MemoryObject found on prefab {prefabAssetPath}");
                return false;
            }

            if (!AssignDataAsset(memoryObject, dataAsset))
            {
                return false;
            }

            PrefabUtility.SaveAsPrefabAsset(prefabRootObject, prefabAssetPath);
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRootObject);
        }
    }

    private static bool AssignDataAsset(MemoryObject memoryObject, MemoryItemData dataAsset)
    {
        var serializedObject = new SerializedObject(memoryObject);
        var memoryItemDataProperty = serializedObject.FindProperty("memoryItemData");
        if (memoryItemDataProperty == null)
        {
            Debug.LogWarning($"[MemoryItemDataAssetBuilder] Could not find memoryItemData field on {memoryObject.name}");
            return false;
        }

        if (memoryItemDataProperty.objectReferenceValue == dataAsset)
        {
            return true;
        }

        memoryItemDataProperty.objectReferenceValue = dataAsset;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(memoryObject);
        return true;
    }

    internal static string ResolveExistingFolder(IEnumerable<string> candidates)
    {
        foreach (var candidate in candidates)
        {
            if (AssetDatabase.IsValidFolder(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    internal static void EnsureFolder(string assetPath)
    {
        if (AssetDatabase.IsValidFolder(assetPath))
        {
            return;
        }

        var parentPath = Path.GetDirectoryName(assetPath)?.Replace("\\", "/");
        var folderName = Path.GetFileName(assetPath);

        if (string.IsNullOrWhiteSpace(parentPath) || string.IsNullOrWhiteSpace(folderName))
        {
            throw new InvalidOperationException($"Could not create folder: {assetPath}");
        }

        EnsureFolder(parentPath);
        AssetDatabase.CreateFolder(parentPath, folderName);
    }

    internal static string ToAbsolutePath(string assetPath)
    {
        var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
    }

    internal static string ToAssetPath(string absolutePath)
    {
        var normalizedAbsolute = absolutePath.Replace("\\", "/");
        var normalizedDataPath = Application.dataPath.Replace("\\", "/");

        if (!normalizedAbsolute.StartsWith(normalizedDataPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Path is outside the Unity Assets folder: {absolutePath}");
        }

        return $"Assets{normalizedAbsolute.Substring(normalizedDataPath.Length)}";
    }

    private static void ShowSummary(string message)
    {
        Debug.Log($"[MemoryItemDataAssetBuilder] {message}");

        if (!Application.isBatchMode)
        {
            EditorUtility.DisplayDialog("Memory Item Data", message, "OK");
        }
    }
}
#endif
