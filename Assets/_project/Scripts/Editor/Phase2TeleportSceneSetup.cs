#if UNITY_EDITOR
using System.IO;
using System.Linq;
using MemoryGarden.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;

namespace MemoryGarden.Editor
{
    public static class Phase2TeleportSceneSetup
    {
        const string ScenePath = "Assets/_project/Scenes/00_Prototype/_02_VR_test_Displayzone.unity";
        const string InputPath = "Assets/Samples/XR Interaction Toolkit/2.6.5/Starter Assets/XRI Default Input Actions.inputactions";
        const string PrefabPath = "Assets/_project/Prefabs/Interaction/TeleportReticle.prefab";
        const string MaterialPath = "Assets/_project/Art/Interactions/MAT_TeleportReticle.mat";

        [MenuItem("Memory Garden/Phase 2/Configure Left-Stick Teleport")]
        public static void ConfigurePhase2()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var xrOrigin = Find("XR Origin (XR Rig)");
            var leftController = Find("Left Controller");
            if (xrOrigin == null || leftController == null)
                throw new System.InvalidOperationException("XR Origin or Left Controller was not found in the reference scene.");

            var locomotionObject = GetOrCreateChild(xrOrigin.transform, "Locomotion System");
            var locomotionSystem = GetOrAdd<LocomotionSystem>(locomotionObject);
            locomotionSystem.xrOrigin = xrOrigin.GetComponent<Unity.XR.CoreUtils.XROrigin>();

            var providerObject = GetOrCreateChild(locomotionObject.transform, "Teleportation Provider");
            var provider = GetOrAdd<TeleportationProvider>(providerObject);
            provider.system = locomotionSystem;

            foreach (var moveProvider in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ContinuousMoveProviderBase>(true)))
                moveProvider.enabled = false;

            var prefab = CreateOrUpdateReticlePrefab();
            var reticle = Find("TeleportReticle");
            if (reticle == null)
            {
                reticle = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                reticle.name = "TeleportReticle";
            }
            reticle.SetActive(false);

            foreach (var floorName in new[] { "LeftFloor", "RightFloor" })
            {
                var floor = Find(floorName);
                if (floor == null || floor.GetComponent<Collider>() == null)
                    throw new System.InvalidOperationException($"Expected collidable floor segment '{floorName}' was not found.");
                GetOrAdd<TeleportSurfaceMarker>(floor);
            }

            var controller = GetOrAdd<LeftStickTeleportController>(leftController);
            controller.rayOrigin = leftController.transform;
            controller.teleportationProvider = provider;
            controller.teleportSurfaceMask = ~0;
            controller.teleportReticle = reticle;
            controller.activationThreshold = 0.2f;
            controller.maxDistance = 10f;
            controller.requireValidSurface = true;
            controller.useFloorSegmentMarker = true;
            controller.leftThumbstickAction = LoadActionReference("XRI LeftHand Locomotion/Move");

            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(provider);
            EditorUtility.SetDirty(locomotionSystem);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Phase 2 left-stick teleport configured successfully.");
        }

        static InputActionReference LoadActionReference(string actionPath)
        {
            var parts = actionPath.Split('/');
            var refs = AssetDatabase.LoadAllAssetsAtPath(InputPath).OfType<InputActionReference>();
            var result = refs.FirstOrDefault(r => r.action != null && r.action.actionMap.name == parts[0] && r.action.name == parts[1]);
            if (result == null)
                throw new System.InvalidOperationException($"Input action reference '{actionPath}' was not found.");
            return result;
        }

        static GameObject CreateOrUpdateReticlePrefab()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            Directory.CreateDirectory(Path.GetDirectoryName(MaterialPath));
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
                material = new Material(shader) { name = "MAT_TeleportReticle" };
                material.color = new Color(0.32f, 0.86f, 0.72f, 0.72f);
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", new Color(0.2f, 1.15f, 0.82f, 1f));
                AssetDatabase.CreateAsset(material, MaterialPath);
            }

            var root = new GameObject("TeleportReticle");
            var line = root.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = 64;
            line.widthMultiplier = 0.025f;
            line.material = material;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            for (var i = 0; i < line.positionCount; i++)
            {
                var angle = i * Mathf.PI * 2f / line.positionCount;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle) * 0.22f, 0f, Mathf.Sin(angle) * 0.22f));
            }
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        static GameObject Find(string name) => Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(o => o.name == name);

        static GameObject GetOrCreateChild(Transform parent, string name)
        {
            var child = parent.Cast<Transform>().FirstOrDefault(t => t.name == name);
            if (child != null) return child.gameObject;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go;
        }

        static T GetOrAdd<T>(GameObject go) where T : Component => go.TryGetComponent<T>(out var value) ? value : go.AddComponent<T>();
    }
}
#endif
