#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class ModelLightmapUvTools
{
    private const string EnableSceneReferencedMenuPath = "Memory Garden/Models/Enable Lightmap UVs For Scene Referenced Models";
    private const string EnableSelectedMenuPath = "Memory Garden/Models/Enable Lightmap UVs For Selected Models";

    [MenuItem(EnableSceneReferencedMenuPath)]
    public static void EnableLightmapUvsForSceneReferencedModels()
    {
        HashSet<string> modelGuids = new HashSet<string>();
        string[] sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/_project/Scenes" });

        foreach (string sceneGuid in sceneGuids)
        {
            string scenePath = AssetDatabase.GUIDToAssetPath(sceneGuid);
            foreach (string dependencyPath in AssetDatabase.GetDependencies(scenePath, true))
            {
                if (AssetImporter.GetAtPath(dependencyPath) is not ModelImporter)
                {
                    continue;
                }

                string modelGuid = AssetDatabase.AssetPathToGUID(dependencyPath);
                if (!string.IsNullOrEmpty(modelGuid))
                {
                    modelGuids.Add(modelGuid);
                }
            }
        }

        ApplyGenerateLightmapUvs(modelGuids, "scene referenced models");
    }

    [MenuItem(EnableSelectedMenuPath)]
    public static void EnableLightmapUvsForSelectedModels()
    {
        HashSet<string> modelGuids = new HashSet<string>();

        foreach (string guid in Selection.assetGUIDs)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (AssetDatabase.IsValidFolder(assetPath))
            {
                foreach (string modelGuid in AssetDatabase.FindAssets("t:Model", new[] { assetPath }))
                {
                    modelGuids.Add(modelGuid);
                }
                continue;
            }

            if (AssetImporter.GetAtPath(assetPath) is ModelImporter)
            {
                modelGuids.Add(guid);
            }
        }

        ApplyGenerateLightmapUvs(modelGuids, "selected models");
    }

    [MenuItem(EnableSelectedMenuPath, true)]
    private static bool ValidateEnableLightmapUvsForSelectedModels()
    {
        return Selection.assetGUIDs != null && Selection.assetGUIDs.Length > 0;
    }

    private static void ApplyGenerateLightmapUvs(IEnumerable<string> modelGuids, string scopeLabel)
    {
        int updatedCount = 0;
        int alreadyEnabledCount = 0;
        int inspectedCount = 0;

        foreach (string guid in modelGuids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (AssetImporter.GetAtPath(assetPath) is not ModelImporter importer)
            {
                continue;
            }

            inspectedCount++;
            if (importer.generateSecondaryUV)
            {
                alreadyEnabledCount++;
                continue;
            }

            importer.generateSecondaryUV = true;
            importer.SaveAndReimport();
            updatedCount++;
        }

        AssetDatabase.Refresh();

        string message =
            $"Inspected {inspectedCount} {scopeLabel}.\n" +
            $"Enabled Lightmap UVs on {updatedCount} models.\n" +
            $"{alreadyEnabledCount} models were already enabled.";

        Debug.Log($"[ModelLightmapUvTools] {message}");
        EditorUtility.DisplayDialog("Lightmap UVs Updated", message, "OK");
    }
}
#endif
