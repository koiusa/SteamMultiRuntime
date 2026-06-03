using System.Threading.Tasks;
using Koiusa.SteamMultiRuntime.Network;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Koiusa.SteamMultiRuntime
{
    [DisallowMultipleComponent]
    public class LocalLoadingSplash : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour sceneLoaderBehaviour;
        [SerializeField] private LoadingSplashSettings splashSettings;
        [SerializeField] private bool showSplashDuringSceneLoad = true;

        private LoadingSplashPresenter splashPresenter;
        private int splashVisibilityVersion;
        private const float SceneReadyWaitTimeoutSeconds = 15f;
        private ILoadingSplashEventSource sceneLoader;

        private void Awake()
        {
            ResolveSceneLoader();
            EnsureSplashPresenter();
        }

        private void OnEnable()
        {
            ResolveSceneLoader();
            EnsureSplashPresenter();
            SubscribeLoaderEvents();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            splashVisibilityVersion++;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            UnsubscribeLoaderEvents();
            splashPresenter?.Hide();
        }

        private void OnDestroy()
        {
            UnsubscribeLoaderEvents();
            splashPresenter?.Dispose();
            splashPresenter = null;
        }

        private void ResolveSceneLoader()
        {
            if (sceneLoader != null)
            {
                return;
            }

            if (sceneLoaderBehaviour != null)
            {
                sceneLoader = sceneLoaderBehaviour as ILoadingSplashEventSource;
                return;
            }

            sceneLoader = GetComponent<ILoadingSplashEventSource>()
                ?? GetComponentInChildren<ILoadingSplashEventSource>(true)
                ?? FindFirstObjectByType<LocalStartupSceneLoader>(FindObjectsInactive.Include);
        }

        private void EnsureSplashPresenter()
        {
            splashPresenter ??= new LoadingSplashPresenter(this, splashSettings);
        }

        private void SubscribeLoaderEvents()
        {
            if (sceneLoader == null)
            {
                return;
            }

            sceneLoader.LoadingStarted -= OnLoadingStarted;
            sceneLoader.LoadingFinished -= OnLoadingFinished;
            sceneLoader.LoadingStarted += OnLoadingStarted;
            sceneLoader.LoadingFinished += OnLoadingFinished;
        }

        private void UnsubscribeLoaderEvents()
        {
            if (sceneLoader == null)
            {
                return;
            }

            sceneLoader.LoadingStarted -= OnLoadingStarted;
            sceneLoader.LoadingFinished -= OnLoadingFinished;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ResolveSceneLoader();
            SubscribeLoaderEvents();
        }

        private void OnLoadingStarted()
        {
            if (!showSplashDuringSceneLoad)
            {
                return;
            }

            splashVisibilityVersion++;
            EnsureSplashPresenter();
            splashPresenter?.Show();
        }

        private void OnLoadingFinished()
        {
            if (!showSplashDuringSceneLoad)
            {
                return;
            }

            var visibilityVersion = splashVisibilityVersion;
            _ = HideSplashWhenSceneReadyAsync(visibilityVersion);
        }

        private async Task HideSplashWhenSceneReadyAsync(int visibilityVersion)
        {
            await WaitForSceneReadyAsync(visibilityVersion);

            if (!isActiveAndEnabled || visibilityVersion != splashVisibilityVersion)
            {
                return;
            }

            splashPresenter?.Hide();
        }

        private async Task WaitForSceneReadyAsync(int visibilityVersion)
        {
            var startedAt = Time.realtimeSinceStartup;

            while (isActiveAndEnabled && visibilityVersion == splashVisibilityVersion)
            {
                if (IsSceneReadyForSplashHide())
                {
                    return;
                }

                if (Time.realtimeSinceStartup - startedAt >= SceneReadyWaitTimeoutSeconds)
                {
                    return;
                }

                await Task.Yield();
            }
        }

        private bool IsSceneReadyForSplashHide()
        {
            var activeScene = SceneManager.GetActiveScene();
            return activeScene.IsValid() && activeScene.isLoaded;
        }
    }
}
