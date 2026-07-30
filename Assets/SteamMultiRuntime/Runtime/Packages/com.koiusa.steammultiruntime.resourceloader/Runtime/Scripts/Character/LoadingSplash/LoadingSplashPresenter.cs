using System.Threading.Tasks;
using Koiusa.SteamMultiRuntime.Core;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;
using Koiusa.SteamMultiRuntime.Localization;

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
            GameLocalization.LocaleChanged += RefreshSplashUi;
        }

        public void Dispose()
        {
            GameLocalization.LocaleChanged -= RefreshSplashUi;
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

                if (runtimeCharacterLoader.IsCharacterReady)
                {
                    return;
                }

                await Task.Yield();
            }
        }

        public async Task WaitForLocalCharacterReadyAsync(ILocalPlayerProvider localPlayerProvider, int visibilityVersion, System.Func<int> getVisibilityVersion)
        {
            while (owner != null && owner.isActiveAndEnabled && visibilityVersion == getVisibilityVersion())
            {
                var playerObject = localPlayerProvider?.LocalPlayerObject;

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
            splashMessageElement.text = GameLocalization.Get(
                string.IsNullOrWhiteSpace(messageText) ? "loading.default" : messageText);
            splashMessageElement.style.display = string.IsNullOrWhiteSpace(messageText)
                ? DisplayStyle.None
                : DisplayStyle.Flex;
        }
    }
}
