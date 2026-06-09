#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public static class MemoryItemPrefabBuilder
{
    private const string MenuItemPath = "Tools/Memory Garden/Build Memory Item Prefabs";
    private static readonly string[] ModelRootCandidates =
    {
        "Assets/_Project/Art/Models/MemoryItems",
        "Assets/_project/Art/Models/MemoryItems",
    };

    private static readonly string[] PrefabRootCandidates =
    {
        "Assets/_Project/Prefabs/MemoryItems",
        "Assets/_project/Prefabs/MemoryItems",
    };

    [MenuItem(MenuItemPath)]
    public static void BuildMemoryItemPrefabs()
    {
        var modelRoot = ResolveExistingFolder(ModelRootCandidates);
        var prefabRoot = ResolveOrCreateFolder(PrefabRootCandidates);

        if (string.IsNullOrWhiteSpace(modelRoot))
        {
            Debug.LogWarning("[MemoryItemPrefabBuilder] Could not find MemoryItems model folder.");
            ShowSummary("No MemoryItems model folder was found.");
            return;
        }

        var modelRootAbsolute = ToAbsolutePath(modelRoot);
        if (!Directory.Exists(modelRootAbsolute))
        {
            Debug.LogWarning($"[MemoryItemPrefabBuilder] Model folder does not exist on disk: {modelRoot}");
            ShowSummary($"Model folder does not exist on disk: {modelRoot}");
            return;
        }

        var fbxFiles = Directory.GetFiles(modelRootAbsolute, "*.fbx", SearchOption.AllDirectories);
        Array.Sort(fbxFiles, StringComparer.OrdinalIgnoreCase);

        if (fbxFiles.Length == 0)
        {
            Debug.LogWarning($"[MemoryItemPrefabBuilder] No FBX files found under {modelRoot}.");
            ShowSummary($"No FBX files found under {modelRoot}.");
            return;
        }

        var generatedPrefabs = new List<string>();

        try
        {
            foreach (var fbxAbsolutePath in fbxFiles)
            {
                var fbxAssetPath = ToAssetPath(fbxAbsolutePath);
                var prefabAssetPath = BuildPrefabForFbx(fbxAssetPath, prefabRoot);
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

        foreach (var prefabPath in generatedPrefabs)
        {
            Debug.Log($"[MemoryItemPrefabBuilder] Generated prefab: {prefabPath}");
        }

        ShowSummary($"Generated {generatedPrefabs.Count} memory item prefab(s).");
    }

    private static string BuildPrefabForFbx(string fbxAssetPath, string prefabRoot)
    {
        var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(fbxAssetPath);
        if (modelAsset == null)
        {
            throw new InvalidOperationException($"Could not load FBX asset at {fbxAssetPath}.");
        }

        var fbxName = Path.GetFileNameWithoutExtension(fbxAssetPath);
        var rootName = $"PF_{fbxName}";
        var prefabAssetPath = $"{prefabRoot}/{rootName}.prefab";

        if (File.Exists(ToAbsolutePath(prefabAssetPath)))
        {
            Debug.LogWarning($"[MemoryItemPrefabBuilder] Overwriting existing prefab: {prefabAssetPath}");
        }

        var root = new GameObject(rootName);

        try
        {
            var modelContainer = new GameObject("Model");
            modelContainer.transform.SetParent(root.transform, false);

            var modelInstance = PrefabUtility.InstantiatePrefab(modelAsset) as GameObject;
            if (modelInstance == null)
            {
                modelInstance = UnityEngine.Object.Instantiate(modelAsset);
            }

            modelInstance.name = modelAsset.name;
            modelInstance.transform.SetParent(modelContainer.transform, false);
            modelInstance.transform.localPosition = Vector3.zero;
            modelInstance.transform.localRotation = Quaternion.identity;
            modelInstance.transform.localScale = Vector3.one;

            ConfigureCollider(root);
            ConfigureRigidbody(root);
            ConfigureGrabInteractable(root);
            ConfigureMemoryObject(root, fbxName);

            var savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, prefabAssetPath);
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

    private static void ConfigureCollider(GameObject root)
    {
        var collider = GetOrAddComponent<BoxCollider>(root);
        var renderers = root.GetComponentsInChildren<Renderer>(true);

        if (renderers.Length == 0)
        {
            collider.center = Vector3.zero;
            collider.size = Vector3.one * 0.25f;
            return;
        }

        var combinedBounds = renderers[0].bounds;
        for (var i = 1; i < renderers.Length; i++)
        {
            combinedBounds.Encapsulate(renderers[i].bounds);
        }

        collider.center = root.transform.InverseTransformPoint(combinedBounds.center);

        var localSize = root.transform.InverseTransformVector(combinedBounds.size);
        collider.size = new Vector3(
            Mathf.Max(Mathf.Abs(localSize.x), 0.01f),
            Mathf.Max(Mathf.Abs(localSize.y), 0.01f),
            Mathf.Max(Mathf.Abs(localSize.z), 0.01f));
    }

    private static void ConfigureRigidbody(GameObject root)
    {
        var rigidbody = GetOrAddComponent<Rigidbody>(root);
        rigidbody.useGravity = true;
        rigidbody.isKinematic = false;
    }

    private static void ConfigureGrabInteractable(GameObject root)
    {
        var grabInteractable = GetOrAddComponent<XRGrabInteractable>(root);
        grabInteractable.movementType = XRBaseInteractable.MovementType.VelocityTracking;
        grabInteractable.selectMode = InteractableSelectMode.Single;
        grabInteractable.trackPosition = true;
        grabInteractable.trackRotation = true;
        grabInteractable.throwOnDetach = true;
    }

    private static void ConfigureMemoryObject(GameObject root, string fbxName)
    {
        var memoryObject = GetOrAddComponent<MemoryObject>(root);
        memoryObject.itemId = fbxName;
        memoryObject.itemName = fbxName;
        memoryObject.shortDescription = "Imported memory item.";
        memoryObject.emotionType = "neutral";
        memoryObject.observeRequiredTime = 5f;
        memoryObject.maxObserveAngle = 25f;
    }

    private static T GetOrAddComponent<T>(GameObject gameObject) where T : Component
    {
        var component = gameObject.GetComponent<T>();
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

    private static string ResolveExistingFolder(IEnumerable<string> candidates)
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

    private static string ResolveOrCreateFolder(IReadOnlyList<string> candidates)
    {
        var existing = ResolveExistingFolder(candidates);
        if (!string.IsNullOrWhiteSpace(existing))
        {
            return existing;
        }

        if (candidates.Count == 0)
        {
            throw new InvalidOperationException("No asset folder candidates were provided.");
        }

        EnsureFolder(candidates[candidates.Count - 1]);
        return candidates[candidates.Count - 1];
    }

    private static void EnsureFolder(string assetPath)
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

    private static string ToAbsolutePath(string assetPath)
    {
        var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
    }

    private static string ToAssetPath(string absolutePath)
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
        Debug.Log($"[MemoryItemPrefabBuilder] {message}");

        if (!Application.isBatchMode)
        {
            EditorUtility.DisplayDialog("Memory Item Prefabs", message, "OK");
        }
    }
}
#endif
