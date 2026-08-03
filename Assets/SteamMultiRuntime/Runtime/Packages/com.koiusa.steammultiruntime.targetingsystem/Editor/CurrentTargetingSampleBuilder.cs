using System.IO;
using Koiusa.TargetingSystem.Runtime;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Koiusa.SteamMultiRuntime.TargetingSystem.Editor
{
    public static class CurrentTargetingSampleBuilder
    {
        private const string GenericPackageRoot = "Assets/SteamMultiRuntime/Runtime/Packages/com.koiusa.targetingsystem";
        private const string SampleRoot = "Assets/SteamMultiRuntime/Runtime/Packages/com.koiusa.steammultiruntime.targetingsystem/Samples~/Showcase";
        private const string TemporaryRoot = "Assets/__TargetingSampleAuthoring";
        private const string TargetingCameraPrefabPath =
            "Assets/SteamMultiRuntime/Runtime/Prefabs/Camera/Targeting Camera System.prefab";

        [MenuItem("Tools/SteamMultiRuntime/Maintenance/Targeting/Rebuild Showcase Sample")]
        public static void Rebuild()
        {
            var previousSceneSetup = EditorSceneManager.GetSceneManagerSetup();
            var hasLoadedScene = HasLoadedScene(previousSceneSetup);
            AssetDatabase.DeleteAsset(TemporaryRoot);
            Directory.CreateDirectory(Path.GetFullPath(TemporaryRoot));
            Directory.CreateDirectory(Path.GetFullPath(SampleRoot));
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            try
            {
                BuildScene(hasLoadedScene ? NewSceneMode.Additive : NewSceneMode.Single);
            }
            finally
            {
                if (hasLoadedScene)
                {
                    EditorSceneManager.RestoreSceneManagerSetup(previousSceneSetup);
                }
                AssetDatabase.DeleteAsset(TemporaryRoot);
                AssetDatabase.Refresh();
            }
        }

        private static bool HasLoadedScene(SceneSetup[] setup)
        {
            foreach (var scene in setup)
            {
                if (scene.isLoaded)
                {
                    return true;
                }
            }

            return false;
        }

        private static void BuildScene(NewSceneMode mode)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, mode);
            SceneManager.SetActiveScene(scene);
            var camera = CreateCamera();
            CreateLight();
            CreateGround();

            var registryObject = new GameObject("Target Registry");
            registryObject.AddComponent<TargetMarkerRegistry>();

            var player = new GameObject("Player Targeting Controller");
            player.name = "Player Targeting Controller";
            player.transform.position = new Vector3(0f, 1f, 0f);

            var playerVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            playerVisual.name = "Player Visual";
            playerVisual.transform.SetParent(player.transform, false);
            playerVisual.transform.localScale = new Vector3(1f, 2f, 1f);
            UnityEngine.Object.DestroyImmediate(playerVisual.GetComponent<Collider>());

            var forwardMarker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            forwardMarker.name = "Forward Marker";
            forwardMarker.transform.SetParent(player.transform, false);
            forwardMarker.transform.localPosition = new Vector3(0f, 0f, 0.65f);
            forwardMarker.transform.localScale = new Vector3(0.35f, 0.3f, 0.45f);
            UnityEngine.Object.DestroyImmediate(forwardMarker.GetComponent<Collider>());
            var characterController = player.AddComponent<CharacterController>();
            characterController.height = 2f;
            characterController.radius = 0.5f;
            var context = player.AddComponent<TargetingContextProvider>();
            var candidates = player.AddComponent<RegistryTargetCandidateSource>();
            var policy = player.AddComponent<ViewportTargetPolicy>();
            var controller = player.AddComponent<TargetingController>();
            var input = player.AddComponent<TargetingCommandInput>();

            SetObjectReference(context, "viewCamera", camera);
            SetObjectReference(controller, "contextSource", context);
            SetObjectReference(controller, "candidateSource", candidates);
            SetObjectArray(controller, "filters", policy);
            SetObjectArray(controller, "scorers", policy);
            SetObjectReference(input, "controller", controller);
            SetObjectReference(input, "inputActions", AssetDatabase.LoadAssetAtPath<TargetingInputActions>(
                "Assets/SteamMultiRuntime/Runtime/Configs/Input/GameplayTargetingInputActions.asset"));
            var playerMover = player.AddComponent<TargetingSamplePlayerMover>();
            SetObjectReference(playerMover, "inputActionsConfig", AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                "Assets/SteamMultiRuntime/Runtime/Configs/Input/GameplayInputActionsConfig.asset"));
            SetObjectReference(playerMover, "viewCamera", camera);

            var cameraAimObject = new GameObject("Camera Aim");
            cameraAimObject.transform.SetParent(player.transform, false);
            cameraAimObject.transform.localPosition = new Vector3(0f, 0.25f, 0f);

            CreateRandomTargetSpawner(registryObject.GetComponent<TargetMarkerRegistry>());
            CreateIndicator(controller, camera);
            CreateCinemachineRig(player.transform, cameraAimObject.transform, controller);

            var temporaryScenePath = TemporaryRoot + "/TargetingSystem.unity";
            EditorSceneManager.SaveScene(scene, temporaryScenePath);
            NormalizeUnityYaml(temporaryScenePath);
            File.Copy(Path.GetFullPath(temporaryScenePath), Path.GetFullPath(SampleRoot + "/TargetingSystem.unity"), true);
            File.Copy(Path.GetFullPath(temporaryScenePath + ".meta"), Path.GetFullPath(SampleRoot + "/TargetingSystem.unity.meta"), true);
            Debug.Log($"Current Targeting sample rebuilt: {SampleRoot}/TargetingSystem.unity");
        }

        private static Camera CreateCamera()
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetPositionAndRotation(new Vector3(0f, 3f, -8f), Quaternion.Euler(8f, 0f, 0f));
            var camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<CinemachineBrain>();
            return camera;
        }

        private static void CreateCinemachineRig(
            Transform player,
            Transform playerAim,
            TargetingController controller)
        {
            var cameraSystem = new GameObject("Camera System");
            var rigObject = new GameObject("Camera Mixer");
            rigObject.transform.SetParent(cameraSystem.transform, false);
            var targetingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TargetingCameraPrefabPath);
            if (targetingPrefab == null)
                throw new FileNotFoundException("Targeting camera prefab was not found.", TargetingCameraPrefabPath);
            var targetingSystem = (GameObject)PrefabUtility.InstantiatePrefab(targetingPrefab);
            targetingSystem.transform.SetParent(cameraSystem.transform, false);
            var mixer = rigObject.AddComponent<CinemachineMixingCamera>();
            var freeCamera = CreateVirtualCamera(rigObject.transform, "Free Camera", player, playerAim);
            var singleCamera = CreateVirtualCamera(rigObject.transform, "Single Target Camera", player, playerAim);
            var multiCamera = CreateVirtualCamera(rigObject.transform, "Multi Target Camera", player, null);
            CopyProductionCameraSettings(freeCamera, "FollowCamera", copyInput: true);
            CopyProductionCameraSettings(singleCamera, "SingleTargetCamera", copyInput: false);
            CopyProductionCameraSettings(multiCamera, "MultiTargetCamera", copyInput: false);

            var targetGroup = targetingSystem.GetComponentInChildren<CinemachineTargetGroup>(true);
            multiCamera.LookAt = targetGroup.transform;
            multiCamera.gameObject.AddComponent<CinemachineGroupFraming>();

            var groupPresenter = targetingSystem.GetComponent<TargetingCameraGroupPresenter>();
            groupPresenter.Configure(
                singleCamera,
                multiCamera,
                targetGroup,
                TargetingCameraFramingMode.PrimaryCentered,
                1f,
                0.5f);
            groupPresenter.SetPlayerAnchor(playerAim);

            var presenter = targetingSystem.AddComponent<TargetingCameraPresenter>();
            SetObjectReference(presenter, "controller", controller);
            SetObjectReference(presenter, "mixingCamera", mixer);
            SetObjectReference(presenter, "freeCamera", freeCamera);
            SetObjectReference(presenter, "singleCamera", singleCamera);
            SetObjectReference(presenter, "multiCamera", multiCamera);
            SetObjectReference(presenter, "multiTargetGroup", targetGroup);
            SetObjectReference(presenter, "fallbackLookAt", playerAim);
            SetObjectReference(presenter, "groupPresenter", groupPresenter);
            mixer.SetWeight(freeCamera, 1f);
            mixer.SetWeight(singleCamera, 0f);
            mixer.SetWeight(multiCamera, 0f);
        }

        private static void CopyProductionCameraSettings(
            CinemachineCamera destination,
            string sourceCameraName,
            bool copyInput)
        {
            var productionRig = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/SteamMultiRuntime/Runtime/Resources/System/Local Mixing Camera.prefab");
            var sourceCamera = FindChild(productionRig.transform, sourceCameraName);
            if (sourceCamera == null)
            {
                return;
            }

            EditorUtility.CopySerialized(
                sourceCamera.GetComponent<CinemachineOrbitalFollow>(),
                destination.GetComponent<CinemachineOrbitalFollow>());
            EditorUtility.CopySerialized(
                sourceCamera.GetComponent<CinemachineRotationComposer>(),
                destination.GetComponent<CinemachineRotationComposer>());

            if (copyInput && sourceCamera.TryGetComponent<CinemachineInputAxisController>(out var sourceInput))
            {
                var destinationInput = destination.gameObject.AddComponent<CinemachineInputAxisController>();
                EditorUtility.CopySerialized(sourceInput, destinationInput);
            }
        }

        private static Transform FindChild(Transform root, string childName)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == childName)
                {
                    return child;
                }
            }

            return null;
        }

        private static CinemachineCamera CreateVirtualCamera(
            Transform parent,
            string cameraName,
            Transform follow,
            Transform lookAt)
        {
            var cameraObject = new GameObject(cameraName);
            cameraObject.transform.SetParent(parent, false);
            var camera = cameraObject.AddComponent<CinemachineCamera>();
            camera.Follow = follow;
            camera.LookAt = lookAt;
            cameraObject.AddComponent<CinemachineOrbitalFollow>();
            cameraObject.AddComponent<CinemachineRotationComposer>();
            return camera;
        }

        private static void CreateIndicator(TargetingController controller, Camera camera)
        {
            var uiObject = new GameObject("Current Target Indicator");
            var document = uiObject.AddComponent<UIDocument>();
            document.panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(
                GenericPackageRoot + "/Runtime/Resources/UI/TargetIndicator Panel Settings.asset");
            document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                GenericPackageRoot + "/Runtime/Resources/UI/TargetIndicator.uxml");

            var theme = uiObject.AddComponent<TargetIndicatorThemeProvider>();
            SetObjectReference(theme, "targetIndicatorVisualTree", document.visualTreeAsset);
            SetObjectReference(theme, "targetIndicatorStyleSheet", AssetDatabase.LoadAssetAtPath<StyleSheet>(
                GenericPackageRoot + "/Runtime/Resources/UI/TargetIndicator.uss"));

            var indicator = uiObject.AddComponent<TargetIndicatorController>();
            SetObjectReference(indicator, "controller", controller);
            SetObjectReference(indicator, "targetCamera", camera);
            SetObjectReference(indicator, "uiDocument", document);
            SetObjectReference(indicator, "themeProvider", theme);
        }

        private static void CreateRandomTargetSpawner(TargetMarkerRegistry registry)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                GenericPackageRoot + "/Runtime/Prefabs/Samples/RandomTarget.prefab");

            var spawnerObject = new GameObject("Random Target Spawner");
            spawnerObject.transform.position = new Vector3(0f, 2.5f, 10f);
            var spawner = spawnerObject.AddComponent<TargetMarkerRandomSpawner>();
            SetObjectReference(spawner, "targetPrefab", prefab.GetComponent<TargetMarker>());
            SetObjectReference(spawner, "registry", registry);
        }

        private static void CreateGround()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = Vector3.one * 3f;
        }

        private static void CreateLight()
        {
            var lightObject = new GameObject("Directional Light");
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
        }

        private static void NormalizeUnityYaml(string assetPath)
        {
            var fullPath = Path.GetFullPath(assetPath);
            var yaml = File.ReadAllText(fullPath);
            yaml = yaml.Replace("m_Name: \r\n", "m_Name:\r\n").Replace("m_Name: \n", "m_Name:\n");
            File.WriteAllText(fullPath, yaml);
        }

        private static void SetObjectReference(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(propertyName).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetObjectArray(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName);
            property.arraySize = 1;
            property.GetArrayElementAtIndex(0).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

    }
}
