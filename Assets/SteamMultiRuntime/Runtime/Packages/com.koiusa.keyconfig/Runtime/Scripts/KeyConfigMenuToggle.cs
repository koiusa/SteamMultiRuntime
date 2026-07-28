using Koiusa.Input;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Koiusa.Keyconfig.Runtime
{
    [DisallowMultipleComponent]
    public sealed class KeyConfigMenuToggle : MonoBehaviour
    {
        [SerializeField] private KeyConfigUiDocument keyConfigUiDocument;
        [SerializeField] private InputActionsConfig inputActionsConfig;

        private InputActionBinding toggleBinding;

        private void OnEnable()
        {
            toggleBinding = InputActionBinding.Bind(
                inputActionsConfig?.FindAction("UI/MenuToggle"),
                OnTogglePerformed);
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            if (keyConfigUiDocument != null) keyConfigUiDocument.Closed += OnUiClosed;
        }

        private void OnDisable()
        {
            toggleBinding?.Dispose();
            toggleBinding = null;
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            if (keyConfigUiDocument != null) keyConfigUiDocument.Closed -= OnUiClosed;
        }

        private void OnTogglePerformed(InputAction.CallbackContext context) => Toggle();

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
