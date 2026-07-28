using UnityEngine;
using UnityEngine.SceneManagement;

namespace Koiusa.Keyconfig.Runtime
{
    [DisallowMultipleComponent]
    public sealed class KeyConfigMenuToggle : MonoBehaviour
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

        private void OnActiveSceneChanged(Scene previousScene, Scene nextScene) => Hide();

        private void OnUiClosed() => Cursor.visible = false;

        public void Toggle()
        {
            if (keyConfigUiDocument == null) return;
            if (keyConfigUiDocument.gameObject.activeSelf) Hide();
            else Show();
        }

        public void Show()
        {
            if (keyConfigUiDocument == null) return;
            keyConfigUiDocument.gameObject.SetActive(true);
            Cursor.visible = true;
        }

        public void Hide()
        {
            if (keyConfigUiDocument == null) return;
            keyConfigUiDocument.gameObject.SetActive(false);
            Cursor.visible = false;
        }
    }
}
