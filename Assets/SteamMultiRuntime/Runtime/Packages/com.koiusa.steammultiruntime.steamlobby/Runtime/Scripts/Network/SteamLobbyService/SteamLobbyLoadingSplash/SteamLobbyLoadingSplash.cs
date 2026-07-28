using System;
using TNRD;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;
using Koiusa.SteamMultiRuntime.Localization;

namespace Koiusa.SteamMultiRuntime
{
    [DisallowMultipleComponent]
    public class SteamLobbyLoadingSplash : MonoBehaviour
    {
        [SerializeField] private SerializableInterface<ISteamLobbySceneLoader> sceneLoader;
        [SerializeField] private NetworkManager networkManager;
        [SerializeField] private LoadingSplashSettings splashSettings;
        [SerializeField] private bool showSplashDuringSceneLoad = true;

        private bool isSubscribed;
        private ISteamLobbySceneLoader subscribedSceneLoader;
        private Network.ILobbyExitEventSource subscribedExitEventSource;
        private bool isLobbyExitInProgress;

        private ISteamLobbySceneLoader SceneLoader => sceneLoader != null ? sceneLoader.Value : null;
        private GameObject splashUiObject;
        private UIDocument splashUiDocument;
        private PanelSettings runtimeSplashPanelSettings;
        private VisualElement splashOverlayElement;
        private VisualElement splashImageElement;
        private Label splashMessageElement;

        private void Awake()
        {
            ResolveSceneLoader();
            ResolveNetworkManager();
            EnsureSplashUi();
            RefreshSplashUi();
            HideSplashUi();
        }

        private void OnEnable()
        {
            ResolveSceneLoader();
            ResolveNetworkManager();
            SubscribeLoaderEvents();
            SubscribeExitEvents();
            SceneManager.sceneLoaded += OnSceneLoaded;
            GameLocalization.LocaleChanged += RefreshSplashUi;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            GameLocalization.LocaleChanged -= RefreshSplashUi;
            UnsubscribeLoaderEvents();
            UnsubscribeExitEvents();
            HideSplashUi();
        }

        private void OnDestroy()
        {
            UnsubscribeLoaderEvents();
            UnsubscribeExitEvents();

            if (splashUiObject != null)
            {
                Destroy(splashUiObject);
                splashUiObject = null;
            }

            if (runtimeSplashPanelSettings != null)
            {
                Destroy(runtimeSplashPanelSettings);
                runtimeSplashPanelSettings = null;
            }
        }

        private void ResolveSceneLoader()
        {
            if (SceneLoader != null)
            {
                return;
            }

            var loader = GetComponent<ISteamLobbySceneLoader>()
                ?? GetComponentInChildren<ISteamLobbySceneLoader>(true)
                ?? FindFirstObjectByType<SteamLobbySceneLoader>() as ISteamLobbySceneLoader
                ?? FindFirstObjectByType<Network.SteamLobbyDedicatedServer>(FindObjectsInactive.Include) as ISteamLobbySceneLoader
                ?? FindFirstObjectByType<LocalSceneFlowLoader>(FindObjectsInactive.Include) as ISteamLobbySceneLoader;

            if (loader != null)
            {
                sceneLoader = new SerializableInterface<ISteamLobbySceneLoader>(loader);
            }
        }

        private void ResolveNetworkManager()
        {
            if (networkManager != null)
            {
                return;
            }

            networkManager = NetworkManager.Singleton;
            if (networkManager == null)
            {
                networkManager = FindFirstObjectByType<NetworkManager>();
            }
        }

        private void SubscribeLoaderEvents()
        {
            var loader = SceneLoader;
            if (loader == null)
            {
                return;
            }

            if (isSubscribed && subscribedSceneLoader == loader)
            {
                return;
            }

            UnsubscribeLoaderEvents();

            loader.LoadingStarted += OnLoadingStarted;
            loader.LoadingFinished += OnLoadingFinished;
            subscribedSceneLoader = loader;
            isSubscribed = true;
        }

        private void UnsubscribeLoaderEvents()
        {
            if (!isSubscribed || subscribedSceneLoader == null)
            {
                isSubscribed = false;
                subscribedSceneLoader = null;
                return;
            }

            subscribedSceneLoader.LoadingStarted -= OnLoadingStarted;
            subscribedSceneLoader.LoadingFinished -= OnLoadingFinished;
            isSubscribed = false;
            subscribedSceneLoader = null;
        }

        private void SubscribeExitEvents()
        {
            var source = GetComponentInParent<Network.ILobbyExitEventSource>()
                ?? FindFirstObjectByType<SteamLobbyService>(FindObjectsInactive.Include) as Network.ILobbyExitEventSource;
            if (source == null || subscribedExitEventSource == source)
            {
                return;
            }

            UnsubscribeExitEvents();
            source.LobbyExitStarted += OnLobbyExitStarted;
            source.LobbyExitFinished += OnLobbyExitFinished;
            subscribedExitEventSource = source;

            // An exit can begin before this component subscribes or while scenes are
            // replacing UI documents. Recover the current state after subscribing so
            // the start notification cannot be lost.
            if (source.IsLobbyExitInProgress)
            {
                OnLobbyExitStarted();
            }
            else
            {
                isLobbyExitInProgress = false;
            }
        }

