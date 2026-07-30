using System.Threading;
using System.Threading.Tasks;
using Koiusa.SteamMultiRuntime.Core;
using Koiusa.SteamMultiRuntime.Network;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Koiusa.SteamMultiRuntime.Tests
{
    public sealed class SceneLifetimeTests
    {
        [Test]
        public void WaitForOperationAsync_WithPreCancelledToken_ThrowsBeforeWaiting()
        {
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            Assert.Throws<System.OperationCanceledException>(
                () => SceneUtilityEx.WaitForOperationAsync(null, cancellation.Token));
        }

        [Test]
        public async Task LocalStartupSceneLoader_PreCancelledRequest_StillPairsLoadingEvents()
        {
            var gameObject = new GameObject("LocalStartupSceneLoader_Test");
            gameObject.SetActive(false);
            var loader = gameObject.AddComponent<LocalStartupSceneLoader>();
            var startedCount = 0;
            var finishedCount = 0;
            loader.LoadingStarted += () => startedCount++;
            loader.LoadingFinished += () => finishedCount++;
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            try
            {
                await AssertThrowsCancellationAsync(
                    () => loader.LoadStartupSceneAsync(cancellation.Token));

                Assert.That(startedCount, Is.EqualTo(1));
                Assert.That(finishedCount, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public async Task LocalStartupSceneLoader_EmptySceneConfiguration_CompletesWithPairedLoadingEvents()
        {
            var gameObject = new GameObject("LocalStartupSceneLoader_Test");
            gameObject.SetActive(false);
            var loader = gameObject.AddComponent<LocalStartupSceneLoader>();
            var startedCount = 0;
            var finishedCount = 0;
            loader.LoadingStarted += () => startedCount++;
            loader.LoadingFinished += () => finishedCount++;

            try
            {
                var loaded = await loader.LoadStartupSceneAsync();

                Assert.That(loaded, Is.True);
                Assert.That(startedCount, Is.EqualTo(1));
                Assert.That(finishedCount, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public async Task ApplyLoadedSceneCameraSettings_DisablesStageCameraAndPreservesInfrastructureCamera()
        {
            var temporaryScene = SceneManager.CreateScene($"SceneCameraPolicy_{System.Guid.NewGuid():N}");
            var stageCameraObject = new GameObject("StageCamera");
            var stageCamera = stageCameraObject.AddComponent<Camera>();
            var stageListener = stageCameraObject.AddComponent<AudioListener>();
            SceneManager.MoveGameObjectToScene(stageCameraObject, temporaryScene);

            var infrastructureCameraObject = new GameObject("InfrastructureCamera");
            var infrastructureCamera = infrastructureCameraObject.AddComponent<Camera>();
            infrastructureCameraObject.AddComponent<PreservedTestCameraMarker>();
            SceneManager.MoveGameObjectToScene(infrastructureCameraObject, temporaryScene);

            try
            {
                SceneLoadUtility.ApplyLoadedSceneCameraSettings(
                    temporaryScene,
                    disableCamerasInLoadedScenes: true);

                Assert.That(stageCamera.enabled, Is.False);
                Assert.That(stageCameraObject.activeSelf, Is.False);
                Assert.That(stageListener.enabled, Is.False);
                Assert.That(infrastructureCamera.enabled, Is.True);
                Assert.That(infrastructureCameraObject.activeSelf, Is.True);
            }
            finally
            {
                var unloadOperation = SceneManager.UnloadSceneAsync(temporaryScene);
                await SceneUtilityEx.WaitForOperationAsync(unloadOperation);
            }
        }

        private static async Task AssertThrowsCancellationAsync(System.Func<Task<bool>> action)
        {
            try
            {
                await action();
                Assert.Fail("Expected OperationCanceledException.");
            }
            catch (System.OperationCanceledException)
            {
            }
        }

    }

    public sealed class PreservedTestCameraMarker : MonoBehaviour, IPreservedLoadedSceneCamera
    {
    }
}
