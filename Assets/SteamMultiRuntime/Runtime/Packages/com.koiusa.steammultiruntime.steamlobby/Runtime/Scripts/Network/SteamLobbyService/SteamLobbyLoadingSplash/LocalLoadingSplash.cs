using System.Collections.Generic;
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
        private readonly HashSet<ILoadingSplashEventSource> subscribedSources = new HashSet<ILoadingSplashEventSource>();

        private void Awake()
        {
            EnsureSplashPresenter();
        }

        private void OnEnable()
        {
            EnsureSplashPresenter();
            ResolveSources();
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

        private void ResolveSources()
        {
            TrySubscribe(sceneLoader?.Value);

            foreach (var src in GetComponentsInChildren<ILoadingSplashEventSource>(true))
            {
                TrySubscribe(src);
            }

            TrySubscribe(FindFirstObjectByType<LocalSceneFlowLoader>(FindObjectsInactive.Include) as ILoadingSplashEventSource);
            TrySubscribe(FindFirstObjectByType<LocalStageSelectUIDocument>(FindObjectsInactive.Include) as ILoadingSplashEventSource);
        }

        private void TrySubscribe(ILoadingSplashEventSource source)
        {
            if (source == null || !subscribedSources.Add(source))
            {
                return;
            }

            source.LoadingStarted += OnLoadingStarted;
            source.LoadingFinished += OnLoadingFinished;
        }

        private void EnsureSplashPresenter()
        {
            splashPresenter ??= new LoadingSplashPresenter(this, splashSettings);
        }

        private void UnsubscribeLoaderEvents()
        {
            foreach (var source in subscribedSources)
            {
                source.LoadingStarted -= OnLoadingStarted;
                source.LoadingFinished -= OnLoadingFinished;
            }
            subscribedSources.Clear();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ResolveSources();
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

    }
}