        private void UnsubscribeExitEvents()
        {
            if (subscribedExitEventSource == null)
            {
                return;
            }

            subscribedExitEventSource.LobbyExitStarted -= OnLobbyExitStarted;
            subscribedExitEventSource.LobbyExitFinished -= OnLobbyExitFinished;
            subscribedExitEventSource = null;
            isLobbyExitInProgress = false;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ResolveSceneLoader();
            ResolveNetworkManager();
            SubscribeLoaderEvents();
            SubscribeExitEvents();
        }

        private void OnLobbyExitStarted()
        {
            isLobbyExitInProgress = true;
            OnLoadingStarted();
        }

        private void OnLobbyExitFinished()
        {
            isLobbyExitInProgress = false;
            OnLoadingFinished();
        }

        private void OnLoadingStarted()
        {
            if (!showSplashDuringSceneLoad)
            {
                return;
            }

            EnsureSplashUi();
            RefreshSplashUi();
            ShowSplashUi();
        }

        private void OnLoadingFinished()
        {
            if (!showSplashDuringSceneLoad || isLobbyExitInProgress)
            {
                return;
            }

            HideSplashUi();
        }

        private void EnsureSplashUi()
        {
            if (splashUiObject != null)
            {
                return;
            }

            var splashLayoutAsset = splashSettings != null ? splashSettings.SplashLayoutAsset : null;
            if (splashLayoutAsset == null)
            {
                Debug.LogError("[SteamLobbyLoadingSplash] Splash layout asset is not assigned in settings.");
                return;
            }

            var panelSettings = ResolveSplashPanelSettings();
            if (panelSettings == null)
            {
                Debug.LogError("[SteamLobbyLoadingSplash] PanelSettings could not be resolved for splash UI.");
                return;
            }

            splashUiObject = new GameObject("SteamLobbyLoadingSplash");
            DontDestroyOnLoad(splashUiObject);

            splashUiDocument = splashUiObject.AddComponent<UIDocument>();
            splashUiDocument.panelSettings = panelSettings;
            splashUiDocument.sortingOrder = short.MaxValue;

            BuildSplashUi(splashUiDocument.rootVisualElement, splashLayoutAsset);
        }

        private PanelSettings ResolveSplashPanelSettings()
        {
            if (splashSettings != null && splashSettings.PanelSettings != null)
            {
                return splashSettings.PanelSettings;
            }

            var existingDocument = FindFirstObjectByType<UIDocument>();
            if (existingDocument != null && existingDocument.panelSettings != null)
            {
                return existingDocument.panelSettings;
            }

            runtimeSplashPanelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            return runtimeSplashPanelSettings;
        }

        private void BuildSplashUi(VisualElement root, VisualTreeAsset splashLayoutAsset)
        {
            root.Clear();
            splashLayoutAsset.CloneTree(root);

            splashOverlayElement = root.Q<VisualElement>("splash-overlay");
            splashImageElement = root.Q<VisualElement>("splash-image");
            splashMessageElement = root.Q<Label>("splash-message");

            if (splashOverlayElement == null || splashImageElement == null || splashMessageElement == null)
            {
                Debug.LogError("[SteamLobbyLoadingSplash] Splash layout is missing required elements.");
            }
        }

        private void RefreshSplashUi()
        {
            if (splashOverlayElement == null || splashImageElement == null || splashMessageElement == null)
            {
                return;
            }

            var splashTexture = splashSettings != null ? splashSettings.SplashImageTexture : null;

            if (splashTexture != null)
            {
                splashImageElement.style.display = DisplayStyle.Flex;
                splashImageElement.style.backgroundImage = new StyleBackground(splashTexture);
            }
            else
            {
                splashImageElement.style.display = DisplayStyle.None;
            }

            var messageText = splashSettings != null ? splashSettings.SplashMessage : "Loading...";
            splashMessageElement.text = GameLocalization.Get(string.IsNullOrWhiteSpace(messageText) ? "loading.default" : messageText);
            splashMessageElement.style.display = string.IsNullOrWhiteSpace(messageText)
                ? DisplayStyle.None
                : DisplayStyle.Flex;
        }

        private void ShowSplashUi()
        {
            if (splashOverlayElement != null)
            {
                splashOverlayElement.style.display = DisplayStyle.Flex;
            }
        }

        private void HideSplashUi()
        {
            if (splashOverlayElement != null)
            {
                splashOverlayElement.style.display = DisplayStyle.None;
            }
        }
    }
}
