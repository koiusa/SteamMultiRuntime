using System;
using System.Threading.Tasks;
using TNRD;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;

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

        private ISteamLobbySceneLoader SceneLoader => sceneLoader != null ? sceneLoader.Value : null;
        private GameObject splashUiObject;
        private UIDocument splashUiDocument;
        private PanelSettings runtimeSplashPanelSettings;
        private VisualElement splashOverlayElement;
        private VisualElement splashImageElement;
        private Label splashMessageElement;
        private int splashVisibilityVersion;
        private const float SceneReadyWaitTimeoutSeconds = 15f;

        private void Awake()
        {
            ResolveSceneLoader();
            ResolveNetworkManager();
        }

        private void OnEnable()
        {
            ResolveSceneLoader();
            ResolveNetworkManager();
            SubscribeLoaderEvents();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            splashVisibilityVersion++;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            UnsubscribeLoaderEvents();
            HideSplashUi();
        }

        private void OnDestroy()
        {
            UnsubscribeLoaderEvents();

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

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ResolveSceneLoader();
            ResolveNetworkManager();
            SubscribeLoaderEvents();
        }

        private void OnLoadingStarted()
        {
            if (!showSplashDuringSceneLoad)
            {
                return;
            }

            splashVisibilityVersion++;
            EnsureSplashUi();
            RefreshSplashUi();
            ShowSplashUi();
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

            HideSplashUi();
        }

        private async Task WaitForCharacterReadyAsync(int visibilityVersion)
        {
            ResolveNetworkManager();

            while (isActiveAndEnabled && visibilityVersion == splashVisibilityVersion)
            {
                ResolveNetworkManager();

                if (networkManager == null || !networkManager.IsListening)
                {
                    return;
                }

                var playerObject = networkManager.LocalClient?.PlayerObject;
                if (playerObject == null)
                {
                    await Task.Yield();
                    continue;
                }

                var runtimeCharacterLoader = playerObject.GetComponent<ICharacterPrefabLoader>();
                if (runtimeCharacterLoader == null)
                {
                    return;
                }

                if (runtimeCharacterLoader.IsCharacterReady)
                {
                    return;
                }

                await Task.Yield();
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
            if (!activeScene.IsValid() || !activeScene.isLoaded)
            {
                return false;
            }

            ResolveSceneLoader();
            var loader = SceneLoader;
            if (loader is ISteamLobbyTransitionScope transitionScope && transitionScope.IsLobbyTransitionInProgress)
            {
                return false;
            }

            ResolveNetworkManager();
            if (networkManager == null || !networkManager.IsListening)
            {
                return true;
            }

            if (loader == null || string.IsNullOrWhiteSpace(loader.LobbySceneName))
            {
                return true;
            }

            var lobbySceneRef = loader.LobbySceneName;
            if (lobbySceneRef.Contains("/") || lobbySceneRef.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
            {
                return string.Equals(activeScene.path, lobbySceneRef, StringComparison.OrdinalIgnoreCase);
            }

            return string.Equals(activeScene.name, lobbySceneRef, StringComparison.Ordinal);
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
            splashMessageElement.text = string.IsNullOrWhiteSpace(messageText) ? "Loading..." : messageText;
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
