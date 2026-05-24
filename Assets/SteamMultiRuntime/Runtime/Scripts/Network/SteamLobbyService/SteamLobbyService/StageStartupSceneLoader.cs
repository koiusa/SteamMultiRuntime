using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Koiusa.SteamMultiRuntime.Network
{
    internal static class StageStartupSceneLoader
    {
        public static string ResolveStartupSceneReference(IStartupStageSceneLoaderContext context, Object owner, string logPrefix)
        {
            if (context == null || context.StageSceneList == null || context.StageSceneList.sceneNames == null || context.StageSceneList.sceneNames.Length == 0)
            {
                return string.Empty;
            }

            if (context.StartupStageSceneIndex < 0 || context.StartupStageSceneIndex >= context.StageSceneList.sceneNames.Length)
            {
                Debug.LogError($"[{logPrefix}] startupStageSceneIndex '{context.StartupStageSceneIndex}' is out of range.", owner);
                return string.Empty;
            }

            var sceneName = context.StageSceneList.sceneNames[context.StartupStageSceneIndex];
            var resolved = context.StageSceneList.ResolveSceneReference(sceneName);
            return !string.IsNullOrWhiteSpace(resolved) ? resolved : sceneName;
        }

        public static async Task<bool> LoadStartupSceneAsync(IStartupStageSceneLoaderContext context, Object owner, string logPrefix, System.Action<string> logAction = null)
        {
            var startupScene = ResolveStartupSceneReference(context, owner, logPrefix);
            if (string.IsNullOrWhiteSpace(startupScene))
            {
                logAction?.Invoke("Startup scene is empty. Skip scene loading.");
                return true;
            }

            if (!SceneUtilityEx.CanLoadScene(startupScene))
            {
                Debug.LogError($"[{logPrefix}] Scene '{startupScene}' is not in Build Settings.", owner);
                return false;
            }

            var loadedScene = SceneUtilityEx.GetLoadedScene(startupScene);
            if (loadedScene.IsValid() && loadedScene.isLoaded)
            {
                if (context.SetLoadedSceneAsActive)
                {
                    SceneManager.SetActiveScene(loadedScene);
                }

                logAction?.Invoke($"Startup scene already loaded: {startupScene}");
                return true;
            }

            var operation = SceneManager.LoadSceneAsync(startupScene, context.SceneLoadMode);
            if (operation == null)
            {
                Debug.LogError($"[{logPrefix}] Failed to start loading scene '{startupScene}'.", owner);
                return false;
            }

            await SceneUtilityEx.WaitForOperationAsync(operation);

            if (context.SetLoadedSceneAsActive)
            {
                var scene = SceneUtilityEx.GetLoadedScene(startupScene);
                if (scene.IsValid() && scene.isLoaded)
                {
                    SceneManager.SetActiveScene(scene);
                }
            }

            logAction?.Invoke($"Startup scene loaded: {startupScene}");
            return true;
        }
    }
}
