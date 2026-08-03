using System;
using System.IO;
using Koiusa.SteamMultiRuntime.TargetingSystem;
using Koiusa.TargetingSystem.Runtime;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime.TargetingSystem.Editor
{
    public static class TargetingCameraPrefabMigration
    {
        public const string TargetingPrefabPath =
            "Assets/SteamMultiRuntime/Runtime/Prefabs/Camera/Targeting Camera System.prefab";

        private static readonly string[] CameraPrefabPaths =
        {
            "Assets/SteamMultiRuntime/Runtime/Resources/System/Local Mixing Camera.prefab",
            "Assets/SteamMultiRuntime/Runtime/Resources/System/Network Mixing Camera.prefab"
        };

        [MenuItem("Tools/SteamMultiRuntime/Maintenance/Targeting/Migrate Camera Targeting Prefab")]
        public static void Migrate()
        {
            CreateSharedPrefab();
            foreach (var path in CameraPrefabPaths) MigrateCameraPrefab(path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Targeting camera system migrated to a shared nested prefab.");
        }

        private static void CreateSharedPrefab()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(TargetingPrefabPath) ?? string.Empty);
            var root = new GameObject("Targeting Camera System");
            try
            {
                root.AddComponent<LocalTargetingCameraConnector>();
                root.AddComponent<TargetingCameraRuntimeObjectFactory>();

                var standardObject = new GameObject("Targeting Target Group");
                standardObject.transform.SetParent(root.transform, false);
                var targetGroup = standardObject.AddComponent<CinemachineTargetGroup>();
                var standard = standardObject.AddComponent<StandardCinemachineTargetGroupFraming>();
                standard.Configure(targetGroup);

                var primaryObject = new GameObject("Primary Centered Target Group");
                primaryObject.transform.SetParent(root.transform, false);
                primaryObject.AddComponent<PrimaryCenteredCinemachineTargetGroup>();

                var presenter = root.AddComponent<TargetingCameraGroupPresenter>();
                presenter.Configure(
                    null,
                    null,
                    targetGroup,
                    TargetingCameraFramingMode.PrimaryCentered,
                    1f,
                    0.5f);

                PrefabUtility.SaveAsPrefabAsset(root, TargetingPrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void MigrateCameraPrefab(string path)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var controller = root.GetComponent<CameraMixerWeightControllerBase>();
                var mixer = root.GetComponentInChildren<CinemachineMixingCamera>(true);
                var singleCamera = FindCamera(root, "SingleTargetCamera");
                var multiCamera = FindCamera(root, "MultiTargetCamera");
                if (controller == null || mixer == null || singleCamera == null || multiCamera == null)
                    throw new InvalidOperationException($"Camera prefab is incomplete: {path}");

                var existingPresenter = root.GetComponentInChildren<TargetingCameraGroupPresenter>(true);
                if (existingPresenter != null)
                    UnityEngine.Object.DestroyImmediate(existingPresenter.gameObject);

                var sharedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TargetingPrefabPath);
                var instance = PrefabUtility.InstantiatePrefab(sharedPrefab, root.transform) as GameObject;
                if (instance == null)
                    throw new InvalidOperationException($"Could not instantiate {TargetingPrefabPath}");
                instance.name = "Targeting Camera System";

                var presenter = instance.GetComponent<TargetingCameraGroupPresenter>();
                var connector = instance.GetComponent<LocalTargetingCameraConnector>();
                var targetGroup = instance.GetComponentInChildren<CinemachineTargetGroup>(true);
                presenter.Configure(
                    singleCamera,
                    multiCamera,
                    targetGroup,
                    TargetingCameraFramingMode.PrimaryCentered,
                    1f,
                    0.5f);

                var connectorObject = new SerializedObject(connector);
                connectorObject.FindProperty("consumerSource").objectReferenceValue = controller;
                connectorObject.ApplyModifiedPropertiesWithoutUndo();

                var controllerObject = new SerializedObject(controller);
                controllerObject.FindProperty("mixingCamera").objectReferenceValue = mixer;
                controllerObject.FindProperty("singleTargetCamera").objectReferenceValue = singleCamera;
                controllerObject.FindProperty("multiTargetCamera").objectReferenceValue = multiCamera;
                controllerObject.FindProperty("multiTargetGroup").objectReferenceValue = targetGroup;
                controllerObject.FindProperty("targetingGroupPresenter").objectReferenceValue = presenter;
                controllerObject.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static CinemachineCamera FindCamera(GameObject root, string name)
        {
            foreach (var camera in root.GetComponentsInChildren<CinemachineCamera>(true))
                if (camera.name == name) return camera;
            return null;
        }
    }
}
