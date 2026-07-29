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
        }

        private void OnDisable()
        {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        }

        private void OnActiveSceneChanged(Scene previousScene, Scene nextScene) => Hide();

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
        }

        public void Hide()
        {
            if (keyConfigUiDocument == null) return;
            keyConfigUiDocument.gameObject.SetActive(false);
        }
    }
}
