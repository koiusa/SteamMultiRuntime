using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;

namespace Koiusa.SteamMultiRuntime
{
    public sealed class LoadingSplashPresenter
    {
        private readonly MonoBehaviour owner;
        private readonly LoadingSplashSettings splashSettings;

        private GameObject splashUiObject;
        private UIDocument splashUiDocument;
        private PanelSettings runtimeSplashPanelSettings;
        private VisualElement splashOverlayElement;
        private VisualElement splashImageElement;
        private Label splashMessageElement;

        public LoadingSplashPresenter(MonoBehaviour owner, LoadingSplashSettings splashSettings)
        {
            this.owner = owner;
            this.splashSettings = splashSettings;
        }

        public void Dispose()
        {
            if (splashUiObject != null)
            {
                Object.Destroy(splashUiObject);
                splashUiObject = null;
            }

            if (runtimeSplashPanelSettings != null)
            {
                Object.Destroy(runtimeSplashPanelSettings);
                runtimeSplashPanelSettings = null;
            }
        }

        public void Show()
        {
            EnsureSplashUi();
            RefreshSplashUi();

            if (splashOverlayElement != null)
            {
                splashOverlayElement.style.display = DisplayStyle.Flex;
            }
        }

        public void Hide()
        {
            if (splashOverlayElement != null)
            {
                splashOverlayElement.style.display = DisplayStyle.None;
            }
        }

        public async Task WaitForCharacterReadyAsync(NetworkManager networkManager, int visibilityVersion, System.Func<int> getVisibilityVersion)
        {
            if (networkManager == null || !networkManager.IsListening)
            {
                return;
            }

            var playerPrefab = networkManager.NetworkConfig.PlayerPrefab;
            if (playerPrefab == null)
            {
                return;
            }

            var prefabCharacterLoader = playerPrefab.GetComponent<ICharacterPrefabLoader>();
            if (prefabCharacterLoader == null)
            {
                return;
            }

            while (owner != null && owner.isActiveAndEnabled && visibilityVersion == getVisibilityVersion())
            {
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

                if (runtimeCharacterLoader.IsLoaded)
                {
                    return;
                }

                await Task.Yield();
            }
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
                Debug.LogError("[LoadingSplashPresenter] Splash layout asset is not assigned in settings.", owner);
                return;
            }

            var panelSettings = ResolveSplashPanelSettings();
            if (panelSettings == null)
            {
                Debug.LogError("[LoadingSplashPresenter] PanelSettings could not be resolved for splash UI.", owner);
                return;
            }

            splashUiObject = new GameObject("LoadingSplash");
            Object.DontDestroyOnLoad(splashUiObject);

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

            var existingDocument = Object.FindFirstObjectByType<UIDocument>();
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
                Debug.LogError("[LoadingSplashPresenter] Splash layout is missing required elements.", owner);
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
    }
}
