using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Koiusa.SteamMultiRuntime
{
    internal static class SceneUtilityEx
    {
        internal static Task WaitForOperationAsync(AsyncOperation operation)
        {
            if (operation == null || operation.isDone)
            {
                return Task.CompletedTask;
            }

            var completionSource = new TaskCompletionSource<bool>();

            void OnCompleted(AsyncOperation completedOperation)
            {
                operation.completed -= OnCompleted;
                completionSource.TrySetResult(true);
            }

            operation.completed += OnCompleted;

            if (operation.isDone)
            {
                operation.completed -= OnCompleted;
                return Task.CompletedTask;
            }

            return completionSource.Task;
        }

        internal static bool CanLoadScene(string sceneReference)
        {
            if (string.IsNullOrWhiteSpace(sceneReference))
            {
                return false;
            }

            if (IsScenePath(sceneReference))
            {
                return SceneUtility.GetBuildIndexByScenePath(sceneReference) >= 0;
            }

            return Application.CanStreamedLevelBeLoaded(sceneReference);
        }

        internal static Scene GetLoadedScene(string sceneReference)
        {
            if (string.IsNullOrWhiteSpace(sceneReference))
            {
                return default;
            }

            if (IsScenePath(sceneReference))
            {
                var byPath = SceneManager.GetSceneByPath(sceneReference);
                if (byPath.IsValid())
                {
                    return byPath;
                }
            }

            return SceneManager.GetSceneByName(ToSceneName(sceneReference));
        }

        internal static bool IsScenePath(string sceneReference)
        {
            return sceneReference.Contains("/") || sceneReference.EndsWith(".unity", System.StringComparison.OrdinalIgnoreCase);
        }

        internal static string ToSceneName(string sceneReference)
        {
            if (string.IsNullOrWhiteSpace(sceneReference))
            {
                return string.Empty;
            }

            var normalized = sceneReference.Replace('\\', '/');
            return Path.GetFileNameWithoutExtension(normalized);
        }
    }
}
