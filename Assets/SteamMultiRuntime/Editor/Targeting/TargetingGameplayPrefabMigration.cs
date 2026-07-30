using System.IO;
using Koiusa.TargetingSystem.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Koiusa.SteamMultiRuntime.TargetingSystem.Editor
{
    public static class TargetingGameplayPrefabMigration
    {
        public const string TargetingPrefabPath =
            "Assets/SteamMultiRuntime/Runtime/Prefabs/Targeting/Targeting System.prefab";
        public const string GameplaySystemPrefabPath =
            "Assets/SteamMultiRuntime/Runtime/Prefabs/System/Gameplay System.prefab";
        private const string SystemPrefabPath =
            "Assets/SteamMultiRuntime/Runtime/Resources/System/System.prefab";
        private const string IndicatorAssetRoot =
            "Assets/SteamMultiRuntime/Runtime/Packages/com.koiusa.targetingsystem/Runtime/Resources/UI/";

        [MenuItem("Tools/SteamMultiRuntime/Targeting/Migrate Gameplay Targeting Prefab")]
        public static void Migrate()
        {
            TargetingCameraPrefabMigration.Migrate();
            RebuildTargetingPrefab();
            RebuildGameplaySystemPrefab();
            NestUnderSystemPrefab();
            RemoveFromGameplayScenes();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Gameplay System was nested under System, with Targeting System as its child.");
        }

        private static void RebuildTargetingPrefab()
        {
            var root = new GameObject("Targeting System");
            try
            {
                root.AddComponent<TargetMarkerRegistry>();
                var presenter = root.AddComponent<LocalTargetingIndicatorPresenter>();

                var indicatorObject = new GameObject("Target Indicator UI");
                indicatorObject.transform.SetParent(root.transform, false);
                var document = indicatorObject.AddComponent<UIDocument>();
                document.panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(
                    IndicatorAssetRoot + "TargetIndicator Panel Settings.asset");
                document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                    IndicatorAssetRoot + "TargetIndicator.uxml");
                document.sortingOrder = short.MaxValue - 2;

                var theme = indicatorObject.AddComponent<TargetIndicatorThemeProvider>();
                theme.Configure(
                    document.visualTreeAsset,
                    AssetDatabase.LoadAssetAtPath<StyleSheet>(IndicatorAssetRoot + "TargetIndicator.uss"));
                var indicator = indicatorObject.AddComponent<TargetIndicatorController>();
                presenter.Configure(indicatorObject, indicator);
                indicatorObject.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(root, TargetingPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void RebuildGameplaySystemPrefab()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(GameplaySystemPrefabPath) ?? string.Empty);
            var targetingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TargetingPrefabPath);
            var root = new GameObject("Gameplay System");
            try
            {
                var targeting = PrefabUtility.InstantiatePrefab(targetingPrefab, root.transform) as GameObject;
                if (targeting == null)
                    throw new UnityException("Failed to nest Targeting System under Gameplay System.");
                targeting.name = "Targeting System";
                PrefabUtility.SaveAsPrefabAsset(root, GameplaySystemPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void NestUnderSystemPrefab()
        {
            var sharedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GameplaySystemPrefabPath);
            var root = PrefabUtility.LoadPrefabContents(SystemPrefabPath);
            try
            {
                var existing = root.transform.Find("Gameplay System");
                if (existing != null)
                    Object.DestroyImmediate(existing.gameObject);

                var legacyTargeting = root.transform.Find("Targeting System");
                if (legacyTargeting != null)
                    Object.DestroyImmediate(legacyTargeting.gameObject);

                var registry = root.GetComponent<TargetMarkerRegistry>();
                if (registry != null) Object.DestroyImmediate(registry);
                var presenter = root.GetComponent<LocalTargetingIndicatorPresenter>();
                if (presenter != null) Object.DestroyImmediate(presenter);

                var instance = PrefabUtility.InstantiatePrefab(sharedPrefab, root.transform) as GameObject;
                if (instance == null)
                    throw new UnityException("Failed to nest Gameplay System under System.prefab.");
                instance.name = "Gameplay System";
                PrefabUtility.SaveAsPrefabAsset(root, SystemPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void RemoveFromGameplayScenes()
        {
            var sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/SteamMultiRuntime/Samples" });
            foreach (var guid in sceneGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var changed = false;
                foreach (var root in scene.GetRootGameObjects())
                {
                    if (root.name != "Targeting System") continue;
                    Object.DestroyImmediate(root);
                    changed = true;
                }

                var controllers = Object.FindObjectsByType<CameraMixerWeightControllerBase>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var controller in controllers)
                    changed |= RevertLegacySceneCameraOverrides(controller);

                if (!changed) continue;
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }

        private static bool RevertLegacySceneCameraOverrides(CameraMixerWeightControllerBase controller)
        {
            if (!PrefabUtility.IsPartOfPrefabInstance(controller))
                return false;

            var changed = false;
            var serialized = new SerializedObject(controller);
            changed |= RevertOverride(serialized.FindProperty("multiTargetGroup"));
            changed |= RevertOverride(serialized.FindProperty("targetingGroupPresenter"));
            return changed;
        }

        private static bool RevertOverride(SerializedProperty property)
        {
            if (property == null || !property.prefabOverride)
                return false;

            PrefabUtility.RevertPropertyOverride(property, InteractionMode.AutomatedAction);
            return true;
        }
    }
}
