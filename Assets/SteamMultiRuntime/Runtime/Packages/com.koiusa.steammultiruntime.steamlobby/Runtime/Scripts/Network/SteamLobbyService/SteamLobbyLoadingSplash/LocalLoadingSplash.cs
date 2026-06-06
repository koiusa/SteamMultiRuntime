using System.Threading.Tasks;
using Koiusa.SteamMultiRuntime.Network;
using TNRD;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Koiusa.SteamMultiRuntime
{
    [DisallowMultipleComponent]
    public class LocalLoadingSplash : MonoBehaviour
    {
        [SerializeField] private SerializableInterface<ILoadingSplashEventSource> sceneLoader;
        [SerializeField] private LoadingSplashSettings splashSettings;
        [SerializeField] private bool showSplashDuringSceneLoad = true;

        private LoadingSplashPresenter splashPresenter;
        private int splashVisibilityVersion;
        private const float SceneReadyWaitTimeoutSeconds = 15f;
        private ILoadingSplashEventSource resolvedSceneLoader;
        private bool isSubscribed;

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
            if (resolvedSceneLoader != null)
            {
                return;
            }

            var loader = sceneLoader != null ? sceneLoader.Value : null;
            if (loader != null)
            {
                resolvedSceneLoader = loader;
                return;
            }

            resolvedSceneLoader = GetComponent<ILoadingSplashEventSource>()
                ?? GetComponentInChildren<ILoadingSplashEventSource>(true)
                ?? FindFirstObjectByType<LocalStartupSceneLoader>(FindObjectsInactive.Include);
        }

        private void EnsureSplashPresenter()
        {
            splashPresenter ??= new LoadingSplashPresenter(this, splashSettings);
        }

        private void SubscribeLoaderEvents()
        {
            var loader = resolvedSceneLoader;
            if (loader == null)
            {
                return;
            }

            if (isSubscribed)
            {
                return;
            }

            loader.LoadingStarted += OnLoadingStarted;
            loader.LoadingFinished += OnLoadingFinished;
            isSubscribed = true;
        }

        private void UnsubscribeLoaderEvents()
        {
            if (!isSubscribed || resolvedSceneLoader == null)
            {
                isSubscribed = false;
                return;
            }

            resolvedSceneLoader.LoadingStarted -= OnLoadingStarted;
            resolvedSceneLoader.LoadingFinished -= OnLoadingFinished;
            isSubscribed = false;
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
            _ = HideSplashWhenCharacterReadyAsync(visibilityVersion);
        }

        private async Task HideSplashWhenCharacterReadyAsync(int visibilityVersion)
        {
            await WaitForCharacterReadyAsync(visibilityVersion);
            await WaitForSceneReadyAsync(visibilityVersion);

            if (!isActiveAndEnabled || visibilityVersion != splashVisibilityVersion)
            {
                return;
            }

            splashPresenter?.Hide();
        }

        private async Task WaitForCharacterReadyAsync(int visibilityVersion)
        {
            // Get LocalManager singleton via reflection to avoid direct asmdef reference
            var localManagerType = System.Type.GetType("Koiusa.SteamMultiRuntime.LocalManager, Koiusa.SteamMultiRuntime.Integration.Runtime");
            if (localManagerType != null)
            {
                var singletonProperty = localManagerType.GetProperty("Singleton");
                if (singletonProperty != null)
                {
                    var localManager = singletonProperty.GetValue(null);
                    if (localManager != null)
                    {
                        await splashPresenter.WaitForLocalCharacterReadyAsync(localManager, visibilityVersion, () => splashVisibilityVersion);
                    }
                }
            }
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
