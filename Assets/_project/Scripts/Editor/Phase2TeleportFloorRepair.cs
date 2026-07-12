#if UNITY_EDITOR
using MemoryGarden.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MemoryGarden.Editor
{
    public static class Phase2TeleportFloorRepair
    {
        const string ScenePath = "Assets/_project/Scenes/00_Prototype/_02_VR_test_Displayzone.unity";

        static readonly string[] FloorPrefabPaths =
        {
            "Assets/_project/Prefabs/SegmentKit/Floor/SM_floor_white_001/PF_SM_floor_white_001_1X1.prefab",
            "Assets/_project/Prefabs/SegmentKit/Floor/SM_floor_white_001/PF_SM_floor_white_001_1X2.prefab",
            "Assets/_project/Prefabs/SegmentKit/Floor/SM_floor_white_001/PF_SM_floor_white_001_2X2.prefab"
        };

        [MenuItem("Memory Garden/Phase 2/Repair Palace Floor Teleport Markers")]
        public static void Repair()
        {
            foreach (var path in FloorPrefabPaths)
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (root.GetComponent<Collider>() == null)
                        throw new System.InvalidOperationException($"Floor segment has no root collider: {path}");

                    if (root.GetComponent<TeleportSurfaceMarker>() == null)
                        root.AddComponent<TeleportSurfaceMarker>();

                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var marker in root.GetComponentsInChildren<TeleportSurfaceMarker>(true))
                {
                    if (marker.name == "LeftFloor" || marker.name == "RightFloor")
                        Object.DestroyImmediate(marker);
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Phase 2 teleport floor markers repaired: SegmentKit 1X1/1X2/2X2 marked; LeftFloor/RightFloor unmarked.");
        }
    }
}
#endif
