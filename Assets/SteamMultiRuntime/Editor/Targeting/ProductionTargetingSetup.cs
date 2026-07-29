using System;
using System.Collections.Generic;
using Koiusa.TargetingSystem.Runtime;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Koiusa.SteamMultiRuntime.TargetingSystem.Editor
{
    public static class ProductionTargetingSetup
    {
        private const string InputAssetPath = "Assets/SteamMultiRuntime/Runtime/Configs/Input/SteamMultiRuntime_InputActions.inputactions";
        private const string InputConfigPath = "Assets/SteamMultiRuntime/Runtime/Configs/Input/GameplayTargetingInputActions.asset";
        private const string SystemPrefabPath = "Assets/SteamMultiRuntime/Runtime/Resources/System/System.prefab";

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
            ConfigureInput();
            EditPrefab(SystemPrefabPath, ConfigureSystemPrefab);

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
            var inputAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputAssetPath);
            var playerMap = inputAsset?.FindActionMap("Player", false);
            if (playerMap == null) throw new InvalidOperationException($"Player action map was not found: {InputAssetPath}");

            var multiLock = playerMap.FindAction("MultiLock", false);
            if (multiLock == null)
            {
                multiLock = playerMap.AddAction("MultiLock", InputActionType.Button);
                multiLock.AddBinding("<Keyboard>/3", groups: "Keyboard&Mouse");
                multiLock.AddBinding("<Gamepad>/rightStickPress", groups: "Gamepad");
                EditorUtility.SetDirty(inputAsset);
            }

            var inputConfig = AssetDatabase.LoadAssetAtPath<SteamMultiRuntimeTargetingInputActions>(InputConfigPath);
            if (inputConfig == null) throw new InvalidOperationException($"Targeting input config was not found: {InputConfigPath}");
            SetObjectProperty(inputConfig, "multiLockActionPath", "Player/MultiLock");
        }

        private static void ValidateInput(ICollection<string> errors)
        {
            var inputAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputAssetPath);
            var multiLock = inputAsset?.FindAction("Player/MultiLock", false);
            if (multiLock == null)
            {
                errors.Add("Player/MultiLock is missing.");
                return;
            }

            var hasKeyboard = false;
            var hasGamepad = false;
            foreach (var binding in multiLock.bindings)
            {
                hasKeyboard |= binding.path == "<Keyboard>/3";
                hasGamepad |= binding.path == "<Gamepad>/rightStickPress";
            }
            if (!hasKeyboard) errors.Add("Player/MultiLock keyboard binding is missing.");
            if (!hasGamepad) errors.Add("Player/MultiLock gamepad binding is missing.");
        }

        private static void ValidateSystem(ICollection<string> errors)
        {
            ValidateComponent<TargetMarkerRegistry>(SystemPrefabPath, errors);
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
            if (prefab.GetComponentInChildren<CinemachineTargetGroup>(true) == null) errors.Add($"CinemachineTargetGroup: {path}");
            if (prefab.GetComponent<LocalTargetingCameraConnector>() == null) errors.Add($"LocalTargetingCameraConnector: {path}");
        }

        private static void ValidateComponent<T>(string path, ICollection<string> errors) where T : Component
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null || prefab.GetComponent<T>() == null) errors.Add($"Required component is missing: {path}");
        }

        private static void ConfigureSystemPrefab(GameObject root)
        {
            GetOrAdd<TargetMarkerRegistry>(root);
        }

        private static void ConfigureTargetMarker(GameObject root)
        {
            GetOrAdd<TargetMarker>(root);
        }

        private static void ConfigurePlayerTargeting(GameObject root)
        {
            var context = GetOrAdd<TargetingContextProvider>(root);
            var candidates = GetOrAdd<RegistryTargetCandidateSource>(root);
            var policy = GetOrAdd<ViewportTargetPolicy>(root);
            var controller = GetOrAdd<TargetingController>(root);
            var input = GetOrAdd<TargetingCommandInput>(root);
            var owner = GetOrAdd<PlayerTargetingOwner>(root);
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
            var mixingCamera = root.GetComponent<CinemachineMixingCamera>();
            var controller = root.GetComponent<CameraMixerWeightControllerBase>();
            if (mixingCamera == null || controller == null)
                throw new InvalidOperationException($"Camera prefab is missing its mixer/controller: {root.name}");

            var defaultCamera = FindCamera(root, "DefaultCamera");
            var followCamera = FindCamera(root, "FollowCamera");
            if (defaultCamera == null || followCamera == null)
                throw new InvalidOperationException($"Default/Follow camera was not found: {root.name}");

            var singleCamera = GetOrCreateTargetCamera(mixingCamera.transform, followCamera, "SingleTargetCamera");
            var multiCamera = GetOrCreateTargetCamera(mixingCamera.transform, followCamera, "MultiTargetCamera");
            var targetGroup = GetOrCreateTargetGroup(mixingCamera.transform);
            multiCamera.LookAt = targetGroup.transform;

            defaultCamera.transform.SetSiblingIndex(0);
            followCamera.transform.SetSiblingIndex(1);
            singleCamera.transform.SetSiblingIndex(2);
            multiCamera.transform.SetSiblingIndex(3);

            var serialized = new SerializedObject(controller);
            serialized.FindProperty("defaultCameraIndex").intValue = 0;
            serialized.FindProperty("followCameraIndex").intValue = 1;
            serialized.FindProperty("singleTargetCameraIndex").intValue = 2;
            serialized.FindProperty("multiTargetCameraIndex").intValue = 3;
            serialized.FindProperty("singleTargetCamera").objectReferenceValue = singleCamera;
            serialized.FindProperty("multiTargetCamera").objectReferenceValue = multiCamera;
            serialized.FindProperty("multiTargetGroup").objectReferenceValue = targetGroup;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            GetOrAdd<LocalTargetingCameraConnector>(root);
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
            if (existing != null) return existing;
            var groupObject = new GameObject("TargetingTargetGroup");
            groupObject.transform.SetParent(parent, false);
            return groupObject.AddComponent<CinemachineTargetGroup>();
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
