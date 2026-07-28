using Koiusa.Input;
using Koiusa.Keyconfig.Runtime;
using Koiusa.SteamMultiRuntime.Character.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Koiusa.SteamMultiRuntime
{
    [DisallowMultipleComponent]
    public sealed class PauseMenuController : MonoBehaviour
    {
        [SerializeField] private GameObject pauseMenuRoot;
        [SerializeField] private UIDocument pauseMenuDocument;
        [SerializeField] private KeyConfigMenuToggle keyConfigMenu;
        [SerializeField] private InputActionsConfig inputActionsConfig;

        private InputActionBinding toggleBinding;
        private Button keyConfigButton;
        private Button characterSelectButton;
        private Button closeButton;

        private void OnEnable()
        {
            toggleBinding = InputActionBinding.Bind(
                inputActionsConfig?.FindAction("UI/MenuToggle"),
                OnTogglePerformed);
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }

        private void OnDisable()
        {
            toggleBinding?.Dispose();
            toggleBinding = null;
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            UnbindButtons();
        }

        private void OnTogglePerformed(InputAction.CallbackContext context)
        {
            if (keyConfigMenu != null && keyConfigMenu.IsVisible)
            {
                keyConfigMenu.Hide();
                return;
            }

            var characterMenu = FindFirstObjectByType<CharacterSelectMenuToggle>(FindObjectsInactive.Include);
            if (characterMenu != null && characterMenu.IsVisible)
            {
                characterMenu.Hide();
                return;
            }

            Toggle();
        }

        private void OnActiveSceneChanged(Scene previousScene, Scene nextScene) => Hide();

        public void Toggle()
        {
            if (pauseMenuRoot == null) return;
            if (pauseMenuRoot.activeSelf) Hide();
            else Show();
        }

        public void Show()
        {
            if (pauseMenuRoot == null) return;
            pauseMenuRoot.SetActive(true);
            BindButtons();
            UnityEngine.Cursor.visible = true;
            keyConfigButton?.Focus();
        }

        public void Hide()
        {
            if (pauseMenuRoot == null) return;
            UnbindButtons();
            pauseMenuRoot.SetActive(false);
            UnityEngine.Cursor.visible = false;
        }

        private void BindButtons()
        {
            UnbindButtons();
            var root = pauseMenuDocument?.rootVisualElement;
            if (root == null) return;

            keyConfigButton = root.Q<Button>("keyconfig-button");
            characterSelectButton = root.Q<Button>("character-select-button");
            closeButton = root.Q<Button>("close-button");
            if (keyConfigButton != null) keyConfigButton.clicked += OpenKeyConfig;
            if (characterSelectButton != null) characterSelectButton.clicked += OpenCharacterSelect;
            if (closeButton != null) closeButton.clicked += Hide;
        }

        private void UnbindButtons()
        {
            if (keyConfigButton != null) keyConfigButton.clicked -= OpenKeyConfig;
            if (characterSelectButton != null) characterSelectButton.clicked -= OpenCharacterSelect;
            if (closeButton != null) closeButton.clicked -= Hide;
            keyConfigButton = null;
            characterSelectButton = null;
            closeButton = null;
        }

        private void OpenKeyConfig()
        {
            Hide();
            keyConfigMenu?.Show();
        }

        private void OpenCharacterSelect()
        {
            var characterMenu = FindFirstObjectByType<CharacterSelectMenuToggle>(FindObjectsInactive.Include);
            Hide();
            characterMenu?.Show();
        }
    }
}
