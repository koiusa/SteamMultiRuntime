using UnityEngine;
using UnityEngine.SceneManagement;
using Koiusa.UI.Common;

namespace Koiusa.Keyconfig.Runtime
{
    [DisallowMultipleComponent]
    public sealed class KeyConfigMenuToggle : MonoBehaviour, IUiMenu
    {
        [SerializeField] private KeyConfigUiDocument keyConfigUiDocument;

        public bool IsVisible => keyConfigUiDocument != null && keyConfigUiDocument.gameObject.activeSelf;

        private void OnEnable()
        {
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            if (keyConfigUiDocument != null) keyConfigUiDocument.Closed += OnUiClosed;
        }

        private void OnDisable()
        {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            if (keyConfigUiDocument != null) keyConfigUiDocument.Closed -= OnUiClosed;
        }

        private void OnActiveSceneChanged(Scene previousScene, Scene nextScene) => UiMenuNavigator.CloseAll();

        private void OnUiClosed() => UiMenuNavigator.Back(this);

        public void Toggle()
        {
            UiMenuNavigator.ToggleRoot(this);
        }

        public void Show() => UiMenuNavigator.OpenRoot(this);

        public void Hide() => UiMenuNavigator.Close(this);

        public void Activate()
        {
            if (keyConfigUiDocument == null) return;
            keyConfigUiDocument.gameObject.SetActive(true);
        }

        public void Deactivate()
        {
            if (keyConfigUiDocument == null) return;
            keyConfigUiDocument.gameObject.SetActive(false);
        }

        public void FocusInitial() => keyConfigUiDocument?.FocusInitial();
    }
}
