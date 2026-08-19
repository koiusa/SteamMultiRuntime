using Koiusa.Input;
using Koiusa.Keyconfig.Runtime;
using Koiusa.SteamMultiRuntime.Character.UI;
using Koiusa.UI.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Koiusa.SteamMultiRuntime
{
    [DisallowMultipleComponent]
    public sealed class PauseMenuController : MonoBehaviour, IUiMenu
    {
        [SerializeField] private GameObject pauseMenuRoot;
        [SerializeField] private UIDocument pauseMenuDocument;
        [SerializeField] private KeyConfigMenuToggle keyConfigMenu;
        [SerializeField] private InputActionsConfig inputActionsConfig;

        private InputActionBinding toggleBinding;
        private UiNavigationInputSession navigationSession;
        private Button keyConfigButton;
        private Button characterSelectButton;
        private Button closeButton;
        private CharacterSelectMenuToggle characterMenu;
        private int selectedButtonIndex;

        public bool IsVisible => pauseMenuRoot != null && pauseMenuRoot.activeSelf;

        private void OnEnable()
        {
            toggleBinding = InputActionBinding.Bind(
                inputActionsConfig?.FindAction("UI/MenuToggle"),
                OnTogglePerformed);
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }

        private void OnDisable()
        {
            DisposeNavigationSession();
            toggleBinding?.Dispose();
            toggleBinding = null;
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            UnbindButtons();
        }

        private void OnTogglePerformed(InputAction.CallbackContext context)
        {
            if (UiMenuNavigator.Current != null && !ReferenceEquals(UiMenuNavigator.Current, this))
            {
                UiMenuNavigator.Back();
                return;
            }

            Toggle();
        }

        private void OnActiveSceneChanged(Scene previousScene, Scene nextScene) => UiMenuNavigator.CloseAll();

        public void Toggle()
        {
            UiMenuNavigator.ToggleRoot(this);
        }

        public void Show() => UiMenuNavigator.OpenRoot(this);

        public void Hide() => UiMenuNavigator.Close(this);

        public void Activate()
        {
            if (pauseMenuRoot == null) return;
            DisposeNavigationSession();
            pauseMenuRoot.SetActive(true);
            BindButtons();
            selectedButtonIndex = 0;
            navigationSession = new UiNavigationInputSession(
                inputActionsConfig,
                MoveSelection,
                SubmitSelection,
                Hide,
                pauseMenuDocument?.rootVisualElement);
        }

        public void Deactivate()
        {
            if (pauseMenuRoot == null) return;
            DisposeNavigationSession();
            UnbindButtons();
            pauseMenuRoot.SetActive(false);
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
            keyConfigButton?.RegisterCallback<FocusInEvent>(OnKeyConfigFocused);
            characterSelectButton?.RegisterCallback<FocusInEvent>(OnCharacterSelectFocused);
            closeButton?.RegisterCallback<FocusInEvent>(OnCloseFocused);
        }

        private void UnbindButtons()
        {
            if (keyConfigButton != null) keyConfigButton.clicked -= OpenKeyConfig;
            if (characterSelectButton != null) characterSelectButton.clicked -= OpenCharacterSelect;
            if (closeButton != null) closeButton.clicked -= Hide;
            keyConfigButton?.UnregisterCallback<FocusInEvent>(OnKeyConfigFocused);
            characterSelectButton?.UnregisterCallback<FocusInEvent>(OnCharacterSelectFocused);
            closeButton?.UnregisterCallback<FocusInEvent>(OnCloseFocused);
            keyConfigButton = null;
            characterSelectButton = null;
            closeButton = null;
        }

        private void ScheduleInitialFocus()
        {
            var root = pauseMenuDocument?.rootVisualElement;
            root?.schedule.Execute(() =>
            {
                if (pauseMenuRoot != null && pauseMenuRoot.activeInHierarchy)
                {
                    FocusSelectedButton();
                }
            });
        }

        private void MoveSelection(UiNavigationDirection direction)
        {
            if (direction != UiNavigationDirection.Up && direction != UiNavigationDirection.Down &&
                direction != UiNavigationDirection.Left && direction != UiNavigationDirection.Right)
            {
                return;
            }

            var offset = direction == UiNavigationDirection.Up || direction == UiNavigationDirection.Left
                ? -1
                : 1;
            selectedButtonIndex = (selectedButtonIndex + offset + 3) % 3;
            FocusSelectedButton();
        }

        private void SubmitSelection()
        {
            switch (selectedButtonIndex)
            {
                case 0:
                    OpenKeyConfig();
                    break;
                case 1:
                    OpenCharacterSelect();
                    break;
                case 2:
                    Hide();
                    break;
            }
        }

        private void FocusSelectedButton()
        {
            switch (selectedButtonIndex)
            {
                case 0:
                    keyConfigButton?.Focus();
                    break;
                case 1:
                    characterSelectButton?.Focus();
                    break;
                case 2:
                    closeButton?.Focus();
                    break;
            }
        }

        private void DisposeNavigationSession()
        {
            navigationSession?.Dispose();
            navigationSession = null;
        }

        private void OnKeyConfigFocused(FocusInEvent evt) => selectedButtonIndex = 0;

        private void OnCharacterSelectFocused(FocusInEvent evt) => selectedButtonIndex = 1;

        private void OnCloseFocused(FocusInEvent evt) => selectedButtonIndex = 2;

        private void ResolveCharacterMenu()
        {
            characterMenu = CharacterSelectMenuRegistry.Current;
        }

        private void OpenKeyConfig()
        {
            if (keyConfigMenu != null) UiMenuNavigator.Push(keyConfigMenu);
        }

        private void OpenCharacterSelect()
        {
            ResolveCharacterMenu();
            if (characterMenu != null) UiMenuNavigator.Push(characterMenu);
        }

        public void FocusInitial() => ScheduleInitialFocus();
    }
}
