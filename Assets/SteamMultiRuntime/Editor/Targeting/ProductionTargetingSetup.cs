using System;
using System.Collections.Generic;
using Koiusa.TargetingSystem.Runtime;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime.TargetingSystem.Editor
{
    public static class ProductionTargetingSetup
    {
        private const string InputConfigPath = "Assets/SteamMultiRuntime/Runtime/Configs/Input/GameplayTargetingInputActions.asset";
        private static readonly string[] PlayerPrefabPaths =
        {
            "Assets/SteamMultiRuntime/Runtime/Resources/Character/Proxy/LocalPlayer_WithAnimator.prefab",
            "Assets/SteamMultiRuntime/Runtime/Resources/Character/Proxy/NetworkPlayer_WithAnimator.prefab",
            "Assets/SteamMultiRuntime/Runtime/Resources/Character/Proxy/NetworkPlayer_Runtime.prefab",
        };

        private static readonly string[] TargetPrefabPaths =
        {
            "Assets/SteamMultiRuntime/Runtime/Resources/Character/Proxy/LocalPlayer_WithAnimator.prefab",
            "Assets/SteamMultiRuntime/Runtime/Resources/Character/Proxy/NetworkPlayer_WithAnimator.prefab",
            "Assets/SteamMultiRuntime/Runtime/Resources/Character/Proxy/NetworkPlayer_Runtime.prefab",
            "Assets/SteamMultiRuntime/Runtime/Resources/Character/Proxy/LocalPlayer_NPC.prefab",
            "Assets/SteamMultiRuntime/Runtime/Resources/Character/Proxy/NetworkPlayer_NPC.prefab",
        };

        private static readonly string[] CameraPrefabPaths =
        {
            "Assets/SteamMultiRuntime/Runtime/Resources/System/Local Mixing Camera.prefab",
            "Assets/SteamMultiRuntime/Runtime/Resources/System/Network Mixing Camera.prefab",
        };

        [MenuItem("Tools/SteamMultiRuntime/Targeting/Install Production Setup")]
        public static void InstallProductionSetup()
        {
            TargetingGameplayPrefabMigration.Migrate();
            ConfigureInput();

            foreach (var path in TargetPrefabPaths) EditPrefab(path, ConfigureTargetMarker);
            foreach (var path in PlayerPrefabPaths) EditPrefab(path, ConfigurePlayerTargeting);
            foreach (var path in CameraPrefabPaths) EditPrefab(path, ConfigureCameraTargeting);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Production Targeting setup installed.");
        }

        [MenuItem("Tools/SteamMultiRuntime/Read Only/Targeting/Validate Production Setup")]
        public static void ValidateProductionSetup()
        {
            var errors = new List<string>();
            ValidateSystem(errors);
            foreach (var path in TargetPrefabPaths) ValidateComponent<TargetMarker>(path, errors);
            foreach (var path in PlayerPrefabPaths) ValidatePlayer(path, errors);
            foreach (var path in CameraPrefabPaths) ValidateCamera(path, errors);
            ValidateInput(errors);

            if (errors.Count > 0)
                throw new InvalidOperationException($"Production Targeting validation failed:\n{string.Join("\n", errors)}");

            Debug.Log("Production Targeting validation passed.");
        }

        private static void ConfigureInput()
        {
            var inputConfig = AssetDatabase.LoadAssetAtPath<SteamMultiRuntimeTargetingInputActions>(InputConfigPath);
            if (inputConfig == null) throw new InvalidOperationException($"Targeting input config was not found: {InputConfigPath}");
            SetObjectProperty(inputConfig, "multiLockActionPath", string.Empty);
        }

        private static void ValidateInput(ICollection<string> errors)
        {
            var inputConfig = AssetDatabase.LoadAssetAtPath<SteamMultiRuntimeTargetingInputActions>(InputConfigPath);
            if (inputConfig != null && inputConfig.MultiLockAction != null)
                errors.Add("Explicit MultiLock input must be unassigned; use Player/Strafe hold promotion instead.");
        }

        private static void ValidateSystem(ICollection<string> errors)
        {
            ValidateChildComponent<TargetMarkerRegistry>(TargetingGameplayPrefabMigration.TargetingPrefabPath, errors);
            ValidateChildComponent<LocalTargetingIndicatorPresenter>(TargetingGameplayPrefabMigration.TargetingPrefabPath, errors);
            ValidateChildComponent<TargetMarkerRegistry>(TargetingGameplayPrefabMigration.GameplaySystemPrefabPath, errors);
        }

        private static void ValidatePlayer(string path, ICollection<string> errors)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                errors.Add($"Prefab is missing: {path}");
                return;
            }

            if (prefab.GetComponent<TargetingContextProvider>() == null) errors.Add($"TargetingContextProvider: {path}");
            if (prefab.GetComponent<RegistryTargetCandidateSource>() == null) errors.Add($"RegistryTargetCandidateSource: {path}");
            if (prefab.GetComponent<ViewportTargetPolicy>() == null) errors.Add($"ViewportTargetPolicy: {path}");
            if (prefab.GetComponent<TargetingController>() == null) errors.Add($"TargetingController: {path}");
            if (prefab.GetComponent<TargetingCommandInput>() == null) errors.Add($"TargetingCommandInput: {path}");
            if (prefab.GetComponent<PlayerTargetingOwner>() == null) errors.Add($"PlayerTargetingOwner: {path}");
            if (prefab.GetComponent<TargetingFacingRequestSource>() == null) errors.Add($"TargetingFacingRequestSource: {path}");
        }

        private static void ValidateCamera(string path, ICollection<string> errors)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                errors.Add($"Prefab is missing: {path}");
                return;
            }

            if (FindCamera(prefab, "SingleTargetCamera") == null) errors.Add($"SingleTargetCamera: {path}");
            if (FindCamera(prefab, "MultiTargetCamera") == null) errors.Add($"MultiTargetCamera: {path}");
            var mixer = prefab.GetComponentInChildren<CinemachineMixingCamera>(true);
            if (mixer == null || mixer.transform.parent != prefab.transform) errors.Add($"Camera Mixer hierarchy: {path}");
        }

        private static void ValidateComponent<T>(string path, ICollection<string> errors) where T : Component
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null || prefab.GetComponent<T>() == null) errors.Add($"Required component is missing: {path}");
        }

        private static void ValidateChildComponent<T>(string path, ICollection<string> errors) where T : Component
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null || prefab.GetComponentInChildren<T>(true) == null)
                errors.Add($"Required child component is missing: {path}");
        }

        private static void ConfigureTargetMarker(GameObject root)
        {
            var targetMarker = GetOrAdd<TargetMarker>(root);
            var focusMarker = root.GetComponentInChildren<CameraTrackMarker>(true);
            if (focusMarker == null)
                throw new InvalidOperationException($"CameraTrackMarker was not found: {root.name}");

            var serialized = new SerializedObject(targetMarker);
            serialized.FindProperty("aimPoint").objectReferenceValue = focusMarker.transform;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigurePlayerTargeting(GameObject root)
        {
            var context = GetOrAdd<TargetingContextProvider>(root);
            var candidates = GetOrAdd<RegistryTargetCandidateSource>(root);
            var policy = GetOrAdd<ViewportTargetPolicy>(root);
            var controller = GetOrAdd<TargetingController>(root);
            var input = GetOrAdd<TargetingCommandInput>(root);
            var owner = GetOrAdd<PlayerTargetingOwner>(root);
            GetOrAdd<TargetingFacingRequestSource>(root);
            var inputConfig = AssetDatabase.LoadAssetAtPath<SteamMultiRuntimeTargetingInputActions>(InputConfigPath);

            var controllerObject = new SerializedObject(controller);
            controllerObject.FindProperty("contextSource").objectReferenceValue = context;
            controllerObject.FindProperty("candidateSource").objectReferenceValue = candidates;
            SetArray(controllerObject.FindProperty("filters"), policy);
            SetArray(controllerObject.FindProperty("scorers"), policy);
            controllerObject.ApplyModifiedPropertiesWithoutUndo();

            var inputObject = new SerializedObject(input);
            inputObject.FindProperty("controller").objectReferenceValue = controller;
            inputObject.FindProperty("inputActions").objectReferenceValue = inputConfig;
            inputObject.ApplyModifiedPropertiesWithoutUndo();

            var ownerObject = new SerializedObject(owner);
            ownerObject.FindProperty("controller").objectReferenceValue = controller;
            ownerObject.FindProperty("input").objectReferenceValue = input;
            ownerObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureCameraTargeting(GameObject root)
        {
            var mixingCamera = root.GetComponentInChildren<CinemachineMixingCamera>(true);
            var controller = root.GetComponent<CameraMixerWeightControllerBase>();
            if (mixingCamera == null || controller == null)
                throw new InvalidOperationException($"Camera prefab is missing its mixer/controller: {root.name}");

            var defaultCamera = FindCamera(root, "DefaultCamera");
            var followCamera = FindCamera(root, "FollowCamera");
            if (defaultCamera == null || followCamera == null)
                throw new InvalidOperationException($"Default/Follow camera was not found: {root.name}");

            var existingPresenter = root.GetComponentInChildren<TargetingCameraGroupPresenter>(true);
            if (existingPresenter == null)
                throw new InvalidOperationException("Shared Targeting Camera System prefab is not nested under the camera prefab.");
            var targetingSystem = existingPresenter.gameObject;

            var singleCamera = GetOrCreateTargetCamera(mixingCamera.transform, followCamera, "SingleTargetCamera");
            var multiCamera = GetOrCreateTargetCamera(mixingCamera.transform, followCamera, "MultiTargetCamera");
            var targetGroup = GetOrCreateTargetGroup(targetingSystem.transform);
            multiCamera.LookAt = targetGroup.transform;
            GetOrAdd<CinemachineGroupFraming>(multiCamera.gameObject);
            var groupPresenter = GetOrAdd<TargetingCameraGroupPresenter>(targetingSystem);
            groupPresenter.Configure(
                singleCamera,
                multiCamera,
                targetGroup,
                TargetingCameraFramingMode.PrimaryCentered,
                1f,
                0.5f);

            defaultCamera.transform.SetSiblingIndex(0);
            followCamera.transform.SetSiblingIndex(1);
            singleCamera.transform.SetSiblingIndex(2);
            multiCamera.transform.SetSiblingIndex(3);

            var serialized = new SerializedObject(controller);
            serialized.FindProperty("defaultCameraIndex").intValue = 0;
            serialized.FindProperty("followCameraIndex").intValue = 1;
            serialized.FindProperty("singleTargetCameraIndex").intValue = 2;
            serialized.FindProperty("multiTargetCameraIndex").intValue = 3;
            serialized.FindProperty("followCamera").objectReferenceValue = followCamera;
            serialized.FindProperty("singleTargetCamera").objectReferenceValue = singleCamera;
            serialized.FindProperty("multiTargetCamera").objectReferenceValue = multiCamera;
            serialized.FindProperty("multiTargetGroup").objectReferenceValue = targetGroup;
            serialized.FindProperty("targetingGroupPresenter").objectReferenceValue = groupPresenter;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var connector = GetOrAdd<LocalTargetingCameraConnector>(targetingSystem);
            var connectorObject = new SerializedObject(connector);
            connectorObject.FindProperty("consumerSource").objectReferenceValue = controller;
            connectorObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static CinemachineCamera GetOrCreateTargetCamera(Transform parent, CinemachineCamera source, string name)
        {
            var existing = FindCamera(parent.gameObject, name);
            if (existing != null) return existing;

            var targetObject = new GameObject(name);
            targetObject.transform.SetParent(parent, false);
            var camera = targetObject.AddComponent<CinemachineCamera>();
            EditorUtility.CopySerialized(source, camera);
            camera.LookAt = null;

            CopyComponent<CinemachineOrbitalFollow>(source.gameObject, targetObject);
            CopyComponent<CinemachineRotationComposer>(source.gameObject, targetObject);
            CopyComponent<CinemachineDeoccluder>(source.gameObject, targetObject);
            CopyComponent<CinemachineDecollider>(source.gameObject, targetObject);
            return camera;
        }

        private static CinemachineTargetGroup GetOrCreateTargetGroup(Transform parent)
        {
            var existing = parent.GetComponentInChildren<CinemachineTargetGroup>(true);
            if (existing == null)
            {
                var groupObject = new GameObject("TargetingTargetGroup");
                groupObject.transform.SetParent(parent, false);
                existing = groupObject.AddComponent<CinemachineTargetGroup>();
            }
            var primaryOnStandardGroup = existing.GetComponent<PrimaryCenteredCinemachineTargetGroup>();
            if (primaryOnStandardGroup != null)
            {
                UnityEngine.Object.DestroyImmediate(primaryOnStandardGroup);
            }
            var primary = parent.GetComponentInChildren<PrimaryCenteredCinemachineTargetGroup>(true);
            if (primary == null)
            {
                var primaryObject = new GameObject("PrimaryCenteredTargetGroup");
                primaryObject.transform.SetParent(parent, false);
                primaryObject.AddComponent<PrimaryCenteredCinemachineTargetGroup>();
            }
            var standard = GetOrAdd<StandardCinemachineTargetGroupFraming>(existing.gameObject);
            standard.Configure(existing);
            GetOrAdd<TargetingCameraRuntimeObjectFactory>(parent.gameObject);
            return existing;
        }

        private static CinemachineCamera FindCamera(GameObject root, string name)
        {
            foreach (var camera in root.GetComponentsInChildren<CinemachineCamera>(true))
                if (camera.name == name) return camera;
            return null;
        }

        private static void CopyComponent<T>(GameObject source, GameObject destination) where T : Component
        {
            var sourceComponent = source.GetComponent<T>();
            if (sourceComponent == null) return;
            var destinationComponent = destination.AddComponent<T>();
            EditorUtility.CopySerialized(sourceComponent, destinationComponent);
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            var component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
        }

        private static void SetArray(SerializedProperty array, UnityEngine.Object value)
        {
            array.arraySize = 1;
            array.GetArrayElementAtIndex(0).objectReferenceValue = value;
        }

        private static void SetObjectProperty(UnityEngine.Object target, string propertyName, string value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(propertyName).stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void EditPrefab(string path, Action<GameObject> edit)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                edit(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
