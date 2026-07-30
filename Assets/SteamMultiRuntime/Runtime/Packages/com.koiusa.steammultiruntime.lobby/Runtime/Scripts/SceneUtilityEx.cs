using System.IO;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Koiusa.SteamMultiRuntime
{
    public static class SceneUtilityEx
    {
        public static Task WaitForOperationAsync(AsyncOperation operation, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (operation == null || operation.isDone)
            {
                return Task.CompletedTask;
            }

            var completionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            CancellationTokenRegistration cancellationRegistration = default;

            void OnCompleted(AsyncOperation completedOperation)
            {
                operation.completed -= OnCompleted;
                cancellationRegistration.Dispose();
                completionSource.TrySetResult(true);
            }

            void OnCancelled()
            {
                operation.completed -= OnCompleted;
                completionSource.TrySetCanceled(cancellationToken);
            }

            operation.completed += OnCompleted;
            if (cancellationToken.CanBeCanceled)
                cancellationRegistration = cancellationToken.Register(OnCancelled);

            if (operation.isDone)
            {
                operation.completed -= OnCompleted;
                cancellationRegistration.Dispose();
                return Task.CompletedTask;
            }

            return completionSource.Task;
        }

        public static Task WaitForFrameRenderedAsync()
        {
            var completionSource = new TaskCompletionSource<bool>();

            void OnEndContextRendering(ScriptableRenderContext _, List<Camera> __)
            {
                RenderPipelineManager.endContextRendering -= OnEndContextRendering;
                completionSource.TrySetResult(true);
            }

            RenderPipelineManager.endContextRendering += OnEndContextRendering;
            return completionSource.Task;
        }

        public static bool CanLoadScene(string sceneReference)
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

        public static Scene GetLoadedScene(string sceneReference)
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

        public static bool IsScenePath(string sceneReference)
        {
            return sceneReference.Contains("/") || sceneReference.EndsWith(".unity", System.StringComparison.OrdinalIgnoreCase);
        }

        public static string ToSceneName(string sceneReference)
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
