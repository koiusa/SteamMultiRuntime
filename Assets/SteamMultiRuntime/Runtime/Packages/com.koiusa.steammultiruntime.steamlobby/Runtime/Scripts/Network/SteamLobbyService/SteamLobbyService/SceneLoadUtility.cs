using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Koiusa.SteamMultiRuntime
{
    internal static class SceneLoadUtility
    {
        public static async Task<bool> LoadSceneAdditiveAsync(string sceneReference, UnityEngine.Object owner, string logPrefix)
        {
            if (string.IsNullOrWhiteSpace(sceneReference))
            {
                return false;
            }

            if (!SceneUtilityEx.CanLoadScene(sceneReference))
            {
                Debug.LogError($"[{logPrefix}] Scene '{sceneReference}' is not in Build Settings.", owner);
                return false;
            }

            var loadedScene = SceneUtilityEx.GetLoadedScene(sceneReference);
            if (!(loadedScene.IsValid() && loadedScene.isLoaded))
            {
                var operation = SceneManager.LoadSceneAsync(sceneReference, LoadSceneMode.Additive);
                if (operation == null)
                {
                    Debug.LogError($"[{logPrefix}] Failed to start loading scene '{sceneReference}'.", owner);
                    return false;
                }

                await SceneUtilityEx.WaitForOperationAsync(operation);
                loadedScene = SceneUtilityEx.GetLoadedScene(sceneReference);
            }

            return loadedScene.IsValid() && loadedScene.isLoaded;
        }

        public static async Task<bool> LoadPresentationSceneAsync(string sceneReference, ISceneLoadContext context, UnityEngine.Object owner, string logPrefix)
        {
            return await LoadPresentationSceneWithCameraPolicyAsync(sceneReference, context.DisableCamerasInLoadedScenes, owner, logPrefix);
        }

        private static async Task<bool> LoadPresentationSceneWithCameraPolicyAsync(string sceneReference, bool disableCamerasInLoadedScenes, UnityEngine.Object owner, string logPrefix)
        {
            var existingScene = SceneUtilityEx.GetLoadedScene(sceneReference);
            if (existingScene.IsValid()
                && existingScene.isLoaded
                && SceneManager.GetActiveScene() == existingScene)
            {
                // Editorの開始SceneやNetcode側で先にロードされたSceneでも、
                // Presentation Scene内のCameraを残さない。ここでreturnすると
                // Cinemachineが追従していても、このSceneの固定CameraがGame画面を描画する。
                ApplyLoadedSceneCameraSettings(existingScene, disableCamerasInLoadedScenes);
                return true;
            }

            if (!await LoadSceneAdditiveAsync(sceneReference, owner, logPrefix))
            {
                return false;
            }

            var loadedScene = SceneUtilityEx.GetLoadedScene(sceneReference);
            ApplyLoadedSceneCameraSettings(loadedScene, disableCamerasInLoadedScenes);
            ActivatePresentationScene(loadedScene);
            return true;
        }

        public static async Task<bool> SwitchPresentationSceneAsync(
            string targetSceneReference,
            IEnumerable<string> previousSceneReferences,
            bool disableCamerasInLoadedScenes,
            UnityEngine.Object owner,
            string logPrefix)
        {
            if (!await LoadPresentationSceneWithCameraPolicyAsync(targetSceneReference, disableCamerasInLoadedScenes, owner, logPrefix))
            {
                return false;
            }

            if (previousSceneReferences == null)
            {
                return true;
            }

            foreach (var previousSceneReference in previousSceneReferences)
            {
                if (string.IsNullOrWhiteSpace(previousSceneReference)
                    || AreSameSceneReference(previousSceneReference, targetSceneReference))
                {
                    continue;
                }

                await UnloadSceneAsync(previousSceneReference);
            }

            return true;
        }

        public static async Task<bool> UnloadSceneAsync(string sceneReference)
        {
            var scene = SceneUtilityEx.GetLoadedScene(sceneReference);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return false;
            }

            var operation = SceneManager.UnloadSceneAsync(scene);
            if (operation == null)
            {
                return false;
            }

            await SceneUtilityEx.WaitForOperationAsync(operation);
            return true;
        }

        public static void ApplyLoadedSceneCameraSettings(Scene scene, bool disableCamerasInLoadedScenes)
        {
            if (!disableCamerasInLoadedScenes || !scene.IsValid() || !scene.isLoaded)
            {
                return;
            }

            foreach (var rootGameObject in scene.GetRootGameObjects())
            {
                foreach (var camera in rootGameObject.GetComponentsInChildren<Camera>(true))
                {
                    camera.enabled = false;

                    if (camera.gameObject.activeSelf)
                    {
                        camera.gameObject.SetActive(false);
                    }
                }
            }
        }

        public static void ActivatePresentationScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return;
            }

            SceneManager.SetActiveScene(scene);
            DynamicGI.UpdateEnvironment();
        }

        public static bool AreSameSceneReference(string left, string right)
        {
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            {
                return false;
            }

            if (string.Equals(left, right, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return string.Equals(SceneUtilityEx.ToSceneName(left), SceneUtilityEx.ToSceneName(right), StringComparison.OrdinalIgnoreCase);
        }
    }
}
