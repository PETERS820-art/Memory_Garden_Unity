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

    [MenuItem(MenuItemPath)]
    public static void BuildMemoryItemPrefabs()
    {
        var modelRoot = MemoryItemDataAssetBuilder.ResolveExistingFolder(MemoryItemDataAssetBuilder.ModelRootCandidates);
        var prefabRoot = ResolveOrCreatePrefabFolder();

        if (string.IsNullOrWhiteSpace(modelRoot))
        {
            Debug.LogWarning("[MemoryItemPrefabBuilder] Could not find MemoryItems model folder.");
            ShowSummary("No MemoryItems model folder was found.");
            return;
        }

        var modelRootAbsolute = MemoryItemDataAssetBuilder.ToAbsolutePath(modelRoot);
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
                var fbxAssetPath = MemoryItemDataAssetBuilder.ToAssetPath(fbxAbsolutePath);
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

        if (File.Exists(MemoryItemDataAssetBuilder.ToAbsolutePath(prefabAssetPath)))
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

            ConfigureModelOffset(root, modelContainer.transform);
            ConfigureCollider(root);
            ConfigureRigidbody(root);
            ConfigureGrabInteractable(root);
            ConfigureMemoryObject(root, fbxAssetPath);
            ConfigureRespawn(root);

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
        ConfigureDynamicAttachSettings(grabInteractable);
    }

    private static void ConfigureMemoryObject(GameObject root, string fbxAssetPath)
    {
        var memoryObject = GetOrAddComponent<MemoryObject>(root);
        memoryObject.observeRequiredTime = 5f;
        memoryObject.maxObserveAngle = 25f;
        memoryObject.observeAnchor = null;
        memoryObject.useBoundsCenterForObservation = true;
        memoryObject.preferColliderBounds = true;
        MemoryItemDataAssetBuilder.TryAssignExistingDataAsset(memoryObject, fbxAssetPath);
    }

    private static void ConfigureModelOffset(GameObject root, Transform modelRoot)
    {
        var modelOffset = GetOrAddComponent<MemoryItemModelOffset>(root);
        var collider = GetOrAddComponent<BoxCollider>(root);
        modelOffset.SetReferences(modelRoot, collider);
    }

    private static void ConfigureDynamicAttachSettings(XRGrabInteractable grabInteractable)
    {
        var serializedObject = new SerializedObject(grabInteractable);
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
        var respawn = GetOrAddComponent<MemoryItemRespawn>(root);
        var serializedObject = new SerializedObject(respawn);
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
        var property = serializedObject.FindProperty(propertyName);
        if (property == null || property.propertyType != SerializedPropertyType.Boolean)
        {
            return false;
        }

        property.boolValue = value;
        return true;
    }

    private static bool TrySetSerializedFloat(SerializedObject serializedObject, string propertyName, float value)
    {
        var property = serializedObject.FindProperty(propertyName);
        if (property == null || property.propertyType != SerializedPropertyType.Float)
        {
            return false;
        }

        property.floatValue = value;
        return true;
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

    private static string ResolveOrCreatePrefabFolder()
    {
        var candidates = MemoryItemDataAssetBuilder.PrefabRootCandidates;
        var existing = MemoryItemDataAssetBuilder.ResolveExistingFolder(candidates);
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
